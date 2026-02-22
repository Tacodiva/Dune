
using System;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Dune;

public sealed class DuneFieldSignature : DuneMemberSignature, IEquatable<DuneFieldSignature> {

    public static DuneFieldSignature FromType<T>(string fieldName, DuneReflectionContext? ctx = null)
#if NET
        where T : allows ref struct
#endif
        => FromType(typeof(T), fieldName, ctx);

    public static DuneFieldSignature FromType(Type type, string fieldName, DuneReflectionContext? ctx = null)
        => FromFieldInfo(
            type.GetField(fieldName, DuneReflectionContext.EverythingFlags)
                ?? throw new ArgumentException($"No field '{fieldName}' found on type {type}."),
            ctx);

    public static DuneFieldSignature FromFieldInfo(FieldInfo fieldInfo, DuneReflectionContext? ctx = null) {

        ctx ??= new();
        if (ctx.TryGetFieldSignature(fieldInfo, out var cached))
            return cached;

        Type? declaringType = fieldInfo.DeclaringType;

        if (declaringType != null) {
            if (declaringType.IsGenericType && !declaringType.IsGenericTypeDefinition) {
                declaringType = declaringType.GetGenericTypeDefinition();
                fieldInfo = declaringType.GetField(fieldInfo.Name, DuneReflectionContext.EverythingFlags)!;
                InternalUtils.Assert(fieldInfo != null, "Field not found on generic type definition.");
            }
        }

        return ctx.PutFieldSignature(fieldInfo, new(
            DuneAssemblyReference.FromAssembly(fieldInfo.Module.Assembly, ctx),
            declaringType == null ? null : DuneTypeSignature.FromType(declaringType, ctx),
            fieldInfo.Name,
            DuneTypeReference.FromType(fieldInfo.FieldType, ctx)
        ));
    }

    public static DuneFieldSignature FromFieldDefinition(CecilFieldDefinition fieldDef, DuneCecilContext? ctx = null) {
        ctx ??= new();
        if (ctx.TryGetFieldSignature(fieldDef, out var cached))
            return cached;

        return ctx.PutFieldSignature(fieldDef, new(
            DuneAssemblyReference.FromAssemblyDefinition(fieldDef.Module.Assembly, ctx),
            DuneTypeSignature.FromTypeDefinition(fieldDef.DeclaringType, ctx),
            fieldDef.Name,
            DuneTypeReference.FromTypeReference(fieldDef.FieldType, ctx)
        ));
    }

    public static DuneFieldSignature FromSymbol(IFieldSymbol fieldSymbol, DuneRoslynContext? ctx = null) {
        ctx ??= new();
        if (ctx.TryGetFieldSignature(fieldSymbol, out var cached))
            return cached;

        return ctx.PutFieldSignature(fieldSymbol, new(
            DuneAssemblyReference.FromSymbol(fieldSymbol.ContainingAssembly, ctx),
            fieldSymbol.ContainingType == null ? null : DuneTypeSignature.FromSymbol(fieldSymbol.ContainingType, ctx),
            fieldSymbol.MetadataName,
            DuneTypeReference.FromSymbol(fieldSymbol.Type, fieldSymbol.RefKind, ctx)
        ));
    }

    public DuneTypeReference Type { get; }

    internal DuneFieldSignature(DuneAssemblyReference assembly, DuneTypeSignature? declaringType, string name, DuneTypeReference type): base(name, declaringType, assembly) {
        Type = type;
    }

    public DuneFieldReference CreateReference(DuneTypeSignatureReference declaringTypeReference) {
        if (!declaringTypeReference.Signature.Equals(DeclaringType))
            throw new ArgumentException($"{nameof(declaringTypeReference)} must reference the same type definition as the field's declaring type.");

        return new(this, declaringTypeReference);
    }

    public override string ToString()
        => ToString(DuneTypeFormat.Default, DuneTypeFormat.DefaultMinimal);

    public string ToString(in DuneTypeFormat fieldFormat)
        => ToString(fieldFormat, DuneTypeFormat.DefaultMinimal);

    public string ToString(in DuneTypeFormat fieldFormat, DuneTypeFormat? typeFormat) {
        StringBuilder sb = new();

        if (typeFormat != null) {
            typeFormat.Value.AppendType(Type, sb);
            sb.Append(' ');
        }

        fieldFormat.AppendMemberName(this, sb);
        fieldFormat.AppendAssemblySuffix(this, sb);

        return sb.ToString();
    }

    public override bool Equals(object? obj) => Equals(obj as DuneFieldSignature);
    public override bool Equals(IDuneSymbol? other) => Equals(other as DuneFieldSignature);
    public bool Equals(DuneFieldSignature? other) {
        if (ReferenceEquals(this, other)) return true;
        if (other is null) return false;

        if (Name != other.Name) return false;
        if (Assembly != other.Assembly) return false;
        if (DeclaringType != other.DeclaringType) return false;
        if (Type != other.Type) return false;
        return true;
    }

    public static bool operator ==(DuneFieldSignature? a, DuneFieldSignature? b) => a?.Equals(b) ?? b is null;
    public static bool operator !=(DuneFieldSignature? a, DuneFieldSignature? b) => !(a == b);

    public override int GetHashCode()
        => InternalUtils.HashCodeCombine(DeclaringType, Name, Type);
}