
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace Dune;


public sealed class DuneSandboxAssemblyRules {

    public DuneAssemblyReference Assembly { get; set; }
    public bool Allow { get; set; }

    internal readonly Dictionary<DuneTypeSignature, DuneSandboxTypeRules> _types = [];

    internal DuneSandboxAssemblyRules(DuneAssemblyReference assembly) {
        Assembly = assembly;
        Allow = false;
    }

    public DuneSandboxTypeRules? TryGetTypeRules(DuneTypeSignature type) {
        _types.TryGetValue(type, out DuneSandboxTypeRules? rules);
        return rules;
    }

    public DuneSandboxTypeRules GetTypeRules(DuneTypeSignature type) {
        if (!_types.TryGetValue(type, out DuneSandboxTypeRules? rules)) {
            rules = _types[type] = new(this, type);
        }

        return rules;
    }

    public bool IsTypeAllowed(DuneTypeSignature type) {
        return TryGetTypeRules(type)?.Allow ?? false;
    }

    public bool IsMethodAllowed(DuneMethodSignature method) {
        if (method.DeclaringType == null) return false;
        return TryGetTypeRules(method.DeclaringType)?.IsMethodAllowed(method) ?? false;
    }

    public bool IsFieldAllowed(DuneFieldSignature field) {
        if (field.DeclaringType == null) return false;
        return TryGetTypeRules(field.DeclaringType)?.IsFieldAllowed(field) ?? false;
    }

    public DuneSandboxTypeRules AllowType(DuneTypeSignature type, bool recurseDeclaringTypes = true) {
        DuneSandboxTypeRules baseRules = GetTypeRules(type);
        baseRules.Allow = true;

        if (recurseDeclaringTypes && type.DeclaringType != null)
            AllowType(type.DeclaringType, true);

        return baseRules;
    }

    public void BlockType(DuneTypeSignature type) {
        DuneSandboxTypeRules? rules = TryGetTypeRules(type);
        if (rules != null) rules.Allow = false;
    }

}

public sealed class DuneSandboxTypeRules {

    public DuneSandboxAssemblyRules AssemblyRules { get; }
    public DuneTypeSignature Type { get; }

    public bool Allow { get; set; }

    internal readonly HashSet<DuneMethodSignature> _allowedMethods = [];
    internal readonly HashSet<DuneFieldSignature> _allowedFields = [];

    internal DuneSandboxTypeRules(DuneSandboxAssemblyRules assemblyRules, DuneTypeSignature type) {
        AssemblyRules = assemblyRules;
        Type = type;
        Allow = false;
    }

    public bool IsMethodAllowed(DuneMethodSignature method) {
        return _allowedMethods.Contains(method);
    }

    public bool IsFieldAllowed(DuneFieldSignature field) {
        return _allowedFields.Contains(field);
    }

    public DuneSandboxTypeRules AllowMethod(DuneMethodSignature methodDefinition) {
        _allowedMethods.Add(methodDefinition);
        return this;
    }

    public DuneSandboxTypeRules BlockMethod(DuneMethodSignature methodDefinition) {
        _allowedMethods.Remove(methodDefinition);
        return this;
    }

    public DuneSandboxTypeRules AllowField(DuneFieldSignature fieldDefinition) {
        _allowedFields.Add(fieldDefinition);
        return this;
    }

    public DuneSandboxTypeRules BlockField(DuneFieldSignature fieldDefinition) {
        _allowedFields.Remove(fieldDefinition);
        return this;
    }
}

public sealed class DuneSandboxReflectionRuleBuilder {
    public DuneSandboxRules Rules { get; }
    public Type Type { get; }

    internal DuneSandboxReflectionRuleBuilder(DuneSandboxRules rules, Type type) {
        Rules = rules;
        Type = type;
    }


    public DuneSandboxReflectionRuleBuilder AllowConstructor(params Type[] parameters) {
        Rules.AllowConstructor(Type, parameters);
        return this;
    }

