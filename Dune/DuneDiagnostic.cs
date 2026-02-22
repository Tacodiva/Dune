
using System;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Dune;

public enum DuneDiagnosticSeverity {
    Error,
    Warning,
    Info
}

public abstract class DuneDiagnostic {

    public const string UnexpectedExceptionId = "DUNE00";
    public const string IllegalAssemblyId = "DUNE01";
    public const string IllegalTypeReferenceId = "DUNE02";
    public const string IllegalPointerId = "DUNE03";
    public const string IllegalFunctionPointerId = "DUNE04";
    public const string IllegalMethodId = "DUNE05";
    public const string IllegalFieldId = "DUNE06";
    public const string IllegalPropertyId = "DUNE07";
    public const string IllegalEventId = "DUNE08";
    public const string IllegalKeywordId = "DUNE09";

    public abstract DuneDiagnosticSeverity Severity { get; }
    public abstract string Id { get; }

    public abstract string GetMessage();
    public abstract string? GetLocation();

    public override string ToString() {
        StringBuilder sb = new();

        string? location = GetLocation();

        if (location != null)
            sb.Append($"{location} ");

        sb.Append($"{Severity.ToString().ToLower()} {Id}: {GetMessage()}");

        return sb.ToString(); ;
    }

    public virtual void PrintToConsole() {
        string? location = GetLocation();

        if (location != null)
            Console.Write($"{location} ");

        Console.ForegroundColor = Severity switch {
            DuneDiagnosticSeverity.Error => ConsoleColor.Red,
            DuneDiagnosticSeverity.Warning => ConsoleColor.Yellow,
            _ => ConsoleColor.White,
        };

        Console.Write($"\u001b[1m{Severity.ToString().ToLower()} {Id}\u001b[0m");
        Console.ResetColor();
        Console.Write($": {GetMessage()}\n");
    }

    internal static string FormatRoslynLocation(RoslynLocation location) {
        FileLinePositionSpan lineSpan = location.GetMappedLineSpan();

        if (!lineSpan.IsValid)
            return location.ToString();

        LinePosition position = lineSpan.StartLinePosition;

        // +1 to line because it starts at 0 and we want it to start at 1
        return $"{lineSpan.Path}({position.Line + 1},{position.Character})";
    }
}

public sealed class DuneUnexpectedErrorDiagnostic(Exception cause) : DuneDiagnostic {
    public Exception Cause { get; } = cause;
    public override DuneDiagnosticSeverity Severity => DuneDiagnosticSeverity.Error;
    public override string Id => UnexpectedExceptionId;

    public override string? GetLocation() => null;
    public override string GetMessage() => $"Unexpected exception. {Cause}";
}

public sealed class DuneRoslynDiagnostic(RoslynDiagnostic roslynDiagnostic) : DuneDiagnostic {
    public RoslynDiagnostic RoslynDiagnostic { get; } = roslynDiagnostic;

    public override DuneDiagnosticSeverity Severity {
        get {
            return RoslynDiagnostic.Severity switch {
                RoslynDiagnosticSeverity.Hidden or RoslynDiagnosticSeverity.Info => DuneDiagnosticSeverity.Info,
                RoslynDiagnosticSeverity.Warning => DuneDiagnosticSeverity.Warning,
                RoslynDiagnosticSeverity.Error => DuneDiagnosticSeverity.Error,
                _ => DuneDiagnosticSeverity.Error,
            };
        }
    }

    public override string Id => RoslynDiagnostic.Id;
    public override string GetMessage() => RoslynDiagnostic.GetMessage();
    public override string? GetLocation() => FormatRoslynLocation(RoslynDiagnostic.Location);

}


public sealed class DuneRoslynSandboxViolationDiagnostic(DuneSandboxRoslynViolation violation) : DuneDiagnostic {
    public RoslynLocation Location { get; } = violation.LocationNode.GetLocation();
    public DuneSandboxViolation Violation { get; } = violation.Violation;

    public override DuneDiagnosticSeverity Severity => DuneDiagnosticSeverity.Error;
    public override string Id => Violation.DiagnosticId;

    public override string? GetLocation() => FormatRoslynLocation(Location);
    public override string GetMessage() => Violation.GetMessage();
}

public sealed class DuneCecilSandboxViolationDiagnostic(DuneSandboxCecilViolation violation) : DuneDiagnostic {

    public DuneSandboxCecilViolation CecilViolation { get; } = violation;
    public DuneSandboxViolation Violation => CecilViolation.Violation;

    public override DuneDiagnosticSeverity Severity => DuneDiagnosticSeverity.Error;
    public override string Id => Violation.DiagnosticId;

    public override string? GetLocation() {
        CecilSequencePoint? sequencePoint = CecilViolation.SequencePoint;

        if (sequencePoint == null) return null;

        if (sequencePoint.StartLine >= 0xfeefee) {
            // Magic number used by PDB to indicate a hidden (?) line
            // (for example, used by compiler generated code)

            return sequencePoint.Document.Url;
        }

        return $"{sequencePoint.Document.Url}({sequencePoint.StartLine},{sequencePoint.StartColumn})";
    }

    public override string GetMessage() {

        string prefix;
        if (CecilViolation.LocationMemberName == null) prefix = "Violation in the assembly";
        else prefix = $"Violation in {CecilViolation.LocationMemberName}";

        if (CecilViolation.Locations.Length == 0) {
            return $"{prefix}. {Violation.GetMessage()}";
        }

        StringBuilder sb = new($"{prefix}'s ");

        for (int i = 0; i < CecilViolation.Locations.Length; i++) {
            if (i != 0) sb.Append("'s ");
            sb.Append(StringifyViolationLocation(CecilViolation.Locations[i]));
        }

        sb.Append(". ");
        sb.Append(Violation.GetMessage());

        return sb.ToString();
    }

    private static string StringifyViolationLocation(DuneSandboxCecilViolationLocation location) {
        return location switch {
            DuneSandboxCecilViolationLocation.AssemblyReference => "assembly reference",
            DuneSandboxCecilViolationLocation.AttributeConstructor => "attribute's constructor",
            DuneSandboxCecilViolationLocation.AttributeArgument => "attribute's argument",
            DuneSandboxCecilViolationLocation.GenericParameter => "generic parameter",
            DuneSandboxCecilViolationLocation.GenericConstraint => "generic constraint",
            DuneSandboxCecilViolationLocation.TypeBase => "base type",
            DuneSandboxCecilViolationLocation.TypeInterface => "interface implementation",
            DuneSandboxCecilViolationLocation.MethodReturnType => "return type",
            DuneSandboxCecilViolationLocation.MethodParameter => "parameter",
            DuneSandboxCecilViolationLocation.MethodBody => "body",
            DuneSandboxCecilViolationLocation.FieldType => "type",
            DuneSandboxCecilViolationLocation.PropertyType => "type",
            DuneSandboxCecilViolationLocation.PropertySetMethod => "set method",
            DuneSandboxCecilViolationLocation.PropertyGetMethod => "get method",
            DuneSandboxCecilViolationLocation.EventType => "type",
            DuneSandboxCecilViolationLocation.EventAddMethod => "add method",
            DuneSandboxCecilViolationLocation.EventRaiseMethod => "raise method",
            DuneSandboxCecilViolationLocation.EventRemoveMethod => "remove method",
            _ => "something",
        };
    }

}