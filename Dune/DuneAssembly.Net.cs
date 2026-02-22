
#if NET

using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

namespace Dune;

partial class DuneAssembly {
    
    public Assembly LoadRuntime(DuneAssemblyProvider referenceProvider)
        => LoadAssembly(referenceProvider.GetAssemblyLoadContext());

    [MethodImpl(MethodImplOptions.Synchronized)]
    public Assembly LoadAssembly(AssemblyLoadContext ctx) {
        if (IsRuntimeLoaded)
            throw new InvalidOperationException($"Assembly {this} is already loaded.");

        return _runtimeAssembly = ctx.LoadFromStream(new MemoryStream(GetBytes()));
    }
}

#endif