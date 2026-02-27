
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using Microsoft.CodeAnalysis;
using Mono.Cecil;

namespace Dune;

public sealed class DuneTypeSignature : IDuneType, IDuneMemberSignature, IDuneGenericSignature, IEquatable<DuneTypeSignature> {

    internal static Type GetGenericTypeDefinition(Type type) {
        if (type.IsGenericType && !type.IsGenericTypeDefinition)
            type = type.GetGenericTypeDefinition();
        return type;
    }

    public static DuneTypeSignature FromType<T>(DuneReflectionContext? ctx = null)
#if NET
        where T : allows ref struct
#endif
        => FromType(typeof(T), ctx);

    public static DuneTypeSignature FromType(Type type, DuneReflectionContext? ctx = null) {
        InternalUtils.ThrowIfArgumentNull(type);

        ctx ??= new();
        if (ctx.TryGetTypeSignature(type, out var cached))
            return cached;

        type = GetGenericTypeDefinition(type);

        return ctx.PutTypeSignature(type, new(
            DuneAssemblyReference.FromAssembly(type.Assembly, ctx),
            type.Namespace, type.Name,
            type.GetGenericArguments().Select(arg => arg.Name),
            type.DeclaringType != null ? FromType(type.DeclaringType, ctx) : null
        ));
    }

    public static DuneTypeSignature FromCecilDefinition(CecilTypeDefinition typeDefinition, DuneCecilContext? ctx = null) {
        InternalUtils.ThrowIfArgumentNull(typeDefinition);

        ctx ??= new();
        if (ctx.TryGetTypeSignature(typeDefinition, out var cached))
            return cached;

        DuneTypeSignature? declaringType = typeDefinition.DeclaringType != null ? FromCecilDefinition(typeDefinition.DeclaringType) : null;

        return ctx.PutTypeSignature(typeDefinition, new(
            DuneAssemblyReference.FromCecilDefinition(typeDefinition.Module.Assembly, ctx),

            // In Cecil, the "Namespace" is only present in the base declaring type, so if this is a nested
            //  type we grab the namespace from our parent type.
            declaringType != null ? declaringType.Namespace : typeDefinition.Namespace,

            typeDefinition.Name,
            typeDefinition.GenericParameters.Select(param => param.Name),
            declaringType
        ));
    }

    public static DuneTypeSignature FromSymbol(INamedTypeSymbol typeSymbol, DuneRoslynContext? ctx = null) {
        InternalUtils.ThrowIfArgumentNull(typeSymbol);

        ctx ??= new();
        if (ctx.TryGetTypeSignature(typeSymbol, out var cached))
            return cached;

        DuneTypeSignature? containingType = null;
        IEnumerable<string> typeParameters = typeSymbol.TypeParameters.Select(param => param.Name);
        if (typeSymbol.ContainingType != null) {
            containingType = FromSymbol(typeSymbol.ContainingType, ctx);
            // We flatten the generic parameters, so we go from
            // class Outer<T0> { class Inner<T1> { } }
            // To
            // class Outer<T0> { class Inner<T0, T1> { } }
            typeParameters = containingType.GenericParameterNames.Concat(typeParameters);
        }

        return ctx.PutTypeSignature(typeSymbol, new(
            DuneAssemblyReference.FromSymbol(typeSymbol.ContainingAssembly, ctx),
            typeSymbol.ContainingNamespace.MetadataName, typeSymbol.MetadataName,
            typeParameters, containingType
        ));

    }

    public static DuneTypeSignature Void { get; } = FromType(typeof(void));
    public static DuneTypeSignature Object { get; } = FromType<object>();

    public DuneAssemblyReference Assembly { get; }

    public string? Namespace { get; }
    public string Name => GetName();
    public string RawName { get; }

    public DuneTypeSignature? DeclaringType { get; }

    [MemberNotNullWhen(true, nameof(DeclaringType))]
    public bool HasDeclaringType => DeclaringType != null;

    public ImmutableArray<string> GenericParameterNames { get; }
    public bool HasGenericParameters => !GenericParameterNames.IsEmpty;
    public int GenericParameterCount => GenericParameterNames.Length;

    // TODO This should be better. Right now it depends on the void being from the exact same version
    public bool IsVoid => this == Void;

