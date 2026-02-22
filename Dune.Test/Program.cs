
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Dune;
using Mono.Cecil;


// DuneSandboxRules rules = new(false);

// foreach (Type type in typeof(int).Assembly.GetTypes()) {
//     DuneTypeDefinition typeDef = DuneTypeDefinition.FromType(type);

//     rules.AllowType(typeDef.Signature);

//     foreach (DuneMethodDefinition method in typeDef.Methods) rules.AllowMethod(method.Signature);
//     foreach (DuneFieldDefinition field in typeDef.Fields) rules.AllowField(field.Signature);
// }


// AssemblyDefinition nativeAssembly = DuneAssembly.FromAssembly(typeof(int).Assembly).LoadCecil(null);
// DuneCecilContext ctx = new();

// foreach (TypeDefinition type in nativeAssembly.Modules.SelectMany(module => module.Types)) {
//     DuneTypeDefinition typeDef = DuneTypeDefinition.FromTypeDefinition(type, ctx);

//     rules.AllowType(typeDef.Signature);

//     foreach (DuneMethodDefinition method in typeDef.Methods) rules.AllowMethod(method.Signature);
//     foreach (DuneFieldDefinition field in typeDef.Fields) rules.AllowField(field.Signature);

//     foreach (DuneEventDefinition @event in typeDef.Events) {
//         Console.WriteLine(@event.Signature);
//     }
// }

DuneScriptConfig config = new();
DuneAssemblyEnvironment env = new();
IDuneScriptReferenceResolver referenceResolver = new ReferenceResolver();

DuneSandboxRules rules = new();

rules.AllowMethod(DuneMethodSignature.FromDelegate((Action<string>)Console.WriteLine));

byte[] ruleBytes = DuneSandboxRules.Serialize(rules);
File.WriteAllBytes("rules.dune", ruleBytes);

DuneSandboxRules readRules = DuneSandboxRules.Deserialize(ruleBytes);

if (!rules.IsEquivalentTo(readRules))
    throw new Exception("Read rules not equivalent.");

Console.WriteLine(DuneSandboxRules.Stringify(readRules));

rules = readRules;

string path = "../Test.csx";
string source = File.ReadAllText(path);

DuneCompilationResult result = DuneScriptCompiler.Compile(
    config, env, referenceResolver, rules,
    source, path
);

if (result.HasDiagnostics) {

    foreach (DuneDiagnostic diagnostic in result.Diagnostics) {
        diagnostic.PrintToConsole();
    }
}

if (result.Success) {
    result.WriteToFile("Out.dll");
}

[AttributeUsage(AttributeTargets.Class)]
public sealed class MyTestAttribute : Attribute {

    public MyTestAttribute(Type type) { }
}

[MyTest(typeof(List<>))]
public class MyType {
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