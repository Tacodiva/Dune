
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Pdb;


namespace Dune;

public sealed record class DuneCompilationResult(
    DuneAssembly? Assembly,
    byte[]? Pdb,
    ImmutableArray<DuneDiagnostic> Diagnostics
) {
    public static DuneCompilationResult FromUnexpectedException(Exception e)
        => new(null, null, [new DuneUnexpectedErrorDiagnostic(e)]);

    public static DuneCompilationResult FromDiagnostics(IEnumerable<DuneDiagnostic> diagnostics)
        => new(null, null, [.. diagnostics]);

    public bool Success => Assembly != null;
    public bool HasDiagnostics => !Diagnostics.IsEmpty;

    public void WriteToFile(string path, bool includePdb = true) {
        if (!Success)
            throw new InvalidOperationException("Cannot write result to a file as compilation was not successful.");

        File.WriteAllBytes(path, Assembly!.GetBytes());

        if (includePdb && Pdb != null) {
            File.WriteAllBytes(
                Path.ChangeExtension(path, "pdb"),
                Pdb
            );
        }
    }
}

public interface IDuneScriptReferenceResolver {

    /// <summary>
    /// Normalizes a path. Returns the normalized path.
    /// </summary>
    public string? TryNormalizeSourceReference(string reference, string? baseFilePath);

    /// <summary>
    /// Attempts to load the source code from a file. Returns a string containing the source code.
    /// </summary>
    public string? TryResolveSourceReference(string normalizedReference);

    public DuneAssembly? TryResolveAssemblyReference(string reference, string? baseFilePath);
}

public sealed class DuneCompilationProcessorContext {

    public DuneAssemblyDefinition DuneAssembly { get; }
    public DuneAssemblyReference AssemblyReference => DuneAssembly.Reference;

    public CecilAssemblyDefinition CecilAssembly { get; }
    public DuneCecilContext CecilContext { get; }

    public IAssemblySymbol RoslynAssembly { get; }
    public ImmutableArray<SemanticModel> SemanticModels { get; }
    public DuneRoslynContext RoslynContext { get; }

    private readonly List<DuneDiagnostic> _diagnostics;

    internal DuneCompilationProcessorContext(
        DuneAssemblyDefinition duneAssembly,
        CecilAssemblyDefinition assembly,
        DuneCecilContext cecilContext,
        IAssemblySymbol roslynAssembly,
        ImmutableArray<SemanticModel> semanticModels,
        DuneRoslynContext roslynContext,
        List<DuneDiagnostic> diagnostics) {

        DuneAssembly = duneAssembly;

        CecilAssembly = assembly;
        CecilContext = cecilContext;

        RoslynAssembly = roslynAssembly;
        SemanticModels = semanticModels;
        RoslynContext = roslynContext;

        _diagnostics = diagnostics;
    }

    public void AddDiagnostic(DuneDiagnostic diagnostic) {
        _diagnostics.Add(diagnostic);
    }

    public IEnumerable<(T Node, SemanticModel Semantics)> EnumerateSyntaxNodes<T>() where T : SyntaxNode {
        foreach (SemanticModel semantics in SemanticModels) {
            foreach (SyntaxNode node in semantics.SyntaxTree.GetRoot().DescendantNodesAndSelf()) {
                if (node is T nodeT) yield return (nodeT, semantics);
            }
        }
    }

    public CecilCustomAttribute AddAttribute(CecilCustomAttributeProvider member, CecilMethodDefinition attributeConstructor, params object[] constructorParameters) {
        CecilCustomAttribute attribute = new(attributeConstructor);

        foreach (object constructorParameter in constructorParameters) {
            attribute.ConstructorArguments.Add(new(
                CecilAssembly.MainModule.ImportReference(constructorParameter.GetType()),
                constructorParameter
            ));
        }

        member.CustomAttributes.Add(attribute);

        return attribute;
    }

