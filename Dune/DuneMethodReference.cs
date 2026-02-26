
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Mono.Cecil;

namespace Dune;

public sealed class DuneMethodReference : IDuneMemberReference, IDuneGenericReference, IEquatable<DuneMethodReference> {

    public static DuneMethodReference FromDelegate(Delegate @delegate, DuneReflectionContext? ctx = null) {
        InternalUtils.ThrowIfArgumentNull(@delegate);
        return FromMethodInfo(@delegate.Method, ctx);
    }

    public static DuneMethodReference FromMethodInfo(MethodInfo methodInfo, DuneReflectionContext? ctx = null) {
        InternalUtils.ThrowIfArgumentNull(methodInfo);

        if (methodInfo.IsGenericMethodDefinition)
            throw new ArgumentException($"Cannot create a method reference from a generic method definition.");

        InternalUtils.Assert(methodInfo.DeclaringType != null);

        ctx ??= new();
        if (ctx.TryGetMethodReference(methodInfo, out var cached))
            return cached;

        return ctx.PutMethodReference(methodInfo, new(
            DuneMethodSignature.FromMethodInfo(methodInfo, ctx),
            DuneTypeSignatureReference.FromType(methodInfo.DeclaringType),
            [..methodInfo.GetGenericArguments().Select(
                arg => DuneTypeReference.FromType(arg, ctx)
            )]
        ));
    }

    public static DuneMethodReference FromConstructorInfo(ConstructorInfo constructorInfo, DuneReflectionContext? ctx = null) {
        InternalUtils.ThrowIfArgumentNull(constructorInfo);

        ctx ??= new();
        if (ctx.TryGetMethodReference(constructorInfo, out var cached))
            return cached;

        InternalUtils.Assert(constructorInfo.DeclaringType != null);

        return ctx.PutMethodReference(constructorInfo, new(
            DuneMethodSignature.FromConstructorInfo(constructorInfo, ctx),
            DuneTypeSignatureReference.FromType(constructorInfo.DeclaringType),
            []
        ));
    }

    public static DuneMethodReference FromMethodBase(MethodBase methodBase, DuneReflectionContext? ctx = null) {
        InternalUtils.ThrowIfArgumentNull(methodBase);

        return methodBase switch {
            MethodInfo methodInfo => FromMethodInfo(methodInfo, ctx),
            ConstructorInfo constructorInfo => FromConstructorInfo(constructorInfo, ctx),
            _ => throw new ArgumentException($"Unknown method type {methodBase.GetType()}"),
        };
    }

    public static DuneMethodReference FromCecilReference(CecilMethodReference methodReference, DuneCecilContext? ctx = null) {
        InternalUtils.ThrowIfArgumentNull(methodReference);

        ctx ??= new();
        if (ctx.TryGetMethodReference(methodReference, out var cached))
            return cached;


        IEnumerable<DuneTypeReference> genericArguments = [];

        if (methodReference is GenericInstanceMethod instanceRef) {
            genericArguments = instanceRef.GenericArguments.Select(
                arg => DuneTypeReference.FromCecilReference(arg, ctx)
            );

            methodReference = instanceRef.ElementMethod;
        }

        return ctx.PutMethodReference(methodReference, new(
            DuneMethodSignature.FromCecilDefinition(methodReference.Resolve(), ctx),
            DuneTypeSignatureReference.FromCecilReference(methodReference.DeclaringType, ctx),
            [.. genericArguments]
        ));
    }

    public static DuneMethodReference FromSymbol(IMethodSymbol methodSymbol, DuneRoslynContext? ctx = null) {
        InternalUtils.ThrowIfArgumentNull(methodSymbol);

        ctx ??= new();
        if (ctx.TryGetMethodReference(methodSymbol, out var cached))
            return cached;

        return ctx.PutMethodReference(methodSymbol, new(
            DuneMethodSignature.FromSymbol(methodSymbol, ctx),
            methodSymbol.ContainingType == null ? null : DuneTypeSignatureReference.FromSymbol(methodSymbol.ContainingType, ctx),
            [.. methodSymbol.TypeArguments.Select(arg => DuneTypeReference.FromSymbol(arg, false, ctx))]
        ));
    }

    public DuneMethodSignature Signature { get; }
    public DuneAssemblyReference Assembly => Signature.Assembly;

    public string Name => Signature.Name;
    public DuneTypeSignatureReference? DeclaringType { get; }
    public ImmutableArray<DuneTypeReference> GenericArguments { get; }
    public bool HasGenericParameters => !GenericArguments.IsEmpty;

    public DuneTypeReference? ReturnType { get; }
    public ImmutableArray<DuneMethodParameter> Parameters { get; }

    IDuneGenericSignature IDuneGenericReference.Signature => Signature;
    IDuneMemberSignature IDuneMemberReference.Signature => Signature;

    IDuneType? IDuneMember.DeclaringType => DeclaringType;

    internal DuneMethodReference(DuneMethodSignature signature, DuneTypeSignatureReference? declaringType, ImmutableArray<DuneTypeReference> genericArgs) {
        InternalUtils.Assert(genericArgs.Length == signature.GenericParameterCount);

        Signature = signature;
        DeclaringType = declaringType;
        GenericArguments = genericArgs;

        ReturnType = signature.ReturnType?.Resolve(DeclaringType, this) ?? null;
        Parameters = Signature.Parameters.Select(
            param => new DuneMethodParameter(param.Name, param.Type.Resolve(DeclaringType, this))
        ).ToImmutableArray();
    }

    public override string ToString()
    => ToString(DuneTypeFormat.Default, DuneTypeFormat.DefaultMinimal, DuneTypeFormat.DefaultMinimal);

    public string ToString(in DuneTypeFormat methodFormat)
        => ToString(methodFormat, DuneTypeFormat.DefaultMinimal, DuneTypeFormat.DefaultMinimal);

    public string ToString(in DuneTypeFormat methodFormat, DuneTypeFormat? returnFormat, DuneTypeFormat? parameterFormat) {
        StringBuilder sb = new();

        if (returnFormat != null) {
            returnFormat.Value.AppendType(
                methodFormat.IncludeGenericParameters ? Signature.ReturnType : ReturnType, sb
            );
            sb.Append(' ');
        }

        methodFormat.AppendMemberName(this, sb);
        methodFormat.AppendGenericParameters(this, sb);

        if (parameterFormat != null) {
            sb.Append('(');

            sb.AppendEnumerable(
                methodFormat.IncludeGenericParameters ? Signature.Parameters : Parameters,
                (param, sb) => {
                    parameterFormat.Value.AppendType(param.Type, sb);
                    sb.Append(' ');
                    sb.Append(param.Name);
                }
            );
            
            sb.Append(')');
        }

        methodFormat.AppendAssemblySuffix(this, sb);

        return sb.ToString();
    }

    StringBuilder IDuneMember.FormatAndAppendName(in DuneTypeFormat format, StringBuilder sb) {
        return sb.Append(Name);
    }

    StringBuilder IDuneGenericSymbol.FormatAndAppendGenericParameters(in DuneTypeFormat genericFormat, StringBuilder sb) {
        return genericFormat.AppendTypes(GenericArguments, sb);
    }

    public override bool Equals(object? obj) => Equals(obj as DuneMethodReference);
    public bool Equals(IDuneSymbol? other) => Equals(other as DuneMethodReference);
    public bool Equals(DuneMethodReference? other) {
        if (ReferenceEquals(this, other)) return true;
        if (other is null) return false;

        if (!other.Signature.Equals(Signature)) return false;
        if (!other.GenericArguments.SequenceEqual(GenericArguments)) return false;
        return true;
    }

    public static bool operator ==(DuneMethodReference? a, DuneMethodReference? b) => a?.Equals(b) ?? b is null;
    public static bool operator !=(DuneMethodReference? a, DuneMethodReference? b) => !(a == b);

    public override int GetHashCode() {
        int hash = Signature.GetHashCode();
        foreach (DuneTypeReference generic in GenericArguments)
            hash = InternalUtils.HashCodeCombine(hash, generic);
        return hash;
    }


}
