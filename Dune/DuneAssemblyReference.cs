using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Microsoft.CodeAnalysis;
using Mono.Cecil;

namespace Dune;

/// <summary>
/// Represents a reference to an assembly, which specifies a name and may specify a version and culture.
/// </summary>
public sealed class DuneAssemblyReference(string name, Version? version = null, string? cultureName = null) : IEquatable<DuneAssemblyReference> {

    public string Name { get; } = name;
    public Version? Version { get; } = version;
    public string? CultureName { get; } = string.IsNullOrWhiteSpace(cultureName) ? null : cultureName;

    [MemberNotNullWhen(true, nameof(Version))]
    public bool HasVersion => Version != null;

    [MemberNotNullWhen(true, nameof(CultureName))]
    public bool HasCulture => CultureName != null;

    public static DuneAssemblyReference FromAssembly(Assembly assembly, DuneReflectionContext? ctx = null)
        => FromAssemblyName(assembly.GetName(), ctx);

    public static DuneAssemblyReference FromAssemblyName(AssemblyName assemblyName, DuneReflectionContext? ctx = null) {
        InternalUtils.ThrowIfArgumentNullOrWhitespace(assemblyName.Name);

        if (ctx?.TryGetAssemblyReference(assemblyName, out var cached) ?? false)
            return cached;

        DuneAssemblyReference reference = new(
            assemblyName.Name,
            assemblyName.Version,
            assemblyName.CultureName ?? ""
        );

        return ctx?.PutAssemblyReference(assemblyName, reference) ?? reference;
    }

    public static DuneAssemblyReference FromAssemblyDefinition(CecilAssemblyDefinition assemblyDef, DuneCecilContext? ctx = null)
        => FromAssemblyNameReference(assemblyDef.Name, ctx);

    public static DuneAssemblyReference FromAssemblyNameReference(AssemblyNameReference assemblyNameRef, DuneCecilContext? ctx = null) {
        InternalUtils.ThrowIfArgumentNullOrWhitespace(assemblyNameRef.Name);

        if (ctx?.TryGetAssemblyReference(assemblyNameRef, out var cached) ?? false)
            return cached;

        DuneAssemblyReference reference = new(
            assemblyNameRef.Name,
            assemblyNameRef.Version,
            assemblyNameRef.Culture
        );

        return ctx?.PutAssemblyReference(assemblyNameRef, reference) ?? reference;
    }

    public static DuneAssemblyReference FromSymbol(IAssemblySymbol symbol, DuneRoslynContext? ctx = null)
        => FromAssemblyIdentity(symbol.Identity, ctx);

    public static DuneAssemblyReference FromAssemblyIdentity(AssemblyIdentity identity, DuneRoslynContext? ctx = null) {
        if (ctx?.TryGetAssemblyReference(identity, out var cached) ?? false)
            return cached;

        return new DuneAssemblyReference(
            identity.Name,
            identity.Version,
            identity.CultureName
        );
    }

    public static DuneAssemblyReference FromPath(string path)
        => FromStream(new FileStream(path, FileMode.Open, FileAccess.Read));

    public static DuneAssemblyReference FromBytes(ArraySegment<byte> bytes) {
        InternalUtils.ThrowIfArgumentNull(bytes.Array);
        return FromStream(new MemoryStream(bytes.Array, bytes.Offset, bytes.Count, false));
    }

    public static DuneAssemblyReference FromStream(Stream stream) {
        using PEReader peReader = new(stream);

        return FromMetadataReader(peReader.GetMetadataReader());
    }

    internal static DuneAssemblyReference FromMetadataReader(MetadataReader reader) {
        ReflectionAssemblyDefinition assemblyDefinition = reader.GetAssemblyDefinition();

        return new DuneAssemblyReference(
            reader.GetString(assemblyDefinition.Name),
            assemblyDefinition.Version,
            reader.GetString(assemblyDefinition.Culture)
        );
    }

    internal static DuneAssemblyReference FromMetadataAssemblyReference(MetadataReader reader, ReflectionAssemblyReference reference) {
        return new DuneAssemblyReference(
            reader.GetString(reference.Name),
            reference.Version,
            reader.GetString(reference.Culture) ?? ""
        );
    }

    public bool Matches(DuneAssembly assembly) => Matches(assembly.Reference);

    public bool Matches(DuneAssemblyReference reference) {
        if (Name != reference.Name) return false;

        if (HasVersion && reference.HasVersion) {
            if (Version != reference.Version) return false;
        }

        if (HasCulture && reference.HasCulture) {
            if (CultureName != reference.CultureName) return false;
        }

        return true;
    }

    public override bool Equals(object? obj) => Equals(obj as DuneAssemblyReference);
    public bool Equals(DuneAssemblyReference? reference) {
        if (ReferenceEquals(this, reference)) return true;
        if (reference is null) return false;
        
        if (Name != reference.Name) return false;
        if (Version != reference.Version) return false;
        if (CultureName != reference.CultureName) return false;
        return true;
    }

    public static bool operator ==(DuneAssemblyReference? a, DuneAssemblyReference? b) => a?.Equals(b) ?? b is null;
    public static bool operator !=(DuneAssemblyReference? a, DuneAssemblyReference? b) => !(a == b);

    public DuneAssemblyReference WithVersion(Version? version)
        => new(Name, version, CultureName);

    public DuneAssemblyReference WithCulture(string? cultureName)
        => new(Name, Version, cultureName);

    public DuneAssemblyReference WithVersionAndCulture(Version? version, string? cultureName)
        => new(Name, version, cultureName);

    public AssemblyName ToAssemblyName() {
        return new AssemblyName() {
            Name = Name,
            Version = Version,
            CultureName = CultureName
        };
    }

    public AssemblyIdentity ToAssemblyIdentity() {
        return new AssemblyIdentity(
            Name,
            Version,
            CultureName
        );
    }

    public AssemblyNameReference ToAssemblyNameReference() {
        return new AssemblyNameReference(Name, Version) {
            Culture = CultureName
        };
    }

    public override string ToString() {
        string str = Name;
        if (Version != null) str += $", Version={Version}";
        if (CultureName != null) str += $", Culture={CultureName}";
        return str;
    }

    public override int GetHashCode() => InternalUtils.HashCodeCombine(Name, Version, CultureName);

}
