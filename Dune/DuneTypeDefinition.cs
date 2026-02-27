
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;

namespace Dune;

public sealed record class DuneGenericParameterDefinition(
    string Name,
    DuneCustomAttributeContainer CustomAttributes
);

public sealed class DuneTypeDefinition : DuneDefinition<DuneTypeSignature> {

    public static DuneTypeDefinition FromType<T>(DuneReflectionContext? ctx = null)
#if NET
        where T : allows ref struct
#endif
        => FromType(typeof(T), ctx);

    public static DuneTypeDefinition FromType(Type type, DuneReflectionContext? ctx = null) {
        InternalUtils.ThrowIfArgumentNull(type);

        ctx ??= new();

        type = DuneTypeSignature.GetGenericTypeDefinition(type);

        return new(
            DuneTypeSignature.FromType(type, ctx),
            DuneCustomAttributeContainer.FromMemberInfo(type, ctx),
            type.GetGenericArguments().Select(genericArg => DuneCustomAttributeContainer.FromMemberInfo(genericArg, ctx)),

            type.GetMethods(DuneReflectionContext.EverythingWithinFlags)
                .AsEnumerable<MethodBase>()
                .Concat(type.GetConstructors())
                .Select(method => DuneMethodDefinition.FromMethodBase(method, ctx)),

            type.GetFields(DuneReflectionContext.EverythingWithinFlags)
                .Select(field => DuneFieldDefinition.FromFieldInfo(field, ctx)),

            type.GetProperties(DuneReflectionContext.EverythingWithinFlags)
                .Select(property => DunePropertyDefinition.FromPropertyInfo(property, ctx)),

            type.GetEvents(DuneReflectionContext.EverythingWithinFlags)
                .Select(@event => DuneEventDefinition.FromEventInfo(@event, ctx))
        );
    }

    public static DuneTypeDefinition FromCecilDefinition(CecilTypeDefinition type, DuneCecilContext? ctx = null) {
        InternalUtils.ThrowIfArgumentNull(type);

        ctx ??= new();

        return new(
            DuneTypeSignature.FromCecilDefinition(type, ctx),
            DuneCustomAttributeContainer.FromCecil(type, ctx),
            type.GenericParameters.Select(genericArg => DuneCustomAttributeContainer.FromCecil(genericArg, ctx)),

            type.Methods
                .Select(method => DuneMethodDefinition.FromCecilDefinition(method, ctx)),

            type.Fields
                .Select(field => DuneFieldDefinition.FromCecilDefinition(field, ctx)),

            type.Properties
                .Select(property => DunePropertyDefinition.FromCecilDefinition(property, ctx)),

            type.Events
                .Select(@event => DuneEventDefinition.FromCecilDefinition(@event, ctx))
        );
    }

    public ImmutableArray<DuneMethodDefinition> Methods { get; }
    public ImmutableArray<DuneFieldDefinition> Fields { get; }
    public ImmutableArray<DunePropertyDefinition> Properties { get; }
    public ImmutableArray<DuneEventDefinition> Events { get; }

    public string? Namespace => Signature.Namespace;
    public DuneTypeSignature? DeclaringType => Signature.DeclaringType;
    public ImmutableArray<DuneGenericParameterDefinition> GenericParameters { get; }

    internal DuneTypeDefinition(
        DuneTypeSignature signature,
        DuneCustomAttributeContainer customAttributes,
        IEnumerable<DuneCustomAttributeContainer> genericParameterAttributes,
        IEnumerable<DuneMethodDefinition> methods,
        IEnumerable<DuneFieldDefinition> fields,
        IEnumerable<DunePropertyDefinition> properties,
        IEnumerable<DuneEventDefinition> events
    ) : base(signature, customAttributes) {

        GenericParameters = genericParameterAttributes.Select(
            (paramAttributes, i) => new DuneGenericParameterDefinition(signature.GenericParameterNames[i], paramAttributes)
        ).ToImmutableArray();

        Methods = methods.ToImmutableArray();
        Fields = fields.ToImmutableArray();
        Properties = properties.ToImmutableArray();
        Events = events.ToImmutableArray();

        InternalUtils.Assert(GenericParameters.Length == signature.GenericParameterNames.Length);
    }

    public DuneMethodDefinition? TryGetMethodDefinition(DuneMethodSignature signature) {
        if (signature.DeclaringType != Signature) return null;
        return Methods.FirstOrDefault(method => method.Signature == signature);        
    }

    public DuneFieldDefinition? TryGetFieldDefinition(DuneFieldSignature signature) {
        if (signature.DeclaringType != Signature) return null;
        return Fields.FirstOrDefault(field => field.Signature == signature);        
    }

    public DunePropertyDefinition? TryGetPropertyDefinition(DunePropertySignature signature) {
        if (signature.DeclaringType != Signature) return null;
        return Properties.FirstOrDefault(property => property.Signature == signature);        
    }

    public DuneEventDefinition? TryGetEventDefinition(DuneEventSignature signature) {
        if (signature.DeclaringType != Signature) return null;
        return Events.FirstOrDefault(@event => @event.Signature == signature);        
    }
}