    IDuneType? IDuneMember.DeclaringType => DeclaringType;

    internal DuneTypeSignature(DuneAssemblyReference assembly, string? @namespace, string name, IEnumerable<string> genericParamNames, DuneTypeSignature? declaringType) {
        Assembly = assembly;
        Namespace = string.IsNullOrEmpty(@namespace) ? null : @namespace;
        RawName = name;

        GenericParameterNames = [.. genericParamNames];
        DeclaringType = declaringType;

        InternalUtils.Assert(!HasDeclaringType || Namespace == DeclaringType.Namespace);
        InternalUtils.Assert(!HasDeclaringType || Assembly == DeclaringType.Assembly);
    }

    public bool IsDeclaringTypeOf(DuneTypeSignature other) {
        DuneTypeSignature? check = other.DeclaringType;
        while (check != null) {
            if (Equals(check)) return true;
            check = check.DeclaringType;
        }
        return false;
    }


    public DuneTypeSignatureReference CreateReference()
        => CreateReference([.. Enumerable.Repeat<DuneTypeReference>(DuneUnknownTypeReference.Instance, GenericParameterCount)]);

    public DuneTypeSignatureReference CreateReferenceWithParent(DuneTypeSignatureReference parentType)
        => CreateReference([
                .. parentType.GenericArguments,
                .. Enumerable.Repeat<DuneTypeReference>(DuneUnknownTypeReference.Instance, GenericParameterCount - parentType.GenericArguments.Length)
            ]);

    public DuneTypeSignatureReference CreateReferenceWithParent(DuneTypeSignatureReference parentType, params IReadOnlyCollection<DuneTypeReference> genericArgs)
        => CreateReference([.. parentType.GenericArguments, .. genericArgs]);

    public DuneTypeSignatureReference CreateReference(params IReadOnlyCollection<DuneTypeReference> genericArgs) {
        if (genericArgs.Count != GenericParameterCount)
            throw new ArgumentException($"Wrong number of generic arguments. Expected {GenericParameterCount} got {genericArgs.Count}.");

        return new(this, genericArgs);
    }

    public DuneGenericTypeReference CreateGenericParameterReference(int index) {
        // TODO Check range
        return new(index, this, DuneGenericSource.Type);
    }

    private static readonly DuneAssemblyReference _SystemAssembly = DuneAssemblyReference.FromAssembly(typeof(object).Assembly);

    private static readonly ImmutableDictionary<(string? Namespace, string Name), string> _SystemNames = new Dictionary<(string?, string), string>{
        {(typeof(bool).Namespace, typeof(bool).Name), "bool"},
        {(typeof(byte).Namespace, typeof(byte).Name), "byte"},
        {(typeof(sbyte).Namespace, typeof(sbyte).Name), "sbyte"},
        {(typeof(short).Namespace, typeof(short).Name), "short"},
        {(typeof(ushort).Namespace, typeof(ushort).Name), "ushort"},
        {(typeof(int).Namespace, typeof(int).Name), "int"},
        {(typeof(uint).Namespace, typeof(uint).Name), "uint"},
        {(typeof(long).Namespace, typeof(long).Name), "long"},
        {(typeof(ulong).Namespace, typeof(ulong).Name), "ulong"},

        {(typeof(char).Namespace, typeof(char).Name), "char"},
        {(typeof(float).Namespace, typeof(float).Name), "float"},
        {(typeof(double).Namespace, typeof(double).Name), "double"},
        {(typeof(decimal).Namespace, typeof(decimal).Name), "decimal"},

        {(typeof(string).Namespace, typeof(string).Name), "string"},
        {(typeof(object).Namespace, typeof(object).Name), "object"},

        {(typeof(nint).Namespace, typeof(nint).Name), "nint"},
        {(typeof(nuint).Namespace, typeof(nuint).Name), "nuint"},

        {(typeof(void).Namespace, typeof(void).Name), "void"}
    }.ToImmutableDictionary();

    public string GetName() {
        if (Assembly != _SystemAssembly) return RawName;
        if (DeclaringType != null) return RawName;
        return _SystemNames.GetValueOrDefault((Namespace, RawName)) ?? RawName;
    }

