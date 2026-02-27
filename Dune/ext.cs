
using System;
using System.Reflection;
using System.Runtime.Loader;

namespace Dune;


public static class ReflectionExt {

    private static bool IsMatch(Type? type, DuneTypeSignature? signature, DuneReflectionContext? ctx) {
        if (type == null || signature == null)
            return type == null && signature == null;

        if (type.Name != signature.Name) return false;
        if (type.Namespace != signature.Namespace) return false;
        if (type.GenericTypeArguments.Length != signature.GenericParameterCount) return false;

        if (!IsMatch(type.DeclaringType, signature.DeclaringType, ctx))
            return false;

        if (!signature.Assembly.Matches(DuneAssemblyReference.FromAssembly(type.Assembly, ctx)))
            return false;

        return true;
    }

    public static Type? TryGetType(this AssemblyLoadContext assemblyCtx, DuneTypeSignature type, DuneReflectionContext? ctx = null) {
        foreach (Assembly assembly in assemblyCtx.Assemblies) {
            Type? test = TryGetType(assembly, type, ctx);
            if (test != null) return test;
        }
        return null;
    }

    public static Type? TryGetType(this Assembly assembly, DuneTypeSignature type, DuneReflectionContext? ctx = null) {
        if (!type.Assembly.Matches(DuneAssemblyReference.FromAssembly(assembly, ctx)))
            return null;

        foreach (Type test in assembly.GetTypes()) {
            if (IsMatch(test, type, ctx))
                return test;
        }

        return null;
    }

    private static bool IsMatch(MethodBase? methodBase, DuneMethodSignature? signature, DuneReflectionContext? ctx) {
        if (methodBase == null || signature == null)
            return methodBase == null && signature == null;

        if (methodBase.Name != signature.Name)
            return false;

        ParameterInfo[] parameters = methodBase.GetParameters();

        if (parameters.Length != signature.Parameters.Length)
            return false;

        if (methodBase is MethodInfo methodInfo) {
            InternalUtils.Assert(!signature.IsConstructor);

            if (methodInfo.GetGenericArguments().Length != signature.GenericParameterCount)
                return false;

            if (DuneTypeReference.FromType(methodInfo.ReturnType, ctx) != signature.ReturnType)
                return false;
        } else {
            InternalUtils.Assert(methodBase is ConstructorInfo && signature.IsConstructor);
        }

        for (int i = 0; i < parameters.Length; i++) {
            if (DuneTypeReference.FromType(parameters[i].ParameterType, ctx) != signature.Parameters[i].Type)
                return false;
        }

        return true;
    }

    public static MethodBase? TryGetMethod(this AssemblyLoadContext assemblyCtx, DuneMethodSignature method, DuneReflectionContext? ctx = null) {
        foreach (Assembly assembly in assemblyCtx.Assemblies) {
            MethodBase? test = TryGetMethod(assembly, method, ctx);
            if (test != null) return test;
        }
        return null;
    }

    public static MethodBase? TryGetMethod(this Assembly assembly, DuneMethodSignature method, DuneReflectionContext? ctx = null) {
        Type? declaringType = TryGetType(assembly, method.DeclaringType, ctx);
        if (declaringType == null) return null;

        if (method.IsConstructor) {
            foreach (ConstructorInfo test in declaringType.GetConstructors(DuneReflectionContext.EverythingFlags)) {
                if (IsMatch(test, method, ctx))
                    return test;
            }
        } else {
            foreach (MethodInfo test in declaringType.GetMethods(DuneReflectionContext.EverythingFlags)) {
                if (IsMatch(test, method, ctx))
                    return test;
            }
        }

        return null;
    }

    public static FieldInfo? TryGetField(this AssemblyLoadContext assemblyCtx, DuneFieldSignature field, DuneReflectionContext? ctx = null) {
        foreach (Assembly assembly in assemblyCtx.Assemblies) {
            FieldInfo? test = TryGetField(assembly, field, ctx);
            if (test != null) return test;
        }
        return null;
    }

    public static FieldInfo? TryGetField(this Assembly assembly, DuneFieldSignature field, DuneReflectionContext? ctx = null) {
        Type? declaringType = TryGetType(assembly, field.DeclaringType, ctx);
        if (declaringType == null) return null;

        foreach (FieldInfo test in declaringType.GetFields(DuneReflectionContext.EverythingFlags)) {
            if (test.Name != field.Name) continue;

            if (DuneTypeReference.FromType(test.FieldType, ctx) != field.Type)
                continue;
            return test;
        }

        return null;
    }