    public CecilMethodDefinition DefineAttribute(string @namespace, string name, AttributeUsageAttribute usageAttribute, params (Type Type, string Name)[] constructorParameters) {
        CecilTypeDefinition attrDef = new(
            @namespace, name,
            TypeAttributes.AnsiClass | TypeAttributes.BeforeFieldInit | TypeAttributes.Public | TypeAttributes.Sealed,
            CecilAssembly.MainModule.ImportReference(typeof(System.Attribute))
        );

        (CecilTypeReference Type, string Name)[] resolvedParameters =
            [.. constructorParameters.Select(param => (CecilAssembly.MainModule.ImportReference(param.Type), param.Name))];

        // Add CompilerGenerated, Embedded and AttributeUsage attributes to ScriptEntrypointAttribute
        {
            CecilCustomAttribute attrCompilerGenerated = new(
                CecilAssembly.MainModule.ImportReference(typeof(CompilerGeneratedAttribute).GetConstructor([]))
            );

            CecilCustomAttribute attrAttributeUsage = new(
                CecilAssembly.MainModule.ImportReference(typeof(AttributeUsageAttribute).GetConstructor([typeof(AttributeTargets)]))
            );

            attrAttributeUsage.ConstructorArguments.Add(new(
                CecilAssembly.MainModule.ImportReference(typeof(AttributeTargets)),
                usageAttribute.ValidOn
            ));

            attrAttributeUsage.Properties.Add(new(
                nameof(AttributeUsageAttribute.AllowMultiple),
                new(
                    CecilAssembly.MainModule.TypeSystem.Boolean,
                    usageAttribute.AllowMultiple
                )
            ));

            attrAttributeUsage.Properties.Add(new(
                nameof(AttributeUsageAttribute.Inherited),
                new(
                    CecilAssembly.MainModule.TypeSystem.Boolean,
                    usageAttribute.Inherited
                )
            ));

            attrDef.CustomAttributes.Add(attrCompilerGenerated);
            attrDef.CustomAttributes.Add(attrAttributeUsage);
        }

        // Write the blank constructor
        CecilMethodDefinition attrCtorDef = new(
            ".ctor",
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.RTSpecialName | MethodAttributes.SpecialName,
            CecilAssembly.MainModule.TypeSystem.Void
        );

        {
            CecilFieldDefinition[] parameterFields = new CecilFieldDefinition[resolvedParameters.Length];

            for (int i = 0; i < resolvedParameters.Length; i++) {

                (CecilTypeReference paramType, string paramName) = resolvedParameters[i];

                // Add our parameters
                attrCtorDef.Parameters.Add(
                    new(paramName, ParameterAttributes.None, paramType)
                );

                // And fields to the type
                parameterFields[i] = new(paramName, FieldAttributes.Public | FieldAttributes.InitOnly, paramType);
                attrDef.Fields.Add(parameterFields[i]);
            }

            ILProcessor il = attrCtorDef.Body.GetILProcessor();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, CecilAssembly.MainModule.ImportReference(typeof(Attribute).GetConstructor(DuneReflectionContext.EverythingFlags, null, [], [])));

            for (int i = 0; i < resolvedParameters.Length; i++) {
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldarg, i + 1);
                il.Emit(OpCodes.Stfld, parameterFields[i]);
            }

            il.Emit(OpCodes.Ret);

            attrDef.Methods.Add(attrCtorDef);
        }

        CecilAssembly.MainModule.Types.Add(attrDef);

        return attrCtorDef;
    }

}

public static class DuneScriptCompiler {

    private sealed class MetadataReferenceResolverImpl(DuneAssemblyProvider assemblyProvider, IDuneScriptReferenceResolver referenceResolver) : MetadataReferenceResolver {
        public readonly DuneAssemblyProvider AssemblyProvider = assemblyProvider;
        public readonly IDuneScriptReferenceResolver ReferenceResolver = referenceResolver;

        public override bool ResolveMissingAssemblies => true;

        public override ImmutableArray<PortableExecutableReference> ResolveReference(string reference, string? baseFilePath, MetadataReferenceProperties properties) {
            DuneAssembly? assembly = ReferenceResolver.TryResolveAssemblyReference(reference, baseFilePath);
            if (assembly == null) return [];
            return [assembly.GetPortableExecutableReference()];
        }

        public override PortableExecutableReference? ResolveMissingAssembly(MetadataReference definition, AssemblyIdentity referenceIdentity) {
            DuneAssembly? assembly = AssemblyProvider.TryGetAssembly(DuneAssemblyReference.FromAssemblyIdentity(referenceIdentity));
            return assembly?.GetPortableExecutableReference() ?? null;
        }

        public override bool Equals(object? other) {
            if (other is not MetadataReferenceResolverImpl impl)
                return false;
            return Equals(impl);
        }

        public bool Equals(MetadataReferenceResolverImpl other) {
            return AssemblyProvider.Equals(other.AssemblyProvider) && ReferenceResolver.Equals(other.ReferenceResolver);
        }

        public override int GetHashCode() {
            return InternalUtils.HashCodeCombine(AssemblyProvider, ReferenceResolver);
        }
    }

    private sealed class SourceReferenceResolverImpl(IDuneScriptReferenceResolver referenceResolver) : SourceReferenceResolver {
        public readonly IDuneScriptReferenceResolver ReferenceResolver = referenceResolver;

