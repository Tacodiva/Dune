
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;

namespace Dune;

public sealed class DuneAssemblyDefinition {

    public static DuneAssemblyDefinition FromAssembly(Assembly assembly, DuneReflectionContext? ctx = null) {
        return new(
            DuneAssemblyReference.FromAssembly(assembly, ctx),
            assembly.GetTypes().Select(type => DuneTypeDefinition.FromType(type, ctx)),
            DuneCustomAttributeContainer.FromCustomAttributeData(assembly.GetCustomAttributesData(), ctx)
        );
    }

    public static DuneAssemblyDefinition FromCecilDefinition(CecilAssemblyDefinition assembly, DuneCecilContext? ctx = null) {
        return new(
            DuneAssemblyReference.FromCecilDefinition(assembly, ctx),
            assembly.Modules.SelectMany(module => module.GetTypes()).Select(type => DuneTypeDefinition.FromCecilDefinition(type, ctx)),
            DuneCustomAttributeContainer.FromCecil(assembly, ctx)
        );
    }

    public DuneAssemblyReference Reference { get; }
    public ImmutableArray<DuneTypeDefinition> Types { get; }
    public DuneCustomAttributeContainer CustomAttributes { get; }

    private DuneAssemblyDefinition(DuneAssemblyReference reference, IEnumerable<DuneTypeDefinition> types, DuneCustomAttributeContainer attributes) {
        Reference = reference;
        Types = types.ToImmutableArray();
        CustomAttributes = attributes;
    }

    public DuneTypeDefinition? TryGetTypeDefinition(DuneTypeSignature signature)
        => Types.FirstOrDefault(type => type.Signature == signature);

    public DuneMethodDefinition? TryGetMethodDefinition(DuneMethodSignature signature)
        => TryGetTypeDefinition(signature.DeclaringType!)?.TryGetMethodDefinition(signature);

    public DuneFieldDefinition? TryGetFieldDefinition(DuneFieldSignature signature)
        => TryGetTypeDefinition(signature.DeclaringType!)?.TryGetFieldDefinition(signature);

    public DunePropertyDefinition? TryGetPropertyDefinition(DunePropertySignature signature)
        => TryGetTypeDefinition(signature.DeclaringType!)?.TryGetPropertyDefinition(signature);

    public DuneEventDefinition? TryGetEventDefinition(DuneEventSignature signature)
        => TryGetTypeDefinition(signature.DeclaringType!)?.TryGetEventDefinition(signature);

}