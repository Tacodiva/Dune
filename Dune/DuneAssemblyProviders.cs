
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;

namespace Dune;

public abstract class DuneCachedAssemblyProvider : DuneAssemblyProvider {
    private readonly Dictionary<string, List<DuneAssembly>> _assemblyNameMap = new();

    public IEnumerable<DuneAssembly> Assemblies => _assemblyNameMap.Values.SelectMany(list => list);

    internal void CacheAssembly(DuneAssembly assembly, bool replace = true) {
        if (!_assemblyNameMap.TryGetValue(assembly.Reference.Name, out List<DuneAssembly>? assemblies)) {
            _assemblyNameMap[assembly.Reference.Name] = [assembly];
            return;
        }

        for (int i = assemblies.Count - 1; i >= 0; i--) {
            if (assemblies[i].Reference.Matches(assembly)) {
                if (replace) {
                    assemblies.RemoveAt(i);
                } else {
                    throw new ArgumentException($"Assembly matching {assembly.Reference} already in provider.");
                }
            }
        }

        assemblies.Add(assembly);
    }

    public override DuneAssembly? TryGetAssembly(DuneAssemblyReference query) {
        DuneAssembly? assembly = TryGetCachedAssembly(query);

        if (assembly != null) return assembly;

        assembly = TryGetUncachedAssembly(query);

        if (assembly != null) CacheAssembly(assembly);

        return assembly;
    }

    private DuneAssembly? TryGetCachedAssembly(DuneAssemblyReference query) {
        if (!_assemblyNameMap.TryGetValue(query.Name, out List<DuneAssembly>? assemblies))
            return null;

        foreach (DuneAssembly assembly in assemblies)
            if (query.Matches(assembly)) return assembly;

        return null;
    }

    protected abstract DuneAssembly? TryGetUncachedAssembly(DuneAssemblyReference query);
}

public sealed class DuneListAssemblyProvider : DuneCachedAssemblyProvider {
    public void AddAssembly(DuneAssembly assembly) => CacheAssembly(assembly);

    public void AddAssembly(Assembly assembly) => AddAssembly(DuneAssembly.FromAssembly(assembly));

    public void AddAssemblyFromBytes(byte[] bytes, string? path) => AddAssembly(DuneAssembly.FromBytes(bytes, path));

    public void AddAssemblyFromPath(string path) => AddAssembly(DuneAssembly.FromPath(path));

    public void AddAssemblyFromCecilDefinition(CecilAssemblyDefinition definition) => AddAssembly(DuneAssembly.FromCecilDefinition(definition));

    protected override DuneAssembly? TryGetUncachedAssembly(DuneAssemblyReference query)
        => null!;
}

public sealed class DuneDirectoryAssemblyProvider(string directory, bool recursive = false) : DuneCachedAssemblyProvider {

    public static DuneAssembly? SearchDirectory(DuneAssemblyReference query, string directory, bool recursive) {
        foreach (string extension in Extensions) {
            string path = Path.Combine(directory, query.Name + extension);

            if (File.Exists(path)) {

                DuneAssemblyReference reference = DuneAssemblyReference.FromPath(path);

                if (query.Matches(reference))
                    return DuneAssembly.FromPath(path);
            }
        }

        if (recursive) {
            foreach (string subDir in System.IO.Directory.EnumerateDirectories(directory)) {
                DuneAssembly? assembly = SearchDirectory(query, subDir, recursive);
                if (assembly != null) return assembly;
            }
        }

        return null;
    }

    public static readonly string[] Extensions = [".exe", ".dll"];

    public string Directory { get; } = directory;
    public bool IsRecursive { get; } = recursive;

    protected override DuneAssembly? TryGetUncachedAssembly(DuneAssemblyReference query) {
        return SearchDirectory(query, Directory, IsRecursive);
    }
}

public sealed class DuneMultiAssemblyProvider(params IEnumerable<DuneAssemblyProvider> providers) : DuneAssemblyProvider {

    private readonly List<DuneAssemblyProvider> _assemblyProviders = providers.ToList();
    public IEnumerable<DuneAssemblyProvider> AssemblyProviders => _assemblyProviders;

    public void AddProvider(DuneAssemblyProvider provider) {
        _assemblyProviders.Add(provider);
    }

    public bool RemoveProvider(DuneAssemblyProvider provider) {
        return _assemblyProviders.Remove(provider);
    }

    public override DuneAssembly? TryGetAssembly(DuneAssemblyReference query) {
        foreach (DuneAssemblyProvider provider in _assemblyProviders) {
            DuneAssembly? assembly = provider.TryGetAssembly(query);
            if (assembly != null) return assembly;
        }

        return null;
    }
}

/// <summary>
/// A general purpose implementation of DuneAssemblyProvider which provides lots of functionality for ease of use.
/// </summary>
public sealed class DuneAssemblyEnvironment : DuneAssemblyProvider {

    /// <summary>
    /// When true, this environment should load assemblies from the current context (assemblies from the current runtime).
    /// Only supported on .NET 5 or greater. 
    /// </summary>
    public bool LoadFromContext { get; }

    private DuneListAssemblyProvider? _listProvider;

    private Dictionary<string, bool>? _directories;

#if NET
    private DuneAssemblyLoadContextProvider? _contextProvider;
#endif

    public DuneAssemblyEnvironment(bool loadFromContext = true) {
        LoadFromContext = loadFromContext;

        if (LoadFromContext) {
#if NET
            _contextProvider = new(System.Runtime.Loader.AssemblyLoadContext.Default);
#else
            throw new PlatformNotSupportedException("Load from context is only supported on .NET 5 or greater.");
#endif
        }
    }

    public void AddAssembly(DuneAssembly assembly) {
        _listProvider ??= new();
        _listProvider.AddAssembly(assembly);
    }

    public void AddAssembly(Assembly assembly) => AddAssembly(DuneAssembly.FromAssembly(assembly));

    public void AddAssemblyFromBytes(byte[] bytes, string? path) => AddAssembly(DuneAssembly.FromBytes(bytes, path));

    public void AddAssemblyFromPath(string path) => AddAssembly(DuneAssembly.FromPath(path));

    public void AddAssemblyFromCecilDefinition(CecilAssemblyDefinition definition) => AddAssembly(DuneAssembly.FromCecilDefinition(definition));

    public void AddDirectory(string path, bool recursive) {
        _directories ??= new();
        _directories[path] = recursive;
    }

    public override DuneAssembly? TryGetAssembly(DuneAssemblyReference query) {
        if (_listProvider != null) {
            DuneAssembly? assembly = _listProvider.TryGetAssembly(query);
            if (assembly != null) return assembly;
        }

#if NET
        if (_contextProvider != null) {
            DuneAssembly? assembly = _contextProvider.TryGetAssembly(query);
            if (assembly != null) return assembly;
        }
#endif

        if (_directories != null) {
            foreach (KeyValuePair<string, bool> entry in _directories) {
                string path = entry.Key;
                bool isRecursive = entry.Value;

                DuneAssembly? assembly = DuneDirectoryAssemblyProvider.SearchDirectory(query, path, isRecursive);

                if (assembly != null) {
                    AddAssembly(assembly);
                    return assembly;
                }
            }
        }

        return null;
    }
}