    public static PropertyInfo? TryGetProperty(this AssemblyLoadContext assemblyCtx, DunePropertySignature property, DuneReflectionContext? ctx = null) {
        foreach (Assembly assembly in assemblyCtx.Assemblies) {
            PropertyInfo? test = TryGetProperty(assembly, property, ctx);
            if (test != null) return test;
        }
        return null;
    }

    public static PropertyInfo? TryGetProperty(this Assembly assembly, DunePropertySignature property, DuneReflectionContext? ctx = null) {
        Type? declaringType = TryGetType(assembly, property.DeclaringType, ctx);
        if (declaringType == null) return null;

        foreach (PropertyInfo test in declaringType.GetProperties(DuneReflectionContext.EverythingFlags)) {
            if (test.Name != property.Name) continue;

            if (!IsMatch(test.GetMethod, property.GetMethod, ctx)) continue;
            if (!IsMatch(test.SetMethod, property.SetMethod, ctx)) continue;

            return test;
        }

        return null;
    }

    public static EventInfo? TryGetEvent(this AssemblyLoadContext assemblyCtx, DuneEventSignature @event, DuneReflectionContext? ctx = null) {
        foreach (Assembly assembly in assemblyCtx.Assemblies) {
            EventInfo? test = TryGetEvent(assembly, @event, ctx);
            if (test != null) return test;
        }
        return null;
    }

    public static EventInfo? TryGetEvent(this Assembly assembly, DuneEventSignature @event, DuneReflectionContext? ctx = null) {
        Type? declaringType = TryGetType(assembly, @event.DeclaringType, ctx);
        if (declaringType == null) return null;

        foreach (EventInfo test in declaringType.GetEvents(DuneReflectionContext.EverythingFlags)) {
            if (test.Name != @event.Name) continue;

            if (!IsMatch(test.AddMethod, @event.AddMethod, ctx)) continue;
            if (!IsMatch(test.RaiseMethod, @event.RaiseMethod, ctx)) continue;
            if (!IsMatch(test.RemoveMethod, @event.RemoveMethod, ctx)) continue;

            return test;
        }

        return null;
    }
}

public static class CecilExt {

    public static CecilTypeDefinition? TryGetTypeDefinition(this CecilAssemblyDefinition assembly, DuneTypeSignature type, DuneCecilContext? ctx = null) {
        foreach (CecilModuleDefinition module in assembly.Modules) {
            CecilTypeDefinition? found = TryGetTypeDefinition(module, type, ctx);
            if (found != null) return found;
        }
        return null;
    }

    private static bool IsMatch(CecilTypeDefinition definition, DuneTypeSignature signature, DuneCecilContext? ctx) {
        if (definition.Name != signature.RawName)
            return false;

        string? signatureNamespace = signature.HasDeclaringType ? signature.Namespace : null;
        string? definitionNamespace = string.IsNullOrWhiteSpace(definition.Namespace) ? null : definition.Namespace;

        if (signatureNamespace != definitionNamespace)
            return false;

        if (definition.GenericParameters.Count != signature.GenericParameterCount)
            return false;

        if (!signature.Assembly.Matches(DuneAssemblyReference.FromCecilDefinition(definition.Module.Assembly, ctx)))
            return false;

        return true;
    }

    public static CecilTypeDefinition? TryGetTypeDefinition(this CecilModuleDefinition module, DuneTypeSignature type, DuneCecilContext? ctx = null) {

        if (type.HasDeclaringType) {
            // If the type has a declaring type, we can find it inside the declaring type.
            CecilTypeDefinition? declaringType = TryGetTypeDefinition(module, type.DeclaringType, ctx);

            if (declaringType == null)
                return null;

            foreach (CecilTypeDefinition test in declaringType.NestedTypes) {
                if (IsMatch(test, type, ctx))
                    return test;
            }

            return null;
        } else {
            // Otherwise, it should be in the module's root.
            foreach (CecilTypeDefinition test in module.Types) {
                if (IsMatch(test, type, ctx))
                    return test;
            }

            return null;
        }
    }

