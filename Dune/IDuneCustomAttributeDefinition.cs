
using System;
using System.Collections.Generic;

namespace Dune;

public interface IDuneCustomAttributeDefinition {

    public DuneCustomAttributeContainer CustomAttributes { get; }

}

public static class DuneCustomAttributeDefinitionExt {
    public static T? GetCustomAttribute<T>(this IDuneCustomAttributeDefinition definition, DuneReflectionContext? ctx = null) where T : Attribute
        => definition.CustomAttributes.TryGetAttribute<T>(ctx);

    public static IEnumerable<T> GetCustomAttributes<T>(this IDuneCustomAttributeDefinition definition, DuneReflectionContext? ctx = null) where T : Attribute
        => definition.CustomAttributes.GetAttributes<T>(ctx);

    public static IEnumerable<Attribute> GetCustomAttributes(this IDuneCustomAttributeDefinition definition, Type attributeType, DuneReflectionContext? ctx = null)
        => definition.CustomAttributes.GetAttributes(attributeType, ctx);

    public static DuneCustomAttributeDefinition? GetCustomAttribute(this IDuneCustomAttributeDefinition definition, DuneTypeSignature attributeType)
        => definition.GetCustomAttribute(attributeType);

    public static IEnumerable<DuneCustomAttributeDefinition> GetCustomAttributes(this IDuneCustomAttributeDefinition definition, DuneTypeSignature attributeType)
        => definition.GetCustomAttributes(attributeType);
}