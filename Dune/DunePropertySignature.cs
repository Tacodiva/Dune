
using System;
using System.Reflection;
using System.Reflection.Metadata;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Dune;

public sealed class DunePropertySignature : DuneMemberSignature, IEquatable<DunePropertySignature> {

    public static DunePropertySignature FromType<T>(string propertyName, DuneReflectionContext? ctx = null)
#if NET
        where T : allows ref struct
#endif
        => FromType(typeof(T), propertyName, ctx);

    public static DunePropertySignature FromType(Type type, string propertyName, DuneReflectionContext? ctx = null)
        => FromPropertyInfo(
            type.GetProperty(propertyName, DuneReflectionContext.EverythingWithinFlags)
                ?? throw new ArgumentException($"No property '{propertyName}' found on type {type}."),
            ctx);

    public static DunePropertySignature FromPropertyInfo(PropertyInfo propertyInfo, DuneReflectionContext? ctx = null) {
        ctx ??= new();
        if (ctx.TryGetPropertySignature(propertyInfo, out var cached))
            return cached;

        Type declaringType = propertyInfo.DeclaringType ??
            throw new NotSupportedException($"Properties without a declaring type are not supported.");

        if (declaringType.IsGenericType && !declaringType.IsGenericTypeDefinition) {
            declaringType = declaringType.GetGenericTypeDefinition();
            propertyInfo = declaringType.GetProperty(propertyInfo.Name, DuneReflectionContext.EverythingWithinFlags)!;
            InternalUtils.Assert(propertyInfo != null, "Property not found on generic type definition.");
        }

        return ctx.PutPropertySignature(propertyInfo, new(
            DuneAssemblyReference.FromAssembly(propertyInfo.Module.Assembly, ctx),
            DuneTypeSignature.FromType(declaringType, ctx),
            propertyInfo.Name,
            DuneTypeReference.FromType(propertyInfo.PropertyType, ctx),
            propertyInfo.GetMethod == null ? null : DuneMethodSignature.FromMethodInfo(propertyInfo.GetMethod, ctx),
            propertyInfo.SetMethod == null ? null : DuneMethodSignature.FromMethodInfo(propertyInfo.SetMethod, ctx)
        ));
    }

    public static DunePropertySignature FromCecilDefinition(CecilPropertyDefinition propertyDefinition, DuneCecilContext? ctx = null) {
        ctx ??= new();

        return new(
            DuneAssemblyReference.FromCecilDefinition(propertyDefinition.Module.Assembly),
            DuneTypeSignature.FromCecilDefinition(propertyDefinition.DeclaringType, ctx),
            propertyDefinition.Name,
            DuneTypeReference.FromCecilReference(propertyDefinition.PropertyType, ctx),
            propertyDefinition.GetMethod == null ? null : DuneMethodSignature.FromCecilDefinition(propertyDefinition.GetMethod, ctx),
            propertyDefinition.SetMethod == null ? null : DuneMethodSignature.FromCecilDefinition(propertyDefinition.SetMethod, ctx)
        );
    }

    public static DunePropertySignature FromSymbol(IPropertySymbol propertySymbol, DuneRoslynContext? ctx = null) {
        ctx ??= new();
        if (ctx.TryGetPropertySignature(propertySymbol, out var cached))
            return cached;

        return ctx.PutPropertySignature(propertySymbol, new(
            DuneAssemblyReference.FromSymbol(propertySymbol.ContainingAssembly, ctx),
            DuneTypeSignature.FromSymbol(propertySymbol.ContainingType, ctx),
            propertySymbol.MetadataName,
            DuneTypeReference.FromSymbol(propertySymbol.Type, propertySymbol.RefKind, ctx),
            propertySymbol.GetMethod == null ? null : DuneMethodSignature.FromSymbol(propertySymbol.GetMethod, ctx),
            propertySymbol.SetMethod == null ? null : DuneMethodSignature.FromSymbol(propertySymbol.SetMethod, ctx)
        ));
    }

    public DuneTypeReference Type { get; }

    public DuneMethodSignature? GetMethod { get; }
    public DuneMethodSignature? SetMethod { get; }