        public override string? NormalizePath(string path, string? baseFilePath) {
            return ReferenceResolver.TryNormalizeSourceReference(path, baseFilePath);
        }

        public override string? ResolveReference(string path, string? baseFilePath) {
            return ReferenceResolver.TryNormalizeSourceReference(path, baseFilePath);
        }

        public override Stream OpenRead(string resolvedPath) {
            string? str = ReferenceResolver.TryResolveSourceReference(resolvedPath);

            if (str == null) throw new IOException("File was not found or was inaccessible.");

            return new MemoryStream(Encoding.Default.GetBytes(str));
        }

        public override SourceText ReadText(string resolvedPath) {
            string? str = ReferenceResolver.TryResolveSourceReference(resolvedPath);

            if (str == null) throw new IOException("File was not found or was inaccessible.");

            return SourceText.From(str, Encoding.Default);
        }

        public override bool Equals(object? other) {
            if (other is not SourceReferenceResolverImpl impl)
                return false;
            return Equals(impl);
        }

        public bool Equals(SourceReferenceResolverImpl other) {
            return ReferenceResolver.Equals(other.ReferenceResolver);
        }

        public override int GetHashCode() {
            return ReferenceResolver.GetHashCode();
        }
    }

    public static DuneCompilationResult Compile(
        DuneScriptConfig config,
        DuneAssemblyProvider assemblyProvider,
        IDuneScriptReferenceResolver referenceResolver,
        IDuneSandboxRules sandboxRules,
        string script, string? path
    ) {

        try {
            CSharpParseOptions parseOptions = new(
                kind: SourceCodeKind.Script,
                preprocessorSymbols: config.PreprocessorSymbols,
                documentationMode: DocumentationMode.None
            );

            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(
                script, parseOptions, path ?? "", Encoding.Default
            );

            CSharpCompilationOptions compilationOptions = new(
                outputKind: OutputKind.DynamicallyLinkedLibrary,

                usings: config.ImplicitUsings,
                allowUnsafe: sandboxRules.AllowUnsafe,
                optimizationLevel: config.OptimizationLevel,
                nullableContextOptions: config.NullableContextOptions,
                checkOverflow: config.CheckIntegerMath,

                scriptClassName: "DuneScript",
                moduleName: "DuneModule",

                sourceReferenceResolver: new SourceReferenceResolverImpl(referenceResolver),
                metadataReferenceResolver: new MetadataReferenceResolverImpl(assemblyProvider, referenceResolver)
            );

            CSharpCompilation compilation = CSharpCompilation.CreateScriptCompilation(
                assemblyName: "DuneAssembly",
                syntaxTree: syntaxTree,
                references: config.ReferencedAssemblies.Select(assembly => assembly.GetPortableExecutableReference()),
                returnType: config.ReturnType,
                globalsType: config.GlobalsType,
                options: compilationOptions
            );

            return Compile(compilation, assemblyProvider, sandboxRules, config.GeneratePDB);

        } catch (Exception e) {
            return DuneCompilationResult.FromUnexpectedException(e);
        }
    }