    public DuneSandboxReflectionRuleBuilder AllowConstructors() {
        Rules.AllowConstructors(Type);
        return this;
    }

    public DuneSandboxReflectionRuleBuilder AllowMethod(string methodName) {
        Rules.AllowMethod(Type, methodName);
        return this;
    }

    public DuneSandboxReflectionRuleBuilder AllowMethod(string methodName, params Type[] parameters) {
        Rules.AllowMethod(Type, methodName, parameters);
        return this;
    }

    public DuneSandboxReflectionRuleBuilder AllowMethods(string methodName) {
        Rules.AllowMethods(Type, methodName);
        return this;
    }

    public DuneSandboxReflectionRuleBuilder AllowField(string fieldName) {
        Rules.AllowField(Type, fieldName);
        return this;
    }

    public DuneSandboxReflectionRuleBuilder AllowProperty(string propertyName) {
        Rules.AllowProperty(Type, propertyName);
        return this;
    }

    public DuneSandboxReflectionRuleBuilder AllowEvent(string eventName) {
        Rules.AllowEvent(Type, eventName);
        return this;
    }
}

public sealed partial class DuneSandboxRules : IDuneSandboxRules {

    private readonly Dictionary<string, DuneSandboxAssemblyRules> _assemblyRules = [];

    private DuneReflectionContext? _defaultReflectionCtx = null;
    private DuneReflectionContext DefaultReflectionCtx => _defaultReflectionCtx ??= new();

    public bool AllowTypedReferenceKeywords { get; set; } = false;
    public bool AllowDynamicKeyword { get; set; } = false;
    public bool AllowUnsafe { get; set; } = false;
    bool IDuneSandboxRules.CheckInternalReferences => false;

    public DuneSandboxRules(bool initDefault = true) {
        if (initDefault) InitDefaultRules(this);
    }

    public DuneSandboxAssemblyRules? TryGetAssemblyRules(DuneAssemblyReference assembly) {
        if (_assemblyRules.TryGetValue(assembly.Name, out DuneSandboxAssemblyRules? rules)) {
            if (rules.Assembly.Matches(assembly)) return rules;
        }

        return null;
    }

    private DuneSandboxAssemblyRules GetAssemblyRules(DuneAssemblyReference assembly) {
        if (_assemblyRules.TryGetValue(assembly.Name, out DuneSandboxAssemblyRules? rules)) {

            if (!rules.Assembly.Matches(assembly)) {
                // We need to modify the assembly reference in the rule so it matches the assembly we're trying to allow.

                if (rules.Assembly.HasVersion && assembly.HasVersion) {
                    if (rules.Assembly.Version != assembly.Version) {
                        // The version is mismatched, change the rule to be a wildcard.
                        rules.Assembly = rules.Assembly.WithVersion(null);
                    }
                }

                if (rules.Assembly.HasCulture && assembly.HasCulture) {
                    if (rules.Assembly.CultureName != assembly.CultureName) {
                        // The culture is mismatched, change the rule to be a wildcard.
                        rules.Assembly = rules.Assembly.WithCulture(null);
                    }
                }
            }

        } else {
            rules = _assemblyRules[assembly.Name] = new(assembly);
        }

        return rules;
    }

    public bool IsAssemblyAllowed(DuneAssemblyReference assembly) {
        return TryGetAssemblyRules(assembly)?.Allow ?? false;
    }

    bool IDuneSandboxRules.IsTypeAllowed(DuneTypeSignatureReference type)
        => IsTypeAllowed(type.Signature);

    public bool IsTypeAllowed(DuneTypeSignature type) {
        return TryGetAssemblyRules(type.Assembly)?.IsTypeAllowed(type) ?? false;
    }

    bool IDuneSandboxRules.IsMethodAllowed(DuneMethodReference method)
        => IsMethodAllowed(method.Signature);

    public bool IsMethodAllowed(DuneMethodSignature method) {
        return TryGetAssemblyRules(method.Assembly)?.IsMethodAllowed(method) ?? false;
    }