    private void AppendNameAsDeclaringType(StringBuilder sb) {
        DeclaringType?.AppendNameAsDeclaringType(sb);
        sb.Append(RawName);
        sb.Append('/');
    }

    public override string ToString() => ToString(DuneTypeFormat.Default);

    public string ToString(in DuneTypeFormat format) {
        return format.AppendType(this).ToString();
    }

    StringBuilder IDuneMember.FormatAndAppendName(in DuneTypeFormat format, StringBuilder sb)
        => (this as IDuneType).FormatAndAppendName(format, sb);

    StringBuilder IDuneType.FormatAndAppendName(in DuneTypeFormat format, StringBuilder sb) {
        if (format.IncludeNamespace && Namespace != null) {
            sb.Append(Namespace);
            sb.Append('.');
        }

        if (format.IncludeDeclaringTypes && DeclaringType != null) {
            DeclaringType.AppendNameAsDeclaringType(sb);
        }

        if (format.IncludeNamespace) sb.Append(RawName);
        else sb.Append(Name);

        return sb;
    }

    StringBuilder IDuneGenericSymbol.FormatAndAppendGenericParameters(in DuneTypeFormat genericFormat, StringBuilder sb) {
        return sb.Append(string.Join(", ", GenericParameterNames));
    }

    public bool Matches(Type? type, DuneReflectionContext? ctx = null) {
        if (type is null) return IsVoid;

        if (type.Name != RawName) return false;
        if (type.Namespace != Namespace) return false;
        if (type.GenericTypeArguments.Length != GenericParameterCount) return false;
        if (!Assembly.Matches(type.Assembly, ctx)) return false;

        if (DeclaringType == null) {
            if (type.DeclaringType != null) return false;
        } else {
            if (!DeclaringType.Matches(type.DeclaringType, ctx))
                return false;
        }

        return true;
    }

    public bool Matches(CecilTypeReference? type, DuneCecilContext? ctx = null) {
        if (type is null) return IsVoid;

        if (type.Name != RawName) return false;
        if (type.GenericParameters.Count != GenericParameterCount) return false;

        string? signatureNamespace = HasDeclaringType ? Namespace : null;
        string? definitionNamespace = string.IsNullOrWhiteSpace(type.Namespace) ? null : type.Namespace;

        if (signatureNamespace != definitionNamespace) return false;
        if (!Assembly.Matches(type.Module.Assembly, ctx)) return false;

        if (DeclaringType == null) {
            if (type.DeclaringType != null) return false;
        } else {
            if (!DeclaringType.Matches(type.DeclaringType, ctx))
                return false;
        }

        return true;
    }

    public bool Matches(DuneTypeSignature? type, DuneContext? ctx = null) {
        if (ReferenceEquals(this, type)) return true;
        if (type is null) return IsVoid;

        if (type.RawName != RawName) return false;
        if (type.GenericParameterCount != GenericParameterCount) return false;
        if (type.Namespace != Namespace) return false;
        if (!type.Assembly.Matches(Assembly, ctx)) return false;

        if (DeclaringType == null) {
            if (type.DeclaringType != null) return false;
        } else {
            if (!DeclaringType.Matches(type.DeclaringType, ctx))
                return false;
        }

        return true;
    }

    public override bool Equals(object? obj) => Equals(obj as DuneTypeSignature);
    public bool Equals(IDuneSymbol? other) => Equals(other as DuneTypeSignature);
    public bool Equals(DuneTypeSignature? other) {
        if (ReferenceEquals(this, other)) return true;
        if (other is null) return false;
        if (other.RawName != RawName) return false;
        if (other.Namespace != Namespace) return false;
        if (other.Assembly != Assembly) return false;
        if (other.DeclaringType != DeclaringType) return false;
        if (!other.GenericParameterNames.SequenceEqual(GenericParameterNames)) return false;
        return true;
    }

    public static bool operator ==(DuneTypeSignature? a, DuneTypeSignature? b) => a?.Equals(b) ?? b is null;
    public static bool operator !=(DuneTypeSignature? a, DuneTypeSignature? b) => !(a == b);

    public override int GetHashCode() =>
        InternalUtils.HashCodeCombine(Assembly, Namespace, RawName, GenericParameterCount, DeclaringType);
}