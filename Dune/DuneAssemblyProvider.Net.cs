
#if NET

using System.Reflection;
using System.Runtime.Loader;

namespace Dune;

partial class DuneAssemblyProvider {

    private AssemblyLoadContextImpl? _assemblyLoadContext;

    /// <summary>
    /// Gets an AssemblyLoadContext which resolves and loads assemblies from this provider.
    /// </summary>
    public AssemblyLoadContext GetAssemblyLoadContext() {
        _assemblyLoadContext ??= new(this);
        return _assemblyLoadContext;
    }

    private sealed class AssemblyLoadContextImpl(DuneAssemblyProvider provider) : AssemblyLoadContext {
        public readonly DuneAssemblyProvider Provider = provider;

        protected override Assembly? Load(AssemblyName assemblyName) {
            DuneAssembly? resolved = Provider.TryGetAssembly(DuneAssemblyReference.FromAssemblyName(assemblyName));

            if (resolved == null) return null;

            if (resolved.IsRuntimeLoaded) return resolved.GetAssembly();

            return resolved.LoadAssembly(this);
        }
    }
}

#endif