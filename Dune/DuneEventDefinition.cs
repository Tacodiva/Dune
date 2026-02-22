
using System.Reflection;

namespace Dune;

public sealed class DuneEventDefinition : DuneDefinition<DuneEventSignature> {

    public static DuneEventDefinition FromEventInfo(EventInfo eventInfo, DuneReflectionContext? ctx = null) {
        InternalUtils.ThrowIfArgumentNull(eventInfo);
        InternalUtils.Assert(eventInfo.DeclaringType != null);

        ctx ??= new();

        return new(
            DuneEventSignature.FromEventInfo(eventInfo, ctx),
            DuneCustomAttributeContainer.FromMemberInfo(eventInfo, ctx)
        );
    }

    public static DuneEventDefinition FromEventDefinition(CecilEventDefinition eventDefinition, DuneCecilContext? ctx = null) {
        ctx ??= new();

        return new(
            DuneEventSignature.FromEventDefinition(eventDefinition, ctx),
            DuneCustomAttributeContainer.FromCecil(eventDefinition, ctx)
        );
    }

    public DuneTypeReference? Type => Signature.Type;
    public DuneMethodSignature? AddMethod => Signature.AddMethod;
    public DuneMethodSignature? RaiseMethod => Signature.RaiseMethod;
    public DuneMethodSignature? RemoveMethod => Signature.RemoveMethod;

    internal DuneEventDefinition(DuneEventSignature signature, DuneCustomAttributeContainer customAttributes) : base(signature, customAttributes) {
    }
}