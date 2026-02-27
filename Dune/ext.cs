
using System;
using System.Reflection;
using System.Runtime.Loader;

namespace Dune;


public static class ReflectionExt {

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
            if (type.Matches(test, ctx))
                return test;
        }

        return null;
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
        return TryGetMethod(declaringType, method, ctx);
    }

    public static MethodBase? TryGetMethod(this Type declaringType, DuneMethodSignature method, DuneReflectionContext? ctx = null) {
        if (method.IsConstructor) {
            foreach (ConstructorInfo test in declaringType.GetConstructors(DuneReflectionContext.EverythingWithinFlags)) {
                if (method.Matches(test, ctx))
                    return test;
            }
        } else {
            foreach (MethodInfo test in declaringType.GetMethods(DuneReflectionContext.EverythingWithinFlags)) {
                if (method.Matches(test, ctx))
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
        return TryGetField(declaringType, field, ctx);
    }

    public static FieldInfo? TryGetField(this Type declaringType, DuneFieldSignature field, DuneReflectionContext? ctx = null) {
        foreach (FieldInfo test in declaringType.GetFields(DuneReflectionContext.EverythingWithinFlags)) {
            if (field.Matches(test, ctx))
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
        return TryGetProperty(declaringType, property, ctx);
    }

    public static PropertyInfo? TryGetProperty(this Type declaringType, DunePropertySignature property, DuneReflectionContext? ctx = null) {
        foreach (PropertyInfo test in declaringType.GetProperties(DuneReflectionContext.EverythingWithinFlags)) {
            if (property.Matches(test, ctx))
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
        return TryGetEvent(declaringType, @event, ctx);
    }

    public static EventInfo? TryGetEvent(this Type declaringType, DuneEventSignature @event, DuneReflectionContext? ctx = null) {
        foreach (EventInfo test in declaringType.GetEvents(DuneReflectionContext.EverythingWithinFlags)) {
            if (@event.Matches(test, ctx))
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

    public static CecilTypeDefinition? TryGetTypeDefinition(this CecilModuleDefinition module, DuneTypeSignature type, DuneCecilContext? ctx = null) {

        if (type.HasDeclaringType) {
            // If the type has a declaring type, we can find it inside the declaring type.
            CecilTypeDefinition? declaringType = TryGetTypeDefinition(module, type.DeclaringType, ctx);

            if (declaringType == null)
                return null;

            foreach (CecilTypeDefinition test in declaringType.NestedTypes) {
                if (type.Matches(test, ctx))
                    return test;
            }

            return null;
        } else {
            // Otherwise, it should be in the module's root.
            foreach (CecilTypeDefinition test in module.Types) {
                if (type.Matches(test, ctx))
                    return test;
            }

            return null;
        }
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
        return TryGetMethodDefinition(declaringType, method, ctx);
    }

    public static CecilMethodDefinition? TryGetMethodDefinition(this CecilTypeDefinition declaringType, DuneMethodSignature method, DuneCecilContext? ctx = null) {
        foreach (CecilMethodDefinition test in declaringType.Methods) {
            if (method.Matches(test, ctx))
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
        return TryGetFieldDefinition(declaringType, field, ctx);
    }

    public static CecilFieldDefinition? TryGetFieldDefinition(this CecilTypeDefinition declaringType, DuneFieldSignature field, DuneCecilContext? ctx = null) {
        foreach (CecilFieldDefinition test in declaringType.Fields) {
            if (field.Matches(test, ctx))
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
        return TryGetPropertyDefinition(declaringType, property, ctx);
    }

    public static CecilPropertyDefinition? TryGetPropertyDefinition(this CecilTypeDefinition declaringType, DunePropertySignature property, DuneCecilContext? ctx = null) {
        foreach (CecilPropertyDefinition test in declaringType.Properties) {
            if (property.Matches(test, ctx))
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
        return TryGetEventDefinition(declaringType, @event, ctx);
    }

    public static CecilEventDefinition? TryGetEventDefinition(this CecilTypeDefinition declaringType, DuneEventSignature @event, DuneCecilContext? ctx = null) {
        foreach (CecilEventDefinition test in declaringType.Events) {
            if (@event.Matches(test, ctx))
                return test;
        }
        return null;
    }
}