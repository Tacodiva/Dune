
using System.Reflection;

namespace Dune;

public sealed class DuneFieldDefinition : DuneDefinition<DuneFieldSignature> {

    public static DuneFieldDefinition FromFieldInfo(FieldInfo fieldInfo, DuneReflectionContext? ctx = null) {
        InternalUtils.ThrowIfArgumentNull(fieldInfo);
        InternalUtils.Assert(fieldInfo.DeclaringType != null);

        ctx ??= new();

        return new(
            DuneFieldSignature.FromFieldInfo(fieldInfo, ctx),
            DuneCustomAttributeContainer.FromMemberInfo(fieldInfo, ctx)
        );
    }

    public static DuneFieldDefinition FromFieldDefinition(CecilFieldDefinition fieldDefinition, DuneCecilContext? ctx = null) {
        InternalUtils.ThrowIfArgumentNull(fieldDefinition);

        ctx ??= new();

        return new(
            DuneFieldSignature.FromFieldDefinition(fieldDefinition, ctx),
            DuneCustomAttributeContainer.FromCecil(fieldDefinition, ctx)
        );
    }
    public DuneTypeReference Type => Signature.Type;

    internal DuneFieldDefinition(DuneFieldSignature signature, DuneCustomAttributeContainer customAttributes) : base(signature, customAttributes) {
    }
}