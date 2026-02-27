
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.CodeAnalysis;
using Mono.Cecil;

namespace Dune;

/// <summary>
/// Represents an assembly tracked by Dune.
/// This assembly could come from a variety of sources.
/// </summary>
public sealed partial class DuneAssembly {

    public static DuneAssembly FromAssembly(Assembly assembly, DuneReflectionContext? ctx = null)
        => new(assembly, ctx);

    public static DuneAssembly FromBytes(byte[] bytes, string? path)
        => new(bytes, path);

    public static DuneAssembly FromPath(string path)
        => new(File.ReadAllBytes(path), path);

    public static DuneAssembly FromCecilDefinition(CecilAssemblyDefinition definition, DuneCecilContext? ctx = null)
        => new(definition, ctx);

    public DuneAssemblyReference Reference { get; }
    public IEnumerable<DuneAssemblyReference> ReferencedAssemblies { get; }

    public string? Path { get; }

    private Assembly? _runtimeAssembly;
    private byte[]? _assemblyBytes;
    private CecilAssemblyDefinition? _cecilDefinition;
    private PortableExecutableReference? _portableExeReference;
    private DuneAssemblyDefinition? _duneDefinition;

    private DuneAssembly(Assembly assembly, DuneReflectionContext? ctx) {
        Reference = DuneAssemblyReference.FromAssembly(assembly, ctx);

        ReferencedAssemblies = assembly.GetReferencedAssemblies()
            .Select(name => DuneAssemblyReference.FromAssemblyName(name, ctx))
            .ToArray();

        Path = string.IsNullOrEmpty(assembly.Location) ? null : assembly.Location;

        _runtimeAssembly = assembly;
    }

    private DuneAssembly(byte[] bytes, string? path) {
        Path = path;

        using (PEReader peReader = new(new MemoryStream(bytes))) {
            MetadataReader reader = peReader.GetMetadataReader();

            Reference = DuneAssemblyReference.FromMetadataReader(reader);

            ReferencedAssemblies = reader.AssemblyReferences
                .Select(handle => DuneAssemblyReference.FromMetadataAssemblyReference(reader, reader.GetAssemblyReference(handle)))
                .ToArray();
        }

        _assemblyBytes = bytes;
    }

    private DuneAssembly(CecilAssemblyDefinition definition, DuneCecilContext? ctx) {
        Reference = DuneAssemblyReference.FromCecilReference(definition.Name, ctx);

        ReferencedAssemblies = definition.Modules
            .SelectMany(def => def.AssemblyReferences)
            .Select(refernece => DuneAssemblyReference.FromCecilReference(refernece, ctx))
            .ToArray();

        _cecilDefinition = definition;
    }

    /// <summary>
    /// True if we are able to provide a byte representation of this assembly, otherwise false.
    /// </summary>
    public bool HasBytes => _cecilDefinition != null || _assemblyBytes != null || Path != null;

    private byte[]? TryLoadBytes() {
        InternalUtils.Assert(Monitor.IsEntered(this));

        if (Path != null) return File.ReadAllBytes(Path);

        if (_cecilDefinition != null) {
            using MemoryStream byteStream = new();

            _cecilDefinition.Write(byteStream, new() {
                // https://github.com/jbevain/cecil/issues/920
                // WriteSymbols = true
            });

            return byteStream.ToArray();
        }

        return null;
    }

    /// <summary>
    /// Returns a byte representation of this assembly. Do not mutate the byte array.
    /// </summary>
    [MethodImpl(MethodImplOptions.Synchronized)]
    public byte[] GetBytes() {
        if (_assemblyBytes == null) {
            _assemblyBytes = TryLoadBytes();

            if (_assemblyBytes == null)
                throw new InvalidOperationException($"Cannot get bytes of assembly {this} which does not have a path, provided bytes or cecil definition.");
        }

        return _assemblyBytes;
    }

    /// <summary>
    /// Attempts to clear the bytes array associated with this assembly.
    /// </summary>
    /// <returns>True if the assembly had bytes to clear, otherwise false.</returns>
    public bool ClearBytes() {
        if (_assemblyBytes == null)
            return false;

        _assemblyBytes = null;
        return true;
    }

    /// <summary>
    /// True if we are able to provide a cecil definition for this assembly, otherwise false.
    /// </summary>
    public bool HasCecilDefinition => _cecilDefinition != null;

    /// <summary>
    /// Returns the cecil representation of this assembly. Do not mutate the definition.
    /// </summary>
    public CecilAssemblyDefinition GetCecilDefinition() {
        if (_cecilDefinition == null)
            throw new InvalidOperationException($"Assembly {this} does not have a cecil definition.");

        return _cecilDefinition;
    }

    /// <summary>
    /// Create a cecil definition for this assembly from the bytes.
    /// </summary>
    [MethodImpl(MethodImplOptions.Synchronized)]
    public CecilAssemblyDefinition LoadCecil(ReaderParameters? readerParameters) {
        if (_cecilDefinition != null)
            throw new InvalidOperationException($"Assembly {this} already has a cecil definition.");

        if (!HasBytes)
            throw new InvalidOperationException($"Cannot load cecil for assembly {this} as it has no avaliable bytes.");

        return _cecilDefinition = CecilAssemblyDefinition.ReadAssembly(
            new MemoryStream(GetBytes()),
            readerParameters ?? new ReaderParameters(ReadingMode.Deferred)
        );
    }

    /// <summary>
    /// Attempts to clear the cecil assembly definition associated with this assembly.
    /// </summary>
    /// <param name="dispose">If true, dispsoe the cleared cecil definitions.</param>
    /// <returns>True if the assembly had a cecil definition to clear, otherwise false.</returns>
    public bool ClearCecil(bool dispose = true) {
        if (_cecilDefinition == null)
            return false;

        if (dispose) _cecilDefinition.Dispose();

        _cecilDefinition = null;
        return true;
    }

    /// <summary>
    /// True if this assembly is loaded in the current runtime, otherwise false.
    /// </summary>
    public bool IsRuntimeLoaded => _runtimeAssembly != null;

    public Assembly GetAssembly() {
        if (_runtimeAssembly == null)
            throw new InvalidOperationException($"Assembly {this} is not currently loaded.");
        return _runtimeAssembly;
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    public PortableExecutableReference GetPortableExecutableReference() {
        if (_portableExeReference != null) return _portableExeReference;
        return _portableExeReference = MetadataReference.CreateFromImage(GetBytes(), filePath: Path);
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    public DuneAssemblyDefinition GetDefinition() {
        if (_duneDefinition != null) return _duneDefinition;

        if (_runtimeAssembly != null)
            return _duneDefinition = DuneAssemblyDefinition.FromAssembly(_runtimeAssembly);

        if (_cecilDefinition != null)
            return _duneDefinition = DuneAssemblyDefinition.FromCecilDefinition(_cecilDefinition);

        throw new InvalidOperationException($"Cannot get definition of assembly {this} which does not have a cecil definition and is not runtime loaded.");
    }

    public override string ToString() {
        return Reference.ToString();
    }
}