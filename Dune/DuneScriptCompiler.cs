
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
    public DuneAssemblyReference AssemblyReference { get; }
    public CecilAssemblyDefinition Assembly { get; }
    public ImmutableArray<SemanticModel> SemanticModels { get; }

    private readonly List<DuneDiagnostic> _diagnostics;

    internal DuneCompilationProcessorContext(CecilAssemblyDefinition assembly, DuneAssemblyReference assemblyReference, ImmutableArray<SemanticModel> semanticModels, List<DuneDiagnostic> diagnostics) {
        Assembly = assembly;
        AssemblyReference = assemblyReference;
        SemanticModels = semanticModels;
        _diagnostics = diagnostics;
    }

    public void AddDiagnostic(DuneDiagnostic diagnostic) {
        _diagnostics.Add(diagnostic);
    }

    public CecilTypeDefinition? TryGetTypeSignature(DuneTypeSignature targetType) {
        if (!AssemblyReference.Matches(targetType.Assembly))
            return null;

        bool IsMatch(CecilTypeDefinition type) {
            if (type.Name != targetType.RawName)
                return false;

            if (targetType.DeclaringType == null) {

                // We only check the namespace if the target type does not have a decalring type.
                //   This is because in cecil, inner types do not have their namespace set.

                string? typeNamespace = type.Namespace;
                if (string.IsNullOrWhiteSpace(typeNamespace)) typeNamespace = null;

                if (typeNamespace != targetType.Namespace)
                    return false;
            }

            if (type.GenericParameters.Count != targetType.GenericParameterCount)
                return false;

            return true;
        }

        if (targetType.DeclaringType == null) {
            foreach (CecilModuleDefinition module in Assembly.Modules) {
                foreach (CecilTypeDefinition type in module.Types) {
                    if (IsMatch(type))
                        return type;
                }
            }

            return null;
        } else {
            CecilTypeDefinition? declaringType = TryGetTypeSignature(targetType.DeclaringType);

            if (declaringType == null)
                return null;

            foreach (CecilTypeDefinition innerType in declaringType.NestedTypes) {
                if (IsMatch(innerType))
                    return innerType;
            }

            return null;
        }

    }

    public CustomAttribute AddAttribute(CecilCustomAttributeProvider member, CecilMethodDefinition attributeConstructor, params object[] constructorParameters) {
        CustomAttribute attribute = new(attributeConstructor);

        foreach (object constructorParameter in constructorParameters) {
            attribute.ConstructorArguments.Add(new(
                Assembly.MainModule.ImportReference(constructorParameter.GetType()),
                constructorParameter
            ));
        }

        member.CustomAttributes.Add(attribute);

        return attribute;
    }

    public CecilMethodDefinition DefineAttribute(string @namespace, string name, AttributeTargets targets, params (Type Type, string Name)[] constructorParameters) {
        CecilTypeDefinition attrDef = new(
            @namespace, name,
            TypeAttributes.AnsiClass | TypeAttributes.BeforeFieldInit | TypeAttributes.Public | TypeAttributes.Sealed,
            Assembly.MainModule.ImportReference(typeof(System.Attribute))
        );

        (CecilTypeReference Type, string Name)[] resolvedParameters =
            [.. constructorParameters.Select(param => (Assembly.MainModule.ImportReference(param.Type), param.Name))];

        // Add CompilerGenerated, Embedded and AttributeUsage attributes to ScriptEntrypointAttribute
        {
            CustomAttribute attrCompilerGenerated = new(
                Assembly.MainModule.ImportReference(typeof(CompilerGeneratedAttribute).GetConstructor([]))
            );

            CustomAttribute attrAttributeUsage = new(
                Assembly.MainModule.ImportReference(typeof(AttributeUsageAttribute).GetConstructor([typeof(AttributeTargets)]))
            );

            attrAttributeUsage.ConstructorArguments.Add(new(
                Assembly.MainModule.ImportReference(typeof(AttributeTargets)),
                targets
            ));

            attrDef.CustomAttributes.Add(attrCompilerGenerated);
            attrDef.CustomAttributes.Add(attrAttributeUsage);
        }

        // Write the blank constructor
        CecilMethodDefinition attrCtorDef = new(
            ".ctor",
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.RTSpecialName | MethodAttributes.SpecialName,
            Assembly.MainModule.TypeSystem.Void
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
            il.Emit(OpCodes.Call, Assembly.MainModule.ImportReference(typeof(Attribute).GetConstructor(DuneReflectionContext.EverythingFlags, null, [], [])));

            for (int i = 0; i < resolvedParameters.Length; i++) {
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldarg, i + 1);
                il.Emit(OpCodes.Stfld, parameterFields[i]);
            }

            il.Emit(OpCodes.Ret);

            attrDef.Methods.Add(attrCtorDef);
        }

        Assembly.MainModule.Types.Add(attrDef);

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

            DuneAssemblyReference assemblyReference = DuneAssemblyReference.FromSymbol(compilation.Assembly);

            List<DuneDiagnostic> diagnostics = [];

            ImmutableArray<SemanticModel> semanticModels = [.. compilation.SyntaxTrees.Select(
                tree => compilation.GetSemanticModel(tree, true)
            )];

            {
                bool success = true;

                foreach (SemanticModel semantics in semanticModels) {

                    ImmutableArray<DuneSandboxRoslynViolation> violations = DuneSandboxEnforcer.CheckSyntaxTree(sandboxRules, semantics.SyntaxTree, semantics, assemblyReference);

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

            // Sanity check: The compiled assembly's reference should be identical to the refernece Roslyn told us 
            Debug.Assert(DuneAssemblyReference.FromAssemblyDefinition(cecilAssembly) == assemblyReference);

            ImmutableArray<DuneSandboxCecilViolation> duneViolations = DuneSandboxEnforcer.CheckCecilAssemblyDefinition(sandboxRules, cecilAssembly);
            diagnostics.AddRange(duneViolations.Select(violation => new DuneCecilSandboxViolationDiagnostic(violation)));

            if (duneViolations.Length != 0) return DuneCompilationResult.FromDiagnostics(diagnostics);

            DuneCompilationProcessorContext processorCtx = new(cecilAssembly, assemblyReference, semanticModels, diagnostics);

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
            AttributeTargets.Class | AttributeTargets.Struct,
            (typeof(string[]), "sourcePaths"),
            (typeof(int[]), "sourceLines"),
            (typeof(int[]), "sourceCharacter")
        );

        void AddSourceInfo(CecilCustomAttributeProvider dst, RoslynLocation location) {
            CustomAttribute? sourceInfoAttribute = dst.CustomAttributes
                .FirstOrDefault(attribute => attribute.Constructor == sourceInfoAttributeConstructor);

            sourceInfoAttribute ??= ctx.AddAttribute(
                dst, sourceInfoAttributeConstructor,
                Array.Empty<CustomAttributeArgument>(),
                Array.Empty<CustomAttributeArgument>(),
                Array.Empty<CustomAttributeArgument>()
            );

            InternalUtils.Assert(sourceInfoAttribute.ConstructorArguments.Count == 3);

            void AddToConstructorArgumentArray(int index, object value) {
                CustomAttributeArgument oldArg = sourceInfoAttribute.ConstructorArguments[index];
                sourceInfoAttribute.ConstructorArguments[index] = new(
                    oldArg.Type,
                    ((CustomAttributeArgument[])oldArg.Value)
                        .Append(new(((ArrayType)oldArg.Type).ElementType, value))
                        .ToArray()
                );
            }

            FileLinePositionSpan lineSpan = location.GetMappedLineSpan();

            AddToConstructorArgumentArray(0, lineSpan.Path);
            AddToConstructorArgumentArray(1, lineSpan.StartLinePosition.Line + 1);
            AddToConstructorArgumentArray(2, lineSpan.StartLinePosition.Character);
        }

        foreach (SemanticModel treeSemantics in ctx.SemanticModels) {
            SyntaxTree syntaxTree = treeSemantics.SyntaxTree;

            foreach (SyntaxNode syntaxNode in syntaxTree.GetRoot().DescendantNodesAndSelf()) {
                if (syntaxNode is TypeDeclarationSyntax) {
                    ISymbol? classDeclr = treeSemantics.GetDeclaredSymbol(syntaxNode);

                    if (classDeclr is INamedTypeSymbol classDeclrNamed) {
                        DuneTypeSignature declaredType = DuneTypeSignature.FromSymbol(classDeclrNamed);
                        CecilTypeDefinition? declaredTypeDefinition = ctx.TryGetTypeSignature(declaredType);

                        if (declaredTypeDefinition != null) 
                            AddSourceInfo(declaredTypeDefinition, syntaxNode.GetLocation());
                    }
                }
            }
        }
    }


}