    private DunePropertySignature(DuneAssemblyReference assembly, DuneTypeSignature declaringType, string name, DuneTypeReference type,
        DuneMethodSignature? getMethod, DuneMethodSignature? setMethod) : base(name, declaringType, assembly) {
        Type = type;

        GetMethod = getMethod;
        SetMethod = setMethod;
    }

    public DunePropertyReference CreateReference(DuneTypeSignatureReference declaringTypeReference) {
        if (!declaringTypeReference.Signature.Equals(DeclaringType))
            throw new ArgumentException($"{nameof(declaringTypeReference)} must reference the same type definition as the property's declaring type.");

        return new(this, declaringTypeReference);
    }

    public override string ToString()
        => ToString(DuneTypeFormat.Default, DuneTypeFormat.DefaultMinimal);

    public string ToString(in DuneTypeFormat propertyFormat)
        => ToString(propertyFormat, DuneTypeFormat.DefaultMinimal);

    public string ToString(in DuneTypeFormat propertyFormat, DuneTypeFormat? typeFormat) {
        StringBuilder sb = new();

        if (typeFormat != null) {
            typeFormat.Value.AppendType(Type, sb);
            sb.Append(' ');
        }

        propertyFormat.AppendMemberName(this, sb);

        propertyFormat.AppendAssemblySuffix(this, sb);

        return sb.ToString();
    }

    public bool Matches(PropertyInfo? property, DuneReflectionContext? ctx = null) {
        if (property is null) return false;
        if (property.Name != Name) return false;
        if (!DeclaringType.Matches(property.DeclaringType, ctx)) return false;

        if (GetMethod == null) {
            if (property.GetMethod != null) return false;
        } else {
            if (!GetMethod.Matches(property.GetMethod, ctx)) return false;
        }

        if (SetMethod == null) {
            if (property.SetMethod != null) return false;
        } else {
            if (!SetMethod.Matches(property.SetMethod, ctx)) return false;
        }

        return true;
    }

    public bool Matches(CecilPropertyDefinition? property, DuneCecilContext? ctx = null) {
        if (property is null) return false;
        if (property.Name != Name) return false;
        if (!DeclaringType.Matches(property.DeclaringType, ctx)) return false;

        if (GetMethod == null) {
            if (property.GetMethod != null) return false;
        } else {
            if (!GetMethod.Matches(property.GetMethod, ctx)) return false;
        }

        if (SetMethod == null) {
            if (property.SetMethod != null) return false;
        } else {
            if (!SetMethod.Matches(property.SetMethod, ctx)) return false;
        }
        return true;
    }

    public bool Matches(DunePropertySignature? property, DuneContext? ctx = null) {
        if (ReferenceEquals(this, property)) return true;
        if (property is null) return false;
        if (property.Name != Name) return false;
        if (!DeclaringType.Matches(property.DeclaringType, ctx)) return false;

        if (GetMethod == null) {
            if (property.GetMethod != null) return false;
        } else {
            if (!GetMethod.Matches(property.GetMethod, ctx)) return false;
        }

        if (SetMethod == null) {
            if (property.SetMethod != null) return false;
        } else {
            if (!SetMethod.Matches(property.SetMethod, ctx)) return false;
        }

        return true;
    }

    public override bool Equals(object? obj) => Equals(obj as DunePropertySignature);
    public override bool Equals(IDuneSymbol? other) => Equals(other as DunePropertySignature);
    public bool Equals(DunePropertySignature? other) {
        if (ReferenceEquals(this, other)) return true;
        if (other is null) return false;

        if (Name != other.Name) return false;
        if (Assembly != other.Assembly) return false;
        if (DeclaringType != other.DeclaringType) return false;
        if (Type != other.Type) return false;
        return true;
    }

    public static bool operator ==(DunePropertySignature? a, DunePropertySignature? b) => a?.Equals(b) ?? b is null;
    public static bool operator !=(DunePropertySignature? a, DunePropertySignature? b) => !(a == b);

    public override int GetHashCode()
        => InternalUtils.HashCodeCombine(DeclaringType, Name, Type);
}