
using System;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Dune;

public sealed class DunePropertyReference : DuneMemberReference<DunePropertySignature>, IEquatable<DunePropertyReference> {

    public static DunePropertyReference FromType<T>(string propertyName, DuneReflectionContext? ctx = null)
#if NET
        where T : allows ref struct
#endif
        => FromType(typeof(T), propertyName, ctx);

    public static DunePropertyReference FromType(Type type, string propertyName, DuneReflectionContext? ctx = null) {
        InternalUtils.ThrowIfArgumentNull(type);
        InternalUtils.ThrowIfArgumentNullOrWhitespace(propertyName);

        return FromPropertyInfo(
            type.GetProperty(propertyName, DuneReflectionContext.EverythingFlags)
                ?? throw new ArgumentException($"No property '{propertyName}' found on type {type}."),
            ctx);
    }

    public static DunePropertyReference FromPropertyInfo(PropertyInfo propertyInfo, DuneReflectionContext? ctx = null) {
        InternalUtils.ThrowIfArgumentNull(propertyInfo);
        InternalUtils.Assert(propertyInfo.DeclaringType != null);

        ctx ??= new();
        if (ctx.TryGetPropertyReference(propertyInfo, out var cached))
            return cached;

        return ctx.PutPropertyReference(propertyInfo, new(
            DunePropertySignature.FromPropertyInfo(propertyInfo, ctx),
            DuneTypeSignatureReference.FromType(propertyInfo.DeclaringType, ctx)
        ));
    }

    public static DunePropertyReference FromCecilReference(CecilPropertyReference propertyReference, DuneCecilContext? ctx = null) {
        ctx ??= new();

        return new(
            DunePropertySignature.FromCecilDefinition(propertyReference.Resolve(), ctx),
            DuneTypeSignatureReference.FromCecilReference(propertyReference.DeclaringType, ctx)
        );
    }

    public static DunePropertyReference FromSymbol(IPropertySymbol propertySymbol, DuneRoslynContext? ctx = null) {
        InternalUtils.ThrowIfArgumentNull(propertySymbol);

        ctx ??= new();
        if (ctx.TryGetPropertyReference(propertySymbol, out var cached))
            return cached;

        return ctx.PutPropertyReference(propertySymbol, new(
            DunePropertySignature.FromSymbol(propertySymbol, ctx),
            DuneTypeSignatureReference.FromSymbol(propertySymbol.ContainingType, ctx)
        ));
    }

    public DuneTypeReference Type { get; }

    public DuneMethodReference? GetMethod { get; }
    public DuneMethodReference? SetMethod { get; }

    internal DunePropertyReference(DunePropertySignature signature, DuneTypeSignatureReference declaringType) : base(signature, declaringType) {
        Type = signature.Type.Resolve(declaringType);
        GetMethod = signature.GetMethod?.CreateReference(DeclaringType);
        SetMethod = signature.SetMethod?.CreateReference(DeclaringType);
    }

    public override string ToString()
        => ToString(DuneTypeFormat.Default, DuneTypeFormat.DefaultMinimal);

    public string ToString(in DuneTypeFormat propertyFormat)
        => ToString(propertyFormat, DuneTypeFormat.DefaultMinimal);

    public string ToString(in DuneTypeFormat propertyFormat, DuneTypeFormat? typeFormat) {
        StringBuilder sb = new();

        if (typeFormat != null) {
            typeFormat.Value.AppendType(
                propertyFormat.IncludeGenericParameters ? Signature.Type : Type, sb
            );
            sb.Append(' ');
        }

        propertyFormat.AppendMemberName(this, sb);

        propertyFormat.AppendAssemblySuffix(this, sb);

        return sb.ToString();
    }

    public override bool Equals(object? obj) => Equals(obj as DunePropertyReference);
    public override bool Equals(IDuneSymbol? other) => Equals(other as DunePropertyReference);
    public bool Equals(DunePropertyReference? other) {
        if (ReferenceEquals(this, other)) return true;
        if (other is null) return false;

        if (Name != other.Name) return false;
        if (DeclaringType != other.DeclaringType) return false;
        if (Type != other.Type) return false;

        return true;
    }

    public static bool operator ==(DunePropertyReference? a, DunePropertyReference? b) => a?.Equals(b) ?? b is null;
    public static bool operator !=(DunePropertyReference? a, DunePropertyReference? b) => !(a == b);

    public override int GetHashCode()
        => InternalUtils.HashCodeCombine(DeclaringType, Name, Type);
}