    public static DuneCompilationResult Compile(
        CSharpCompilation compilation,
        DuneAssemblyProvider assemblyProvider,
        IDuneSandboxRules sandboxRules,
        bool generatePDB = false
    ) {
        try {
            DuneRoslynContext roslynCtx = new();
            DuneAssemblyReference assemblyReference = DuneAssemblyReference.FromSymbol(compilation.Assembly, roslynCtx);

            List<DuneDiagnostic> diagnostics = [];

            ImmutableArray<SemanticModel> semanticModels = [.. compilation.SyntaxTrees.Select(
                tree => compilation.GetSemanticModel(tree, true)
            )];

            {
                bool success = true;

                foreach (SemanticModel semantics in semanticModels) {

                    ImmutableArray<DuneSandboxRoslynViolation> violations = DuneSandboxEnforcer.CheckSyntaxTree(
                        sandboxRules,
                        semantics.SyntaxTree, semantics,
                        assemblyReference, roslynCtx
                    );

                    if (violations.Length != 0) {
                        diagnostics.AddRange(violations.Select(violation => new DuneRoslynSandboxViolationDiagnostic(violation)));
                        success = false;
                    }
                }

                if (!success) return DuneCompilationResult.FromDiagnostics(diagnostics);
            }

            using MemoryStream roslynPeStream = new();
            using MemoryStream? roslynPdbStream = generatePDB ? new() : null;

            EmitResult roslynResult = compilation.Emit(
                roslynPeStream, roslynPdbStream,

                options: new(
                    debugInformationFormat: DebugInformationFormat.PortablePdb
                )
            );

            diagnostics.AddRange(roslynResult.Diagnostics.Select(diagnostic => new DuneRoslynDiagnostic(diagnostic)));

            if (!roslynResult.Success) return DuneCompilationResult.FromDiagnostics(diagnostics);

            roslynPeStream.Seek(0, SeekOrigin.Begin);
            roslynPdbStream?.Seek(0, SeekOrigin.Begin);

            using CecilAssemblyDefinition cecilAssembly = CecilAssemblyDefinition.ReadAssembly(
                roslynPeStream,
                new() {
                    ReadingMode = ReadingMode.Immediate,
                    InMemory = true,

                    ReadSymbols = roslynPdbStream != null,
                    SymbolStream = roslynPdbStream,
                    SymbolReaderProvider = new PdbReaderProvider(),

                    AssemblyResolver = assemblyProvider.GetAssemblyResolver()
                }
            );

            DuneCecilContext cecilCtx = new();

            // Sanity check: The compiled assembly's reference should be identical to the refernece Roslyn told us 
            Debug.Assert(DuneAssemblyReference.FromCecilDefinition(cecilAssembly, cecilCtx) == assemblyReference);

            ImmutableArray<DuneSandboxCecilViolation> duneViolations = DuneSandboxEnforcer.CheckCecilAssemblyDefinition(sandboxRules, cecilAssembly, cecilCtx);
            diagnostics.AddRange(duneViolations.Select(violation => new DuneCecilSandboxViolationDiagnostic(violation)));

            if (duneViolations.Length != 0) return DuneCompilationResult.FromDiagnostics(diagnostics);

            DuneAssemblyDefinition duneDefiniton = DuneAssemblyDefinition.FromCecilDefinition(cecilAssembly, cecilCtx);

            DuneCompilationProcessorContext processorCtx = new(
                duneDefiniton,
                cecilAssembly, cecilCtx,
                compilation.Assembly, semanticModels, roslynCtx,
                diagnostics
            );

            AddSourceInfoAttributes(processorCtx);

            // Not sure if we can reuse the old streams here so we'll make new ones
            // (cecil might read from the input because of lazy initilization while writing the output)
            using MemoryStream cecilPeStream = new();
            using MemoryStream? cecilPdbStream = generatePDB ? new() : null;

            cecilAssembly.Write(cecilPeStream, new() {
                WriteSymbols = generatePDB,
                SymbolStream = cecilPdbStream,
                SymbolWriterProvider = new PdbWriterProvider(),
                DeterministicMvid = true
            });

            byte[] cecilPeBytes = cecilPeStream.ToArray();
            byte[]? cecilPdbBytes = cecilPdbStream?.ToArray();

            DuneAssembly assembly = DuneAssembly.FromBytes(cecilPeBytes, null);

            return new(assembly, cecilPdbBytes, [.. diagnostics]);

        } catch (Exception e) {
            return DuneCompilationResult.FromUnexpectedException(e);
        }
    }

    private static void AddSourceInfoAttributes(DuneCompilationProcessorContext ctx) {
        CecilMethodDefinition sourceInfoAttributeConstructor = ctx.DefineAttribute(
            "Dune.CompilerServices", "SourceInfoAttribute",
            new(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum | AttributeTargets.Interface) {
                AllowMultiple = true,
                Inherited = false
            },
            (typeof(string), "sourcePath"),
            (typeof(int), "sourceLine"),
            (typeof(int), "sourceCharacte")
        );

        void AddSourceInfo(CecilCustomAttributeProvider dst, RoslynLocation location) {
            FileLinePositionSpan lineSpan = location.GetMappedLineSpan();
            
            ctx.AddAttribute(
                dst, sourceInfoAttributeConstructor,
                lineSpan.Path,
                lineSpan.StartLinePosition.Line + 1,
                lineSpan.StartLinePosition.Character
            );
        }

        foreach ((TypeDeclarationSyntax declr, SemanticModel semantics) in ctx.EnumerateSyntaxNodes<TypeDeclarationSyntax>()) {
            ISymbol? classDeclr = semantics.GetDeclaredSymbol(declr);

            if (classDeclr is INamedTypeSymbol classDeclrNamed) {
                DuneTypeSignature declaredType = DuneTypeSignature.FromSymbol(classDeclrNamed);
                CecilTypeDefinition? declaredTypeDefinition = ctx.CecilAssembly.TryGetTypeDefinition(declaredType);

                if (declaredTypeDefinition != null)
                    AddSourceInfo(declaredTypeDefinition, declr.GetLocation());
            }

        }
    }


}