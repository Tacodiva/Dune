
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace Dune;

public interface IDuneSymbol : IEquatable<IDuneSymbol> {
    public string Name { get; }
    public DuneAssemblyReference? Assembly { get; }

    public abstract string? ToString();
    public abstract int GetHashCode();
}

public interface IDuneGenericSymbol : IDuneSymbol {
    public bool HasGenericParameters { get; }
    public StringBuilder FormatAndAppendGenericParameters(in DuneTypeFormat genericFormat, StringBuilder sb);
}

public interface IDuneType : IDuneGenericSymbol {
    public StringBuilder FormatAndAppendName(in DuneTypeFormat format, StringBuilder sb);
}

public interface IDuneMember : IDuneSymbol {
    public IDuneType? DeclaringType { get; }
    public new DuneAssemblyReference Assembly { get; }

    public StringBuilder FormatAndAppendName(in DuneTypeFormat format, StringBuilder sb);
}

public interface IDuneMemberReference : IDuneMember {
    public new DuneTypeSignatureReference? DeclaringType { get; }
    public IDuneMemberSignature Signature { get; }
}

public interface IDuneMemberSignature : IDuneMember {
    public new DuneTypeSignature? DeclaringType { get; }
}

public interface IDuneGenericSignature : IDuneGenericSymbol {
    public ImmutableArray<string> GenericParameterNames { get; }
}

public interface IDuneGenericReference : IDuneGenericSymbol {
    public IDuneGenericSignature Signature { get; }
    public ImmutableArray<DuneTypeReference> GenericArguments { get; }
}

public abstract class DuneMemberSignature(string name, DuneTypeSignature declaringType, DuneAssemblyReference assembly) : IDuneMemberSignature {
    public string Name { get; } = name;
    public DuneTypeSignature DeclaringType { get; } = declaringType;
    public DuneAssemblyReference Assembly { get; } = assembly;

    IDuneType? IDuneMember.DeclaringType => DeclaringType;

    public abstract bool Equals(IDuneSymbol? other);

    public StringBuilder FormatAndAppendName(in DuneTypeFormat format, StringBuilder sb) {
        return sb.Append(Name);
    }
}

public abstract class DuneMemberReference<TSignature>(TSignature signature, DuneTypeSignatureReference declaringType) : IDuneMemberReference
    where TSignature : IDuneMemberSignature {

    public TSignature Signature { get; } = signature;

    public DuneTypeSignatureReference DeclaringType { get; } = declaringType;
    public DuneAssemblyReference Assembly => DeclaringType.Assembly;
    public string Name => Signature.Name;

    IDuneType? IDuneMember.DeclaringType => DeclaringType;
    IDuneMemberSignature IDuneMemberReference.Signature => Signature;

    public abstract bool Equals(IDuneSymbol? other);

    public StringBuilder FormatAndAppendName(in DuneTypeFormat format, StringBuilder sb) {
        return sb.Append(Name);
    }
}