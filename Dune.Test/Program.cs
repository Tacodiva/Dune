
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Text;
using Dune;
using Dune.Attributes;
using Mono.Cecil;

// AssemblyDefinition nativeAssembly = DuneAssembly.FromAssembly(typeof(int).Assembly).LoadCecil(null);

DuneSandboxRules rules = new(false);

rules.AllowAnnotatedAssembly(Assembly.GetExecutingAssembly());

// Console.WriteLine(DuneSandboxRules.Stringify(rules));

// DuneSandboxRules rules = new();

// rules.AllowMethod((Action<string>)Console.WriteLine);

byte[] ruleBytes = DuneSandboxRules.Serialize(rules);
File.WriteAllBytes("rules.dune", ruleBytes);

DuneSandboxRules readRules = DuneSandboxRules.Deserialize(ruleBytes);

if (!rules.IsEquivalentTo(readRules))
    throw new Exception("Read rules not equivalent.");

Console.WriteLine(DuneSandboxRules.Stringify(readRules));

// rules = readRules;

// DuneScriptConfig config = new();
// DuneAssemblyProvider env = new DuneAssemblyEnvironment();
// IDuneScriptReferenceResolver referenceResolver = new ReferenceResolver();

// string path = "../Test.csx";
// string source = File.ReadAllText(path);

// DuneCompilationResult result = DuneScriptCompiler.Compile(
//     config, env, referenceResolver, rules,
//     source, path
// );

// if (result.HasDiagnostics) {

//     foreach (DuneDiagnostic diagnostic in result.Diagnostics) {
//         diagnostic.PrintToConsole();
//     }
// }

// if (result.Success) {
//     result.WriteToFile("Out.dll");
// }

[AttributeUsage(AttributeTargets.Class)]
public sealed class MyTestAttribute : Attribute {

    public MyTestAttribute(Type type) { }
}

[DuneAllow]
public class MyType {

    [DuneAllow]
    public void AllowedMethod() { }

    public void NotAllowedMethod() { }

    [DuneAllow(isRecursive: true)]
    public class MyInnerType {

        public void RecursivelyAllowed() { }

        [DuneDeny]
        public void ExplicitlyDenies() { }

    }
}

public class ReferenceResolver : IDuneScriptReferenceResolver {
    public string? TryNormalizeSourceReference(string reference, string? baseFilePath) {
        if (baseFilePath == null) return Path.GetFullPath(reference);
        else return Path.GetFullPath(reference, Path.GetDirectoryName(Path.GetFullPath(baseFilePath))!);
    }

    public DuneAssembly? TryResolveAssemblyReference(string reference, string? baseFilePath) {
        return null;
    }

    public string? TryResolveSourceReference(string normalizedReference) {

        Console.WriteLine(Path.GetDirectoryName(normalizedReference));
        return null;
    }
}