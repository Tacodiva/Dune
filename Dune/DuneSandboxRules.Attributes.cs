
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Dune.Attributes;

namespace Dune;

partial class DuneSandboxRules {

    public record AnnotationParseSettings(
        ulong Mask = DuneAllowAttribute.DefaultMask
    );

    public DuneSandboxAssemblyRules AllowAnnotatedAssembly(Assembly assembly, AnnotationParseSettings? settings = null, DuneReflectionContext? ctx = null)
        => AllowAnnotatedAssembly(DuneAssemblyDefinition.FromAssembly(assembly, ctx ?? DefaultReflectionCtx), settings);

    public DuneSandboxAssemblyRules AllowAnnotatedAssembly(CecilAssemblyDefinition assemblyDefinition, AnnotationParseSettings? settings = null, DuneCecilContext? ctx = null)
        => AllowAnnotatedAssembly(DuneAssemblyDefinition.FromCecilDefinition(assemblyDefinition, ctx), settings);

    public DuneSandboxAssemblyRules AllowAnnotatedAssembly(DuneAssembly assembly, AnnotationParseSettings? settings = null)
        => AllowAnnotatedAssembly(assembly.GetDefinition(), settings);

    private record struct AttributeInfo(
        bool HasDenyAttribute,
        bool HasAllowAttribute,
        bool HasRecursiveAllowAttribute
    ) {

        public readonly bool IsAllowed => !HasDenyAttribute && HasAllowAttribute;

        public readonly AttributeInfo WithinParent(AttributeInfo parent) {
            AttributeInfo newInfo = this;

            if (parent.HasDenyAttribute) {
                // If a declaring type is denied, we should be denied as well
                newInfo.HasDenyAttribute = true;
            }

            if (parent.HasRecursiveAllowAttribute) {
                // If a parent is recursivley allowed, we should be recursively allowed as well
                // *unless* we have already specified an allow attribute without recursion
                if (!newInfo.HasAllowAttribute) {
                    newInfo.HasAllowAttribute = true;
                    newInfo.HasRecursiveAllowAttribute = true;
                }
            }

            return newInfo;
        }
    }

    public DuneSandboxAssemblyRules AllowAnnotatedAssembly(DuneAssemblyDefinition assemblyDefinition, AnnotationParseSettings? settings = null) {

        settings ??= new();

        AttributeInfo GetAttributeInfo(IDuneCustomAttributeDefinition definition) {
            IEnumerable<DuneAllowAttribute> typeAllowAttributes = definition.GetCustomAttributes<DuneAllowAttribute>(DefaultReflectionCtx)
                .Where(attribute => (attribute.Mask & settings.Mask) == settings.Mask);

            IEnumerable<DuneDenyAttribute> typeDenyAttributes = definition.GetCustomAttributes<DuneDenyAttribute>(DefaultReflectionCtx)
                .Where(attribute => (attribute.Mask & settings.Mask) == settings.Mask);

            return new() {
                HasDenyAttribute = typeDenyAttributes.Any(),
                HasAllowAttribute = typeAllowAttributes.Any(),
                HasRecursiveAllowAttribute = typeAllowAttributes.Any(attribute => attribute.IsRecursive)
            };
        }

        DuneSandboxAssemblyRules assemblyRules = GetAssemblyRules(assemblyDefinition.Reference);
        AttributeInfo assemblyAttributes = GetAttributeInfo(assemblyDefinition);

        if (assemblyAttributes.HasDenyAttribute) {
            // If the assembly has a deny attribute, nothing will be allowed
            assemblyRules.Allow = false;
        } else {
            assemblyRules.Allow = true;

            // Step 1. Collect all the types and information on their attributes into a big list
            Dictionary<DuneTypeSignature, (AttributeInfo, DuneTypeDefinition)> assemblyTypes = [];
            foreach (DuneTypeDefinition type in assemblyDefinition.Types) {
                assemblyTypes[type.Signature] = (GetAttributeInfo(type), type);
            }


            foreach (KeyValuePair<DuneTypeSignature, (AttributeInfo, DuneTypeDefinition)> entry in assemblyTypes) {

                DuneTypeSignature typeSignature = entry.Key;
                AttributeInfo typeAttributeInfo = entry.Value.Item1;
                DuneTypeDefinition typeDefinition = entry.Value.Item2;

                // Step 2. Propagate rules into child types

                {
                    DuneTypeSignature? declaringType = typeSignature.DeclaringType;

                    while (declaringType != null) {
                        AttributeInfo declaringTypeInfo = assemblyTypes[declaringType].Item1;
                        typeAttributeInfo = typeAttributeInfo.WithinParent(declaringTypeInfo);
                        declaringType = declaringType.DeclaringType;
                    }

                    typeAttributeInfo = typeAttributeInfo.WithinParent(assemblyAttributes);

                    // The assembly "IsAllowed" is special, it sets all types as allowed by default,
                    //   but not their members. We apply that here 
                    if (assemblyAttributes.HasAllowAttribute) {
                        typeAttributeInfo.HasAllowAttribute = true;
                    }
                }


                // Step 3. Iterate all members and allow stuff

                if (typeAttributeInfo.IsAllowed) {
                    DuneSandboxTypeRules typeRules = assemblyRules.AllowType(typeSignature, false);

                    foreach (DuneMethodDefinition method in typeDefinition.Methods) {
                        if (GetAttributeInfo(method).WithinParent(typeAttributeInfo).IsAllowed)
                            typeRules.AllowMethod(method.Signature);
                    }

                    foreach (DuneFieldDefinition field in typeDefinition.Fields) {
                        if (GetAttributeInfo(field).WithinParent(typeAttributeInfo).IsAllowed)
                            typeRules.AllowField(field.Signature);
                    }

                    foreach (DunePropertyDefinition property in typeDefinition.Properties) {
                        if (GetAttributeInfo(property).WithinParent(typeAttributeInfo).IsAllowed) {
                            if (property.Signature.GetMethod != null)
                                typeRules.AllowMethod(property.Signature.GetMethod);

                            if (property.Signature.SetMethod != null)
                                typeRules.AllowMethod(property.Signature.SetMethod);
                        }
                    }

                    foreach (DuneEventDefinition @event in typeDefinition.Events) {
                        if (GetAttributeInfo(@event).WithinParent(typeAttributeInfo).IsAllowed) {
                            if (@event.Signature.AddMethod != null)
                                typeRules.AllowMethod(@event.Signature.AddMethod);

                            if (@event.Signature.RaiseMethod != null)
                                typeRules.AllowMethod(@event.Signature.RaiseMethod);

                            if (@event.Signature.RemoveMethod != null)
                                typeRules.AllowMethod(@event.Signature.RemoveMethod);
                        }
                    }
                }

            }
        }

        return assemblyRules;
    }
}