    private static bool IsMatch(CecilMethodDefinition? definition, DuneMethodSignature? signature, DuneCecilContext? ctx) {
        if (definition == null || signature == null)
            return definition == null && signature == null;

        if (definition.Name != signature.Name)
            return false;

        if (definition.GenericParameters.Count != signature.GenericParameterCount)
            return false;

        if (definition.Parameters.Count != signature.Parameters.Length)
            return false;

        if (DuneTypeReference.FromCecilReference(definition.ReturnType, ctx) != signature.ReturnType)
            return false;

        for (int i = 0; i < definition.Parameters.Count; i++) {
            if (DuneTypeReference.FromCecilReference(definition.Parameters[i].ParameterType, ctx) != signature.Parameters[i].Type)
                return false;
        }

        return true;
    }

    public static CecilMethodDefinition? TryGetMethodDefinition(this CecilAssemblyDefinition assembly, DuneMethodSignature method, DuneCecilContext? ctx = null) {
        foreach (CecilModuleDefinition module in assembly.Modules) {
            CecilMethodDefinition? found = TryGetMethodDefinition(module, method, ctx);
            if (found != null) return found;
        }
        return null;
    }

    public static CecilMethodDefinition? TryGetMethodDefinition(this CecilModuleDefinition module, DuneMethodSignature method, DuneCecilContext? ctx = null) {
        CecilTypeDefinition? declaringType = TryGetTypeDefinition(module, method.DeclaringType, ctx);
        if (declaringType == null) return null;

        foreach (CecilMethodDefinition test in declaringType.Methods) {
            if (IsMatch(test, method, ctx))
                return test;
        }

        return null;
    }

    public static CecilFieldDefinition? TryGetFieldDefinition(this CecilAssemblyDefinition assembly, DuneFieldSignature field, DuneCecilContext? ctx = null) {
        foreach (CecilModuleDefinition module in assembly.Modules) {
            CecilFieldDefinition? found = TryGetFieldDefinition(module, field, ctx);
            if (found != null) return found;
        }
        return null;
    }

    public static CecilFieldDefinition? TryGetFieldDefinition(this CecilModuleDefinition module, DuneFieldSignature field, DuneCecilContext? ctx = null) {
        CecilTypeDefinition? declaringType = TryGetTypeDefinition(module, field.DeclaringType, ctx);
        if (declaringType == null) return null;

        foreach (CecilFieldDefinition test in declaringType.Fields) {
            if (test.Name != field.Name) continue;

            if (DuneTypeReference.FromCecilReference(test.FieldType, ctx) != field.Type)
                continue;

            return test;
        }

        return null;
    }

    public static CecilPropertyDefinition? TryGetPropertyDefinition(this CecilAssemblyDefinition assembly, DunePropertySignature property, DuneCecilContext? ctx = null) {
        foreach (CecilModuleDefinition module in assembly.Modules) {
            CecilPropertyDefinition? found = TryGetPropertyDefinition(module, property, ctx);
            if (found != null) return found;
        }
        return null;
    }

    public static CecilPropertyDefinition? TryGetPropertyDefinition(this CecilModuleDefinition module, DunePropertySignature property, DuneCecilContext? ctx = null) {
        CecilTypeDefinition? declaringType = TryGetTypeDefinition(module, property.DeclaringType, ctx);
        if (declaringType == null) return null;

        foreach (CecilPropertyDefinition test in declaringType.Properties) {
            if (test.Name != property.Name) continue;

            if (!IsMatch(test.GetMethod, property.GetMethod, ctx))
                continue;

            if (!IsMatch(test.SetMethod, property.SetMethod, ctx))
                continue;

            return test;
        }

        return null;
    }

    public static CecilEventDefinition? TryGetEventDefinition(this CecilAssemblyDefinition assembly, DuneEventSignature @event, DuneCecilContext? ctx = null) {
        foreach (CecilModuleDefinition module in assembly.Modules) {
            CecilEventDefinition? found = TryGetEventDefinition(module, @event, ctx);
            if (found != null) return found;
        }
        return null;
    }

    public static CecilEventDefinition? TryGetEventDefinition(this CecilModuleDefinition module, DuneEventSignature @event, DuneCecilContext? ctx = null) {
        CecilTypeDefinition? declaringType = TryGetTypeDefinition(module, @event.DeclaringType, ctx);
        if (declaringType == null) return null;

        foreach (CecilEventDefinition test in declaringType.Events) {
            if (test.Name != @event.Name) continue;

            if (!IsMatch(test.AddMethod, @event.AddMethod, ctx))
                continue;

            if (!IsMatch(test.InvokeMethod, @event.RaiseMethod, ctx))
                continue;

            if (!IsMatch(test.RemoveMethod, @event.RemoveMethod, ctx))
                continue;

            return test;
        }

        return null;
    }
}