    bool IDuneSandboxRules.IsFieldAllowed(DuneFieldReference field)
        => IsFieldAllowed(field.Signature);

    public bool IsFieldAllowed(DuneFieldSignature field) {
        return TryGetAssemblyRules(field.Assembly)?.IsFieldAllowed(field) ?? false;
    }

    public DuneSandboxAssemblyRules AllowAssembly(DuneAssemblyReference assembly) {
        DuneSandboxAssemblyRules rules = GetAssemblyRules(assembly);
        rules.Allow = true;
        return rules;
    }

    public void BlockAssembly(DuneAssemblyReference assembly) {
        GetAssemblyRules(assembly).Allow = false;
    }

    #region Types

    public DuneSandboxReflectionRuleBuilder AllowType<T>(bool allowDependencies = true, DuneReflectionContext? ctx = null)
        => AllowType(typeof(T), allowDependencies, ctx);

    public DuneSandboxReflectionRuleBuilder AllowType(Type type, bool allowDependencies = true, DuneReflectionContext? ctx = null) {
        AllowType(DuneTypeSignature.FromType(type, ctx ?? DefaultReflectionCtx), allowDependencies);
        return new(this, type);
    }

    public void AllowType(DuneTypeReference typeRef, bool allowDependencies = true) {
        switch (typeRef) {
            case DunePointerTypeReference pointerRef: {
                    if (!AllowUnsafe)
                        throw new InvalidOperationException($"Cannot allow pointer type '{pointerRef.Name}'. To allow pointers, set {nameof(AllowUnsafe)} to true.");

                    if (allowDependencies) AllowType(pointerRef.Element, true);
                    break;
                }

            case DuneFunctionPointerTypeReference funcPointerRef: {
                    if (!AllowUnsafe)
                        throw new InvalidOperationException($"Cannot allow function pointer type '{funcPointerRef.Name}'. To allow pointers, set {nameof(AllowUnsafe)} to true.");

                    if (allowDependencies) {
                        if (funcPointerRef.ReturnType != null) AllowType(funcPointerRef.ReturnType, true);

                        foreach (DuneTypeReference parameter in funcPointerRef.Parameters)
                            AllowType(parameter, true);
                    }

                    break;
                }

            case DuneArrayTypeReference arrayRef: {
                    if (allowDependencies) AllowType(arrayRef.Element, true);
                    break;
                }

            case DuneRefTypeReference refRef: {
                    if (allowDependencies) AllowType(refRef.Element, true);
                    break;
                }

            case DuneUnknownTypeReference:
                break;

            case DuneGenericTypeReference:
                break;

            case DuneTypeSignatureReference defRef: {
                    AllowType(defRef.Signature, allowDependencies);

                    if (allowDependencies) {
                        foreach (DuneTypeReference genericArg in defRef.GenericArguments)
                            AllowType(genericArg, true);
                    }

                    break;
                }

            default:
                Debug.Fail($"Unhandled type reference {typeRef.GetType()}.");
                break;
        }
    }

    public DuneSandboxTypeRules AllowType(DuneTypeSignature type, bool allowDependencies = true) {
        if (allowDependencies) {
            return AllowAssembly(type.Assembly).AllowType(type);
        } else {
            return GetAssemblyRules(type.Assembly).AllowType(type);
        }
    }

    public void BlockType<T>(DuneReflectionContext? ctx = null)
        => BlockType(DuneTypeSignature.FromType<T>(ctx ?? DefaultReflectionCtx));

    public void BlockType(Type type, DuneReflectionContext? ctx = null)
        => BlockType(DuneTypeSignature.FromType(type, ctx ?? DefaultReflectionCtx));

    public void BlockType(DuneTypeSignature type) {
        TryGetAssemblyRules(type.Assembly)?.BlockType(type);
    }
    #endregion

    #region Methods

    public void AllowMethod<T>(string methodName, bool allowDependencies = true, DuneReflectionContext? ctx = null)
        => AllowMethodsByName(typeof(T), methodName, allowDependencies, ctx, false);

