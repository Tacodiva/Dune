
namespace Dune;

public abstract class DuneDefinition<TSignature>(TSignature signature, DuneCustomAttributeContainer customAttributes)
    where TSignature : IDuneMemberSignature {

    public TSignature Signature { get; } = signature;
    public DuneCustomAttributeContainer CustomAttributes { get; } = customAttributes;

    public DuneAssemblyReference Assembly => Signature.Assembly;
    public string Name => Signature.Name;

    public override string ToString() => Signature?.ToString() ?? "null";
}