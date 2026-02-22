
namespace Dune;

public abstract class DuneDefinition<TSignature> where TSignature : IDuneMemberSignature {

    public TSignature Signature { get; }
    public DuneCustomAttributeContainer CustomAttributes { get; }

    public DuneAssemblyReference Assembly => Signature.Assembly;
    public string Name => Signature.Name;

    protected DuneDefinition(TSignature signature, DuneCustomAttributeContainer customAttributes) {
        Signature = signature;
        CustomAttributes = customAttributes;
    }
}