    public void AllowMethods<T>(string methodName, bool allowDependencies = true, DuneReflectionContext? ctx = null)
        => AllowMethodsByName(typeof(T), methodName, allowDependencies, ctx, true);

    public void AllowMethod(Type type, string methodName, bool allowDependencies = true, DuneReflectionContext? ctx = null)
        => AllowMethodsByName(type, methodName, allowDependencies, ctx, false);

    public void AllowMethods(Type type, string methodName, bool allowDependencies = true, DuneReflectionContext? ctx = null)
        => AllowMethodsByName(type, methodName, allowDependencies, ctx, true);

    public void AllowConstructors<T>(bool allowDependencies = true, DuneReflectionContext? ctx = null)
        => AllowMethodsByName(typeof(T), ".ctor", allowDependencies, ctx, true);

    public void AllowConstructors(Type type, bool allowDependencies = true, DuneReflectionContext? ctx = null)
        => AllowMethodsByName(type, ".ctor", allowDependencies, ctx, true);

    private void AllowMethodsByName(Type type, string methodName, bool allowDependencies, DuneReflectionContext? ctx, bool allowMultiple) {
        MethodBase[] selectedMethods;

        if (methodName == ".ctor") {
            selectedMethods = type.GetConstructors(DuneReflectionContext.EverythingPublicFlags);
        } else {
            selectedMethods = [.. type.GetMethods(DuneReflectionContext.EverythingPublicFlags).Where(method => method.Name == methodName)];
        }

        if (selectedMethods.Length == 0)
            throw new InvalidOperationException($"No methods named '{methodName}' found in type '{type}'.");

        if (!allowMultiple && selectedMethods.Length > 1)
            throw new InvalidOperationException($"Multiple methods named '{methodName}' found in type '{type}'. To allow all of them, use AllowMethods instead. To use only one of them, specify the parameters.");

        foreach (MethodBase method in selectedMethods)
            AllowMethod(method, allowDependencies, ctx);
    }

    public void AllowConstructor<T>(Type[] parameters, bool allowDependencies = true, DuneReflectionContext? ctx = null)
        => AllowMethod<T>(".ctor", parameters, allowDependencies, ctx);

    public void AllowConstructor(Type type, Type[] parameters, bool allowDependencies = true, DuneReflectionContext? ctx = null)
        => AllowMethod(type, ".ctor", parameters, allowDependencies, ctx);

    public void AllowMethod<T>(string methodName, Type[] parameters, bool allowDependencies = true, DuneReflectionContext? ctx = null)
        => AllowMethod(typeof(T), methodName, parameters, allowDependencies, ctx);

    public void AllowMethod(Type type, string methodName, Type[] parameters, bool allowDependencies = true, DuneReflectionContext? ctx = null) {
        MethodBase? method;

        if (methodName == ".ctor") {
            method = type.GetConstructor(DuneReflectionContext.EverythingPublicFlags, null, parameters, []);
        } else {
            method = type.GetMethod(methodName, DuneReflectionContext.EverythingPublicFlags, null, parameters, []);
        }

        if (method == null)
            throw new InvalidOperationException($"No method named '{methodName}' found in type '{type}' with provided parameters.");

        AllowMethod(method, allowDependencies, ctx);
    }

    public void AllowMethod(Delegate method, bool allowDependencies = true, DuneReflectionContext? ctx = null)
        => AllowMethod(DuneMethodSignature.FromDelegate(method, ctx ?? DefaultReflectionCtx), allowDependencies);

    public void AllowMethod(MethodBase method, bool allowDependencies = true, DuneReflectionContext? ctx = null)
        => AllowMethod(DuneMethodSignature.FromMethodBase(method, ctx ?? DefaultReflectionCtx), allowDependencies);

    public void AllowMethod(DuneMethodSignature method, bool allowDependencies = true) {
        if (method.DeclaringType == null)
            throw new NotSupportedException("Methods without a declaring type are not supported.");

        if (allowDependencies) {
            AllowAssembly(method.Assembly).AllowType(method.DeclaringType, true).AllowMethod(method);
        } else {
            GetAssemblyRules(method.Assembly).GetTypeRules(method.DeclaringType).AllowMethod(method);
        }
    }

    public void BlockMethod(DuneMethodSignature method) {
        if (method.DeclaringType == null)
            throw new NotSupportedException("Methods without a declaring type are not supported.");

        TryGetAssemblyRules(method.Assembly)?.TryGetTypeRules(method.DeclaringType)?.BlockMethod(method);
    }

    #endregion Methods

    #region Fields

    public void AllowField<T>(string fieldName, bool allowDependencies = true, DuneReflectionContext? ctx = null)
        => AllowField(DuneFieldSignature.FromType<T>(fieldName, ctx), allowDependencies);

    public void AllowField(Type type, string fieldName, bool allowDependencies = true, DuneReflectionContext? ctx = null)
        => AllowField(DuneFieldSignature.FromType(type, fieldName, ctx), allowDependencies);

    public void AllowField(FieldInfo field, bool allowDependencies = true, DuneReflectionContext? ctx = null)
        => AllowField(DuneFieldSignature.FromFieldInfo(field, ctx), allowDependencies);

    public void AllowField(DuneFieldSignature field, bool allowDependencies = true) {
        if (field.DeclaringType == null)
            throw new NotSupportedException("Fields without a declaring type are not supported.");

        if (allowDependencies) {
            AllowAssembly(field.Assembly).AllowType(field.DeclaringType, true).AllowField(field);
        } else {
            GetAssemblyRules(field.Assembly).GetTypeRules(field.DeclaringType).AllowField(field);
        }
    }

    public void BlockField(DuneFieldSignature field) {
        if (field.DeclaringType == null)
            throw new NotSupportedException("Fields without a declaring type are not supported.");

        TryGetAssemblyRules(field.Assembly)?.TryGetTypeRules(field.DeclaringType)?.BlockField(field);
    }

    #endregion Fields

    #region Properties    

    public void AllowProperty<T>(string propertyName, bool allowDependencies = true, DuneReflectionContext? ctx = null)
        => AllowProperty(DunePropertySignature.FromType<T>(propertyName, ctx), allowDependencies);

    public void AllowProperty(Type type, string propertyName, bool allowDependencies = true, DuneReflectionContext? ctx = null)
        => AllowProperty(DunePropertySignature.FromType(type, propertyName, ctx), allowDependencies);

    public void AllowProperty(PropertyInfo property, bool allowDependencies = true, DuneReflectionContext? ctx = null)
        => AllowProperty(DunePropertySignature.FromPropertyInfo(property, ctx), allowDependencies);

    public void AllowProperty(DunePropertySignature property, bool allowDependencies = true) {
        if (property.GetMethod != null) AllowMethod(property.GetMethod, allowDependencies);
        if (property.SetMethod != null) AllowMethod(property.SetMethod, allowDependencies);
    }

    public void BlockProperty(DunePropertySignature property) {
        if (property.GetMethod != null) BlockMethod(property.GetMethod);
        if (property.SetMethod != null) BlockMethod(property.SetMethod);
    }

    #endregion

    #region Events

    public void AllowEvent<T>(string eventName, bool allowDependencies = true, DuneReflectionContext? ctx = null)
        => AllowEvent(DuneEventSignature.FromType<T>(eventName, ctx), allowDependencies);

    public void AllowEvent(Type type, string eventName, bool allowDependencies = true, DuneReflectionContext? ctx = null)
        => AllowEvent(DuneEventSignature.FromType(type, eventName, ctx), allowDependencies);

    public void AllowEvent(EventInfo @event, bool allowDependencies = true, DuneReflectionContext? ctx = null)
        => AllowEvent(DuneEventSignature.FromEventInfo(@event, ctx), allowDependencies);

    public void AllowEvent(DuneEventSignature @event, bool allowDependencies = true) {
        if (@event.AddMethod != null) AllowMethod(@event.AddMethod, allowDependencies);
        if (@event.RaiseMethod != null) AllowMethod(@event.RaiseMethod, allowDependencies);
        if (@event.RemoveMethod != null) AllowMethod(@event.RemoveMethod, allowDependencies);
    }

    public void BlockEvent(DuneEventSignature @event) {
        if (@event.AddMethod != null) BlockMethod(@event.AddMethod);
        if (@event.RaiseMethod != null) BlockMethod(@event.RaiseMethod);
        if (@event.RemoveMethod != null) BlockMethod(@event.RemoveMethod);
    }

    #endregion

    public void AllowAll<T>(bool allowDependencies = true, DuneReflectionContext? ctx = null)
        => AllowAll(typeof(T), allowDependencies, ctx);

    public void AllowAll(Type type, bool allowDependencies = true, DuneReflectionContext? ctx = null) {
        AllowType(type, allowDependencies, ctx);

        foreach (MethodInfo method in type.GetMethods(DuneReflectionContext.EverythingPublicFlags))
            AllowMethod(method, allowDependencies, ctx);

        foreach (ConstructorInfo method in type.GetConstructors(DuneReflectionContext.EverythingPublicFlags))
            AllowMethod(method, allowDependencies, ctx);

        foreach (FieldInfo field in type.GetFields(DuneReflectionContext.EverythingPublicFlags))
            AllowField(field, allowDependencies, ctx);

        foreach (Type nested in type.GetNestedTypes(DuneReflectionContext.EverythingPublicFlags))
            AllowAll(nested, allowDependencies, ctx);
    }

    public bool IsEquivalentTo(DuneSandboxRules other) {
        if (AllowTypedReferenceKeywords != other.AllowTypedReferenceKeywords)
            return false;

        if (AllowDynamicKeyword != other.AllowDynamicKeyword)
            return false;

        if (AllowUnsafe != other.AllowUnsafe)
            return false;

        foreach (string assemblyName in _assemblyRules.Keys.Concat(other._assemblyRules.Keys).Distinct()) {

            _assemblyRules.TryGetValue(assemblyName, out DuneSandboxAssemblyRules? assemblyRules);
            other._assemblyRules.TryGetValue(assemblyName, out DuneSandboxAssemblyRules? assemblyRulesOther);

            bool assemblyAllowed = assemblyRules?.Allow ?? false;
            bool assemblyAllowedOther = assemblyRulesOther?.Allow ?? false;

            if (assemblyAllowed != assemblyAllowedOther)
                return false;

            if (assemblyAllowed) {
                if (assemblyRules!.Assembly != assemblyRulesOther!.Assembly)
                    return false;
            }

            IEnumerable<DuneTypeSignature> definedTypes = assemblyRules == null ? [] : assemblyRules._types.Keys;

            if (assemblyRulesOther != null)
                definedTypes = definedTypes.Concat(assemblyRulesOther._types.Keys).Distinct();

            foreach (DuneTypeSignature definedType in definedTypes) {
                DuneSandboxTypeRules? typeRules = null;
                assemblyRules?._types.TryGetValue(definedType, out typeRules);

                DuneSandboxTypeRules? typeRulesOther = null;
                assemblyRulesOther?._types.TryGetValue(definedType, out typeRulesOther);

                bool typeAllowed = typeRules?.Allow ?? false;
                bool typeAllowedOther = typeRulesOther?.Allow ?? false;

                if (typeAllowed != typeAllowedOther)
                    return false;

                IEnumerable<DuneMethodSignature> allowedMethods = typeRules == null ? [] : typeRules._allowedMethods;
                if (!allowedMethods.SequenceEqual(typeRulesOther?._allowedMethods ?? []))
                    return false;


                IEnumerable<DuneFieldSignature> allowedFields = typeRules == null ? [] : typeRules._allowedFields;
                if (!allowedFields.SequenceEqual(typeRulesOther?._allowedFields ?? []))
                    return false;
            }
        }

        return true;
    }
}

