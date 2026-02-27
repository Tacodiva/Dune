
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.Loader;

namespace Dune;
public sealed class DuneReflectionContext : DuneContext {

    internal const BindingFlags EverythingFlags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    internal const BindingFlags EverythingPublicFlags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public;

    internal const BindingFlags EverythingWithinFlags = EverythingFlags | BindingFlags.DeclaredOnly;
    internal const BindingFlags EverythingPublicWithinFlags = EverythingPublicFlags | BindingFlags.DeclaredOnly;


    private Dictionary<AssemblyName, DuneAssemblyReference>? _assemblyReferences = null;
    private Dictionary<Type, DuneTypeSignature>? _typeSignatures = null;
    private Dictionary<Type, DuneTypeReference>? _typeReferences = null;
    private Dictionary<MethodBase, DuneMethodSignature>? _methodSignatures = null;
    private Dictionary<MethodBase, DuneMethodReference>? _methodReferences = null;
    private Dictionary<EventInfo, DuneEventSignature>? _eventSignatures = null;
    private Dictionary<EventInfo, DuneEventReference>? _eventReferences = null;
    private Dictionary<PropertyInfo, DunePropertySignature>? _propertySignatures = null;
    private Dictionary<PropertyInfo, DunePropertyReference>? _propertyReferences = null;
    private Dictionary<FieldInfo, DuneFieldSignature>? _fieldSignatures = null;
    private Dictionary<FieldInfo, DuneFieldReference>? _fieldReferences = null;

    public AssemblyLoadContext AssemblyLoadContext { get; } = AssemblyLoadContext.Default;

    #region Assembly
    public bool TryGetAssemblyReference(AssemblyName assemblyName, [NotNullWhen(true)] out DuneAssemblyReference? value) {
        value = null;
        return _assemblyReferences?.TryGetValue(assemblyName, out value) ?? false;
    }

    internal DuneAssemblyReference PutAssemblyReference(AssemblyName assemblyName, DuneAssemblyReference value) {
        return (_assemblyReferences ??= new())[assemblyName] = value;
    }
    #endregion

    #region Type
    public bool TryGetTypeSignature(Type type, [NotNullWhen(true)] out DuneTypeSignature? value) {
        value = null;
        return _typeSignatures?.TryGetValue(type, out value) ?? false;
    }

    internal DuneTypeSignature PutTypeSignature(Type type, DuneTypeSignature value) {
        return (_typeSignatures ??= new())[type] = value;
    }

    public bool TryGetTypeReference(Type type, [NotNullWhen(true)] out DuneTypeReference? value) {
        value = null;
        return _typeReferences?.TryGetValue(type, out value) ?? false;
    }

    internal DuneTypeReference PutTypeReference(Type type, DuneTypeReference value) {
        return (_typeReferences ??= new())[type] = value;
    }
    #endregion

    #region Method
    public bool TryGetMethodSignature(MethodBase methodBase, [NotNullWhen(true)] out DuneMethodSignature? value) {
        value = null;
        return _methodSignatures?.TryGetValue(methodBase, out value) ?? false;
    }

    internal DuneMethodSignature PutMethodSignature(MethodBase methodBase, DuneMethodSignature value) {
        return (_methodSignatures ??= new())[methodBase] = value;
    }

    public bool TryGetMethodReference(MethodBase methodBase, [NotNullWhen(true)] out DuneMethodReference? value) {
        value = null;
        return _methodReferences?.TryGetValue(methodBase, out value) ?? false;
    }

    internal DuneMethodReference PutMethodReference(MethodBase methodBase, DuneMethodReference value) {
        return (_methodReferences ??= new())[methodBase] = value;
    }
    #endregion

    #region Event
    public bool TryGetEventSignature(EventInfo eventInfo, [NotNullWhen(true)] out DuneEventSignature? value) {
        value = null;
        return _eventSignatures?.TryGetValue(eventInfo, out value) ?? false;
    }

    internal DuneEventSignature PutEventSignature(EventInfo eventInfo, DuneEventSignature value) {
        return (_eventSignatures ??= new())[eventInfo] = value;
    }

    public bool TryGetEventReference(EventInfo eventInfo, [NotNullWhen(true)] out DuneEventReference? value) {
        value = null;
        return _eventReferences?.TryGetValue(eventInfo, out value) ?? false;
    }

    internal DuneEventReference PutEventReference(EventInfo eventInfo, DuneEventReference value) {
        return (_eventReferences ??= new())[eventInfo] = value;
    }
    #endregion

    #region Property
    public bool TryGetPropertySignature(PropertyInfo propertyInfo, [NotNullWhen(true)] out DunePropertySignature? value) {
        value = null;
        return _propertySignatures?.TryGetValue(propertyInfo, out value) ?? false;
    }

    internal DunePropertySignature PutPropertySignature(PropertyInfo propertyInfo, DunePropertySignature value) {
        return (_propertySignatures ??= new())[propertyInfo] = value;
    }

    public bool TryGetPropertyReference(PropertyInfo propertyInfo, [NotNullWhen(true)] out DunePropertyReference? value) {
        value = null;
        return _propertyReferences?.TryGetValue(propertyInfo, out value) ?? false;
    }

    internal DunePropertyReference PutPropertyReference(PropertyInfo propertyInfo, DunePropertyReference value) {
        return (_propertyReferences ??= new())[propertyInfo] = value;
    }
    #endregion

    #region Field
    public bool TryGetFieldSignature(FieldInfo fieldInfo, [NotNullWhen(true)] out DuneFieldSignature? value) {
        value = null;
        return _fieldSignatures?.TryGetValue(fieldInfo, out value) ?? false;
    }

    internal DuneFieldSignature PutFieldSignature(FieldInfo fieldInfo, DuneFieldSignature value) {
        return (_fieldSignatures ??= new())[fieldInfo] = value;
    }

    public bool TryGetFieldReference(FieldInfo fieldInfo, [NotNullWhen(true)] out DuneFieldReference? value) {
        value = null;
        return _fieldReferences?.TryGetValue(fieldInfo, out value) ?? false;
    }

    internal DuneFieldReference PutFieldReference(FieldInfo fieldInfo, DuneFieldReference value) {
        return (_fieldReferences ??= new())[fieldInfo] = value;
    }
    #endregion
}