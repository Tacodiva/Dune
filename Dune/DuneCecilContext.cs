
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Mono.Cecil;

namespace Dune;
public sealed class DuneCecilContext : DuneContext {

    private Dictionary<AssemblyNameReference, DuneAssemblyReference>? _assemblyReferences = null;
    private Dictionary<CecilTypeDefinition, DuneTypeSignature>? _typeSignatures = null;
    private Dictionary<CecilTypeReference, DuneTypeReference>? _typeReferences = null;
    private Dictionary<CecilMethodDefinition, DuneMethodSignature>? _methodSignatures = null;
    private Dictionary<CecilMethodReference, DuneMethodReference>? _methodReferences = null;
    private Dictionary<CecilFieldDefinition, DuneFieldSignature>? _fieldSignatures = null;
    private Dictionary<CecilFieldReference, DuneFieldReference>? _fieldReferences = null;

    #region Assembly
    public bool TryGetAssemblyReference(AssemblyNameReference assemblyName, [NotNullWhen(true)] out DuneAssemblyReference? value) {
        value = null;
        return _assemblyReferences?.TryGetValue(assemblyName, out value) ?? false;
    }

    internal DuneAssemblyReference PutAssemblyReference(AssemblyNameReference assemblyName, DuneAssemblyReference value) {
        return (_assemblyReferences ??= [])[assemblyName] = value;
    }
    #endregion

    #region Type
    public bool TryGetTypeSignature(CecilTypeDefinition typeDef, [NotNullWhen(true)] out DuneTypeSignature? value) {
        value = null;
        return _typeSignatures?.TryGetValue(typeDef, out value) ?? false;
    }

    internal DuneTypeSignature PutTypeSignature(CecilTypeDefinition typeDef, DuneTypeSignature value) {
        return (_typeSignatures ??= [])[typeDef] = value;
    }

    public bool TryGetTypeReference(CecilTypeReference typeRef, [NotNullWhen(true)] out DuneTypeReference? value) {
        value = null;
        return _typeReferences?.TryGetValue(typeRef, out value) ?? false;
    }

    internal DuneTypeReference PutTypeReference(CecilTypeReference typeRef, DuneTypeReference value) {
        return (_typeReferences ??= [])[typeRef] = value;
    }
    #endregion

    #region Method
    public bool TryGetMethodSignature(CecilMethodDefinition methodDef, [NotNullWhen(true)] out DuneMethodSignature? value) {
        value = null;
        return _methodSignatures?.TryGetValue(methodDef, out value) ?? false;
    }

    internal DuneMethodSignature PutMethodSignature(CecilMethodDefinition methodDef, DuneMethodSignature value) {
        return (_methodSignatures ??= [])[methodDef] = value;
    }

    public bool TryGetMethodReference(CecilMethodReference methodRef, [NotNullWhen(true)] out DuneMethodReference? value) {
        value = null;
        return _methodReferences?.TryGetValue(methodRef, out value) ?? false;
    }

    internal DuneMethodReference PutMethodReference(CecilMethodReference methodRef, DuneMethodReference value) {
        return (_methodReferences ??= [])[methodRef] = value;
    }
    #endregion

    #region Field
    public bool TryGetFieldSignature(CecilFieldDefinition fieldDef, [NotNullWhen(true)] out DuneFieldSignature? value) {
        value = null;
        return _fieldSignatures?.TryGetValue(fieldDef, out value) ?? false;
    }

    internal DuneFieldSignature PutFieldSignature(CecilFieldDefinition fieldDef, DuneFieldSignature value) {
        return (_fieldSignatures ??= [])[fieldDef] = value;
    }

    public bool TryGetFieldReference(CecilFieldReference fieldRef, [NotNullWhen(true)] out DuneFieldReference? value) {
        value = null;
        return _fieldReferences?.TryGetValue(fieldRef, out value) ?? false;
    }

    internal DuneFieldReference PutFieldReference(CecilFieldReference fieldRef, DuneFieldReference value) {
        return (_fieldReferences ??= [])[fieldRef] = value;
    }
    #endregion
}