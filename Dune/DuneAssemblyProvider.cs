
using System;
using System.Collections.Immutable;
using System.IO;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Mono.Cecil;

namespace Dune;

/// <summary>
/// A class which provides Dune assemblies. Can be used to load assemblies into the
/// current runtime or to load assemblies for use with cecil.
/// </summary>
public abstract partial class DuneAssemblyProvider {

    /// <summary>
    /// Gets an assembly matching the query from the provider. Returns null if no matching assembly is found.
    /// </summary>
    public abstract DuneAssembly? TryGetAssembly(DuneAssemblyReference query);


    /// <summary>
    /// Gets an assembly matching the query from the provider. Throws DuneAssemblyNotFoundExceptionException if no
    /// matching assembly is found.
    /// </summary>
    public DuneAssembly GetAssembly(DuneAssemblyReference query)
        => TryGetAssembly(query) ?? throw new DuneAssemblyNotFoundExceptionException(query);

    private AssemblyResolverImpl? _assemblyResolver;

    /// <summary>
    /// Gets an IAssemblyResolver which resolves and loads assemblies from this provider.
    /// </summary>
    public IAssemblyResolver GetAssemblyResolver() {
        _assemblyResolver ??= new(this);
        return _assemblyResolver;
    }

    private sealed class AssemblyResolverImpl(DuneAssemblyProvider provider) : IAssemblyResolver {
        public readonly DuneAssemblyProvider Provider = provider;

        private ReaderParameters? _providerReaderParams;

        public void Dispose() { }

        public CecilAssemblyDefinition Resolve(AssemblyNameReference name) {
            return Resolve(name, null);
        }

        public CecilAssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters? parameters) {
            try {
                DuneAssembly assembly = Provider.GetAssembly(DuneAssemblyReference.FromAssemblyNameReference(name));

                if (assembly.HasCecilDefinition) {
                    return assembly.GetCecilDefinition();
                }

                parameters ??= _providerReaderParams ??= new(ReadingMode.Immediate) {
                    AssemblyResolver = this
                };

                return assembly.LoadCecil(parameters);
            } catch (DuneAssemblyNotFoundExceptionException e) {
                throw new AssemblyResolutionException(name, e);
            }
        }
    }
}