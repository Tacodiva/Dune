using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Dune;

public interface IDuneSandboxRules {

    public bool AllowTypedReferenceKeywords { get; }
    public bool AllowDynamicKeyword { get; }
    public bool AllowUnsafe { get; }
    public bool CheckInternalReferences { get; }

    public bool IsAssemblyAllowed(DuneAssemblyReference assembly);
    public bool IsTypeAllowed(DuneTypeSignatureReference type);
    public bool IsMethodAllowed(DuneMethodReference method);
    public bool IsFieldAllowed(DuneFieldReference field);

}

public sealed class DuneSandboxTrustedAssemblyRules : IDuneSandboxRules {
    public static DuneSandboxTrustedAssemblyRules Instance { get; } = new();

    private DuneSandboxTrustedAssemblyRules() { }

    public bool AllowTypedReferenceKeywords => true;
    public bool AllowDynamicKeyword => true;
    public bool AllowUnsafe => true;
    public bool CheckInternalReferences => false;

    public bool IsAssemblyAllowed(DuneAssemblyReference assembly) => true;
    public bool IsFieldAllowed(DuneFieldReference field) => true;
    public bool IsMethodAllowed(DuneMethodReference method) => true;
    public bool IsTypeAllowed(DuneTypeSignatureReference type) => true;
}

public sealed class DuneSandboxTestRules : IDuneSandboxRules {
    public static DuneSandboxTestRules Instance { get; } = new();

    private DuneSandboxTestRules() { }

    public bool AllowTypedReferenceKeywords => false;
    public bool AllowDynamicKeyword => false;
    public bool AllowUnsafe => false;
    public bool CheckInternalReferences => true;

    public bool IsAssemblyAllowed(DuneAssemblyReference assembly) => assembly.Name.StartsWith("Evil");
    public bool IsFieldAllowed(DuneFieldReference field) => field.Name.StartsWith("Evil");
    public bool IsMethodAllowed(DuneMethodReference method) => method.Name.StartsWith("Evil");
    public bool IsTypeAllowed(DuneTypeSignatureReference type) => type.Name.StartsWith("Evil");
}

