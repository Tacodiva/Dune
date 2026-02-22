
#if NET

using System.IO;
using System.Runtime.Loader;

namespace Dune;

public sealed class DuneAssemblyLoadContextProvider(AssemblyLoadContext loadContext) : DuneCachedAssemblyProvider {

    public AssemblyLoadContext AssemblyLoadContext { get; } = loadContext;

    protected override DuneAssembly? TryGetUncachedAssembly(DuneAssemblyReference query) {
        try {
            return DuneAssembly.FromAssembly(
                AssemblyLoadContext.LoadFromAssemblyName(query.ToAssemblyName())
            );
        } catch (FileNotFoundException) {
            return null;
        }
    }
}

#endif