
using System.Reflection;
using System.Reflection.Metadata;

namespace Dune;

public sealed class DunePropertyDefinition : DuneDefinition<DunePropertySignature> {

    public static DunePropertyDefinition FromPropertyInfo(PropertyInfo propertyInfo, DuneReflectionContext? ctx = null) {
        InternalUtils.ThrowIfArgumentNull(propertyInfo);
        InternalUtils.Assert(propertyInfo.DeclaringType != null);

        ctx ??= new();

        return new(
            DunePropertySignature.FromPropertyInfo(propertyInfo, ctx),
            DuneCustomAttributeContainer.FromMemberInfo(propertyInfo, ctx)
        );
    }

    public static DunePropertyDefinition FromPropertyDefinition(CecilPropertyDefinition propertyDefinition, DuneCecilContext? ctx = null) {
        ctx ??= new();

        return new(
            DunePropertySignature.FromPropertyDefinition(propertyDefinition, ctx),
            DuneCustomAttributeContainer.FromCecil(propertyDefinition, ctx)
        );
    }

    public DuneTypeReference Type => Signature.Type;
    public DuneMethodSignature? GetMethod => Signature.GetMethod;
    public DuneMethodSignature? SetMethod => Signature.SetMethod;

    internal DunePropertyDefinition(DunePropertySignature signature, DuneCustomAttributeContainer customAttributes) : base(signature, customAttributes) {
    }
}