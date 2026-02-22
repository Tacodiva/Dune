
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;

namespace Dune;
public sealed class DuneRoslynContext {

    private Dictionary<AssemblyIdentity, DuneAssemblyReference>? _assemblyReferences = null;
    private Dictionary<INamedTypeSymbol, DuneTypeSignature>? _typeSignatures = null;
    private Dictionary<(ITypeSymbol Type, bool IsRef), DuneTypeReference>? _typeReferences = null;
    private Dictionary<IMethodSymbol, DuneMethodSignature>? _methodSignatures = null;
    private Dictionary<IMethodSymbol, DuneMethodReference>? _methodReferences = null;
    private Dictionary<IEventSymbol, DuneEventSignature>? _eventSignatures = null;
    private Dictionary<IEventSymbol, DuneEventReference>? _eventReferences = null;
    private Dictionary<IPropertySymbol, DunePropertySignature>? _propertySignatures = null;
    private Dictionary<IPropertySymbol, DunePropertyReference>? _propertyReferences = null;
    private Dictionary<IFieldSymbol, DuneFieldSignature>? _fieldSignatures = null;
    private Dictionary<IFieldSymbol, DuneFieldReference>? _fieldReferences = null;

    #region Assembly
    public bool TryGetAssemblyReference(AssemblyIdentity assemblyName, [NotNullWhen(true)] out DuneAssemblyReference? value) {
        value = null;
        return _assemblyReferences?.TryGetValue(assemblyName, out value) ?? false;
    }

    internal DuneAssemblyReference PutAssemblyReference(AssemblyIdentity assemblyName, DuneAssemblyReference value) {
        return (_assemblyReferences ??= [])[assemblyName] = value;
    }
    #endregion

    #region Type
    public bool TryGetTypeSignature(INamedTypeSymbol typeSymbol, [NotNullWhen(true)] out DuneTypeSignature? value) {
        value = null;
        return _typeSignatures?.TryGetValue(typeSymbol, out value) ?? false;
    }

    internal DuneTypeSignature PutTypeSignature(INamedTypeSymbol typeSymbol, DuneTypeSignature value) {
        return (_typeSignatures ??= [])[typeSymbol] = value;
    }

    public bool TryGetTypeReference(ITypeSymbol typeSymbol, bool isRef, [NotNullWhen(true)] out DuneTypeReference? value) {
        value = null;
        return _typeReferences?.TryGetValue((typeSymbol, isRef), out value) ?? false;
    }

    internal DuneTypeReference PutTypeReference(ITypeSymbol typeSymbol, bool isRef, DuneTypeReference value) {
        return (_typeReferences ??= [])[(typeSymbol, isRef)] = value;
    }
    #endregion

    #region Method
    public bool TryGetMethodSignature(IMethodSymbol methodSymbol, [NotNullWhen(true)] out DuneMethodSignature? value) {
        value = null;
        return _methodSignatures?.TryGetValue(methodSymbol, out value) ?? false;
    }

    internal DuneMethodSignature PutMethodSignature(IMethodSymbol methodSymbol, DuneMethodSignature value) {
        return (_methodSignatures ??= [])[methodSymbol] = value;
    }

    public bool TryGetMethodReference(IMethodSymbol methodSymbol, [NotNullWhen(true)] out DuneMethodReference? value) {
        value = null;
        return _methodReferences?.TryGetValue(methodSymbol, out value) ?? false;
    }

    internal DuneMethodReference PutMethodReference(IMethodSymbol methodSymbol, DuneMethodReference value) {
        return (_methodReferences ??= [])[methodSymbol] = value;
    }
    #endregion

    #region Event
    public bool TryGetEventSignature(IEventSymbol eventSymbol, [NotNullWhen(true)] out DuneEventSignature? value) {
        value = null;
        return _eventSignatures?.TryGetValue(eventSymbol, out value) ?? false;
    }

    internal DuneEventSignature PutEventSignature(IEventSymbol eventSymbol, DuneEventSignature value) {
        return (_eventSignatures ??= [])[eventSymbol] = value;
    }

    public bool TryGetEventReference(IEventSymbol eventSymbol, [NotNullWhen(true)] out DuneEventReference? value) {
        value = null;
        return _eventReferences?.TryGetValue(eventSymbol, out value) ?? false;
    }

    internal DuneEventReference PutEventReference(IEventSymbol eventSymbol, DuneEventReference value) {
        return (_eventReferences ??= [])[eventSymbol] = value;
    }
    #endregion

    #region Property
    public bool TryGetPropertySignature(IPropertySymbol propertySymbol, [NotNullWhen(true)] out DunePropertySignature? value) {
        value = null;
        return _propertySignatures?.TryGetValue(propertySymbol, out value) ?? false;
    }

    internal DunePropertySignature PutPropertySignature(IPropertySymbol propertySymbol, DunePropertySignature value) {
        return (_propertySignatures ??= [])[propertySymbol] = value;
    }

    public bool TryGetPropertyReference(IPropertySymbol propertySymbol, [NotNullWhen(true)] out DunePropertyReference? value) {
        value = null;
        return _propertyReferences?.TryGetValue(propertySymbol, out value) ?? false;
    }

    internal DunePropertyReference PutPropertyReference(IPropertySymbol propertySymbol, DunePropertyReference value) {
        return (_propertyReferences ??= [])[propertySymbol] = value;
    }
    #endregion

    #region Field
    public bool TryGetFieldSignature(IFieldSymbol fieldSymbol, [NotNullWhen(true)] out DuneFieldSignature? value) {
        value = null;
        return _fieldSignatures?.TryGetValue(fieldSymbol, out value) ?? false;
    }

    internal DuneFieldSignature PutFieldSignature(IFieldSymbol fieldSymbol, DuneFieldSignature value) {
        return (_fieldSignatures ??= [])[fieldSymbol] = value;
    }

    public bool TryGetFieldReference(IFieldSymbol fieldSymbol, [NotNullWhen(true)] out DuneFieldReference? value) {
        value = null;
        return _fieldReferences?.TryGetValue(fieldSymbol, out value) ?? false;
    }

    internal DuneFieldReference PutFieldReference(IFieldSymbol fieldSymbol, DuneFieldReference value) {
        return (_fieldReferences ??= [])[fieldSymbol] = value;
    }
    #endregion
}