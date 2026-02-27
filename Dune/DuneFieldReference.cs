
using System;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Dune;

public sealed class DuneFieldReference : DuneMemberReference<DuneFieldSignature>, IEquatable<DuneFieldReference> {

    public static DuneFieldReference FromType<T>(string fieldName, DuneReflectionContext? ctx = null)
#if NET
        where T : allows ref struct
#endif
        => FromType(typeof(T), fieldName, ctx);

    public static DuneFieldReference FromType(Type type, string fieldName, DuneReflectionContext? ctx = null) {
        InternalUtils.ThrowIfArgumentNull(type);
        InternalUtils.ThrowIfArgumentNullOrWhitespace(fieldName);

        return FromFieldInfo(
            type.GetField(fieldName, DuneReflectionContext.EverythingWithinFlags)
                ?? throw new ArgumentException($"No field '{fieldName}' found on type {type}."),
            ctx);
    }

    public static DuneFieldReference FromFieldInfo(FieldInfo fieldInfo, DuneReflectionContext? ctx = null) {
        InternalUtils.ThrowIfArgumentNull(fieldInfo);
        InternalUtils.Assert(fieldInfo.DeclaringType != null);

        ctx ??= new();
        if (ctx.TryGetFieldReference(fieldInfo, out var cached))
            return cached;

        return ctx.PutFieldReference(fieldInfo, new(
            DuneFieldSignature.FromFieldInfo(fieldInfo, ctx),
            DuneTypeSignatureReference.FromType(fieldInfo.DeclaringType, ctx)
        ));
    }

    public static DuneFieldReference FromCecilReference(CecilFieldReference fieldReference, DuneCecilContext? ctx = null) {
        InternalUtils.ThrowIfArgumentNull(fieldReference);

        ctx ??= new();
        if (ctx.TryGetFieldReference(fieldReference, out var cached))
            return cached;

        return ctx.PutFieldReference(fieldReference, new(
            DuneFieldSignature.FromCecilDefinition(fieldReference.Resolve(), ctx),
            DuneTypeSignatureReference.FromCecilReference(fieldReference.DeclaringType, ctx)
        ));
    }

    public static DuneFieldReference FromSymbol(IFieldSymbol fieldSymbol, DuneRoslynContext? ctx = null) {
        InternalUtils.ThrowIfArgumentNull(fieldSymbol);

        ctx ??= new();
        if (ctx.TryGetFieldReference(fieldSymbol, out var cached))
            return cached;

        return ctx.PutFieldReference(fieldSymbol, new(
            DuneFieldSignature.FromSymbol(fieldSymbol, ctx),
            DuneTypeSignatureReference.FromSymbol(fieldSymbol.ContainingType, ctx)
        ));
    }

    public DuneTypeReference Type { get; }

    internal DuneFieldReference(DuneFieldSignature signature, DuneTypeSignatureReference declaringType): base(signature, declaringType) {
        Type = signature.Type.Resolve(declaringType);
    }

    public override string ToString()
        => ToString(DuneTypeFormat.Default, DuneTypeFormat.DefaultMinimal);

    public string ToString(in DuneTypeFormat fieldFormat)
        => ToString(fieldFormat, DuneTypeFormat.DefaultMinimal);

    public string ToString(in DuneTypeFormat fieldFormat, DuneTypeFormat? typeFormat) {
        StringBuilder sb = new();

        if (typeFormat != null) {
            typeFormat.Value.AppendType(
                fieldFormat.IncludeGenericParameters ? Signature.Type : Type, sb
            );
            sb.Append(' ');
        }

        fieldFormat.AppendMemberName(this, sb);
        fieldFormat.AppendAssemblySuffix(this, sb);

        return sb.ToString();
    }

    public override bool Equals(object? obj) => Equals(obj as DuneFieldReference);
    public override bool Equals(IDuneSymbol? other) => Equals(other as DuneFieldReference);
    public bool Equals(DuneFieldReference? other) {
        if (ReferenceEquals(this, other)) return true;
        if (other is null) return false;

        if (Name != other.Name) return false;
        if (DeclaringType != other.DeclaringType) return false;
        if (Type != other.Type) return false;

        return true;
    }

    public static bool operator ==(DuneFieldReference? a, DuneFieldReference? b) => a?.Equals(b) ?? b is null;
    public static bool operator !=(DuneFieldReference? a, DuneFieldReference? b) => !(a == b);

    public override int GetHashCode()
        => InternalUtils.HashCodeCombine(DeclaringType, Name, Type);

}
