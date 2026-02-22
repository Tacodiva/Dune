
#if NETSTANDARD2_0

using System;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Dune.Analyzers;

#pragma warning disable RS2008, RS1036

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DuneSanboxAnalyzer : DiagnosticAnalyzer {
    private static DiagnosticDescriptor CreateSandboxDiagnosticDescriptor(
        string id,
        string title,
        string description
    ) {
        return new(
            id: id,
            title: title,
            description: description,
            category: "Sandbox",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            messageFormat: "{0}"
        );
    }

    private static readonly DiagnosticDescriptor _AssemblyViolation = CreateSandboxDiagnosticDescriptor(
        id: DuneDiagnostic.IllegalAssemblyId,
        title: "The referenced assembly is not allowed",
        description: "The sandbox rules of the current environment do not allow the referenced assembly."
    );

    private static readonly DiagnosticDescriptor _TypeViolation = CreateSandboxDiagnosticDescriptor(
        id: DuneDiagnostic.IllegalTypeReferenceId,
        title: "The referenced type is not allowed",
        description: "The sandbox rules of the current environment do not allow the referenced type."
    );

    private static readonly DiagnosticDescriptor _PointerViolation = CreateSandboxDiagnosticDescriptor(
        id: DuneDiagnostic.IllegalPointerId,
        title: "The pointer is not allowed",
        description: "The sandbox rules of the current environment do not allow that pointer type."
    );

    private static readonly DiagnosticDescriptor _FunctionPointerViolation = CreateSandboxDiagnosticDescriptor(
        id: DuneDiagnostic.IllegalFunctionPointerId,
        title: "The function pointer is not allowed",
        description: "The sandbox rules of the current environment do not allow that function pointer type."
    );

    private static readonly DiagnosticDescriptor _MethodViolation = CreateSandboxDiagnosticDescriptor(
        id: DuneDiagnostic.IllegalMethodId,
        title: "The referenced method is not allowed",
        description: "The sandbox rules of the current environment do not allow the referenced method."
    );

    private static readonly DiagnosticDescriptor _FieldViolation = CreateSandboxDiagnosticDescriptor(
        id: DuneDiagnostic.IllegalFieldId,
        title: "The referenced field is not allowed",
        description: "The sandbox rules of the current environment do not allow the referenced field."
    );

    private static readonly DiagnosticDescriptor _PropertyViolation = CreateSandboxDiagnosticDescriptor(
        id: DuneDiagnostic.IllegalPropertyId,
        title: "The referenced property is not allowed",
        description: "The sandbox rules of the current environment do not allow the referenced property."
    );

    private static readonly DiagnosticDescriptor _EventViolation = CreateSandboxDiagnosticDescriptor(
        id: DuneDiagnostic.IllegalEventId,
        title: "The referenced event is not allowed",
        description: "The sandbox rules of the current environment do not allow the referenced event."
    );

    private static readonly DiagnosticDescriptor _KeywordViolation = CreateSandboxDiagnosticDescriptor(
        id: DuneDiagnostic.IllegalKeywordId,
        title: "The keyword is not allowed",
        description: "The sandbox rules of the current environment do not allow that keyword."
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [
        _AssemblyViolation,
        _TypeViolation,
        _PointerViolation,
        _FunctionPointerViolation,
        _MethodViolation,
        _FieldViolation,
        _PropertyViolation,
        _EventViolation,
        _KeywordViolation
    ];

    public override void Initialize(AnalysisContext context) {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze);
        context.RegisterSemanticModelAction(Analyze);
    }

    private static void Analyze(SemanticModelAnalysisContext context) {
        SemanticModel semantics = context.SemanticModel;
        SyntaxTree tree = semantics.SyntaxTree;

        IDuneSandboxRules rules = DuneSandboxTestRules.Instance;

        ImmutableArray<DuneSandboxRoslynViolation> violations = rules.CheckSyntaxTree(tree, semantics, null);

        foreach (DuneSandboxRoslynViolation violation in violations) {

            DiagnosticDescriptor violationDescriptor = violation.Violation.Category switch {
                DuneSandboxViolationCategory.Assembly => _AssemblyViolation,
                DuneSandboxViolationCategory.Type => _TypeViolation,
                DuneSandboxViolationCategory.Pointer => _PointerViolation,
                DuneSandboxViolationCategory.FunctionPointer => _FunctionPointerViolation,
                DuneSandboxViolationCategory.Method => _MethodViolation,
                DuneSandboxViolationCategory.Field => _FieldViolation,
                DuneSandboxViolationCategory.Property => _PropertyViolation,
                DuneSandboxViolationCategory.Event => _EventViolation,
                DuneSandboxViolationCategory.Keyword => _KeywordViolation,
                _ => throw new InvalidEnumArgumentException()
            };

            Diagnostic.Create(violationDescriptor, violation.LocationNode.GetLocation(), violation.Violation.GetMessage());
        }

    }
}

#endif