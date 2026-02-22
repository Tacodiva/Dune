
using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Dune;

public enum DuneSandboxViolationLocation {
    DeclaringAssembly,
    DeclaringType,

    GenericArgument,

    MethodReturn,
    MethodParameter,

    FieldType,

    PropertyType,
    PropertySetMethod,
    PropertyGetMethod,

    EventType,
    EventAddMethod,
    EventRaiseMethod,
    EventRemoveMethod,

    FunctionPointerParameter,
    FunctionPointerReturn,
}

public enum DuneSandboxViolationCategory {
    Assembly,
    Type,
    Pointer,
    FunctionPointer,
    Method,
    Field,
    Property,
    Event,
    Keyword
}

public sealed record class DuneSandboxViolationCause(
    DuneSandboxViolationLocation Location, DuneSandboxViolation Violation
);


public abstract class DuneSandboxViolation(DuneSandboxViolationCause? cause) {
    public DuneSandboxViolationCause? Cause { get; } = cause;
    public abstract string DiagnosticId { get; }
    public abstract DuneSandboxViolationCategory Category { get; }

    public string GetMessage() {
        StringBuilder sb = new();

        string baseLocationName = GetDecoratedLocationName();

        sb.Append(char.ToUpperInvariant(baseLocationName[0]));
        sb.Append(baseLocationName[1..]);

        if (Cause == null) {
            sb.Append(" is not allowed.");
        } else {
            sb.Append(" is not allowed because it");

            DuneSandboxViolationCause? cause = Cause;

            for (; ; ) {
                DuneSandboxViolation causeViolation = cause.Violation;

                sb.Append($"'s {StringifyViolationLocation(cause.Location)}");

                if (causeViolation.Cause == null) {
                    sb.Append($" ('{causeViolation.GetRawLocationName()}') is not allowed.");
                    break;
                }

                cause = causeViolation.Cause;
            }
        }

        return sb.ToString();
    }

    private static string StringifyViolationLocation(DuneSandboxViolationLocation location) {
        return location switch {
            DuneSandboxViolationLocation.DeclaringAssembly => "declaring assembly",
            DuneSandboxViolationLocation.DeclaringType => "declaring type",
            DuneSandboxViolationLocation.GenericArgument => "generic argument",
            DuneSandboxViolationLocation.MethodReturn => "return type",
            DuneSandboxViolationLocation.MethodParameter => "parameter type",
            DuneSandboxViolationLocation.FieldType => "type",
            DuneSandboxViolationLocation.PropertyType => "type",
            DuneSandboxViolationLocation.PropertySetMethod => "set method",
            DuneSandboxViolationLocation.PropertyGetMethod => "get method",
            DuneSandboxViolationLocation.EventType => "delegate type",
            DuneSandboxViolationLocation.EventAddMethod => "add method",
            DuneSandboxViolationLocation.EventRaiseMethod => "raise method",
            DuneSandboxViolationLocation.EventRemoveMethod => "remove method",
            DuneSandboxViolationLocation.FunctionPointerParameter => "parameter type",
            DuneSandboxViolationLocation.FunctionPointerReturn => "return type",
            _ => "something",
        };
    }

    protected abstract string GetDecoratedLocationName();
    protected abstract string GetRawLocationName();
}

public sealed class DuneSandboxViolationAssembly(DuneAssemblyReference illegalAssembly, DuneSandboxViolationCause? cause) : DuneSandboxViolation(cause) {
    public DuneAssemblyReference IllegalAssembly { get; } = illegalAssembly;

    protected override string GetDecoratedLocationName() => $"the assembly '{IllegalAssembly.Name}'";
    protected override string GetRawLocationName() => IllegalAssembly.Name;

    public override string DiagnosticId => DuneDiagnostic.IllegalAssemblyId;
    public override DuneSandboxViolationCategory Category => DuneSandboxViolationCategory.Assembly;
}

public class DuneSandboxViolationTypeReference(DuneTypeReference illegalType, DuneSandboxViolationCause? cause) : DuneSandboxViolation(cause) {
    public DuneTypeReference IllegalType { get; } = illegalType;

    protected override string GetDecoratedLocationName() => $"the type '{IllegalType.ToString(DuneTypeFormat.FullNameOnly)}'";
    protected override string GetRawLocationName() => IllegalType.Name;

    public override string DiagnosticId => DuneDiagnostic.IllegalTypeReferenceId;
    public override DuneSandboxViolationCategory Category => DuneSandboxViolationCategory.Type;
}

public sealed class DuneSandboxViolationPointer(DunePointerTypeReference illegalPointer) : DuneSandboxViolationTypeReference(illegalPointer, null) {
    public new DunePointerTypeReference IllegalType { get; } = illegalPointer;

    protected override string GetDecoratedLocationName() => $"the pointer '{IllegalType.Name}'";
    protected override string GetRawLocationName() => IllegalType.Name;

    public override string DiagnosticId => DuneDiagnostic.IllegalPointerId;
    public override DuneSandboxViolationCategory Category => DuneSandboxViolationCategory.Pointer;
}

public sealed class DuneSandboxViolationFunctionPointer(DuneFunctionPointerTypeReference illegalFunctionPointer) : DuneSandboxViolationTypeReference(illegalFunctionPointer, null) {
    public new DuneFunctionPointerTypeReference IllegalType { get; } = illegalFunctionPointer;

    protected override string GetDecoratedLocationName() => $"the function pointer '{IllegalType.Name}'";
    protected override string GetRawLocationName() => IllegalType.Name;

    public override string DiagnosticId => DuneDiagnostic.IllegalFunctionPointerId;
    public override DuneSandboxViolationCategory Category => DuneSandboxViolationCategory.FunctionPointer;
}

public sealed class DuneSandboxViolationMethod(DuneMethodReference illegalMethod, DuneSandboxViolationCause? cause) : DuneSandboxViolation(cause) {
    public DuneMethodReference IllegalMethod { get; } = illegalMethod;

    protected override string GetDecoratedLocationName() => $"the method '{IllegalMethod.ToString(DuneTypeFormat.FullNameOnly, null, null)}'";
    protected override string GetRawLocationName() => IllegalMethod.Name;

    public override string DiagnosticId => DuneDiagnostic.IllegalMethodId;
    public override DuneSandboxViolationCategory Category => DuneSandboxViolationCategory.Method;
}

public sealed class DuneSandboxViolationField(DuneFieldReference illegalField, DuneSandboxViolationCause? cause) : DuneSandboxViolation(cause) {
    public DuneFieldReference IllegalField { get; } = illegalField;

    protected override string GetDecoratedLocationName() => $"the field '{IllegalField.ToString(DuneTypeFormat.FullNameOnly, null)}'";
    protected override string GetRawLocationName() => IllegalField.Name;

    public override string DiagnosticId => DuneDiagnostic.IllegalFieldId;
    public override DuneSandboxViolationCategory Category => DuneSandboxViolationCategory.Field;
}

public sealed class DuneSandboxViolationProperty(DunePropertyReference illegalProperty, DuneSandboxViolationCause? cause) : DuneSandboxViolation(cause) {
    public DunePropertyReference IllegalProperty { get; } = illegalProperty;

    protected override string GetDecoratedLocationName() => $"the property '{IllegalProperty.ToString(DuneTypeFormat.FullNameOnly, null)}'";
    protected override string GetRawLocationName() => IllegalProperty.Name;

    public override string DiagnosticId => DuneDiagnostic.IllegalPropertyId;
    public override DuneSandboxViolationCategory Category => DuneSandboxViolationCategory.Property;
}

public sealed class DuneSandboxViolationEvent(DuneEventReference illegalEvent, DuneSandboxViolationCause? cause) : DuneSandboxViolation(cause) {
    public DuneEventReference IllegalEvent { get; } = illegalEvent;

    protected override string GetDecoratedLocationName() => $"the event '{IllegalEvent.ToString(DuneTypeFormat.FullNameOnly, null)}'";
    protected override string GetRawLocationName() => IllegalEvent.Name;

    public override string DiagnosticId => DuneDiagnostic.IllegalEventId;
    public override DuneSandboxViolationCategory Category => DuneSandboxViolationCategory.Event;
}

public sealed class DuneSandboxViolationKeyword(string keyword) : DuneSandboxViolation(null) {
    public string Keyword { get; } = keyword;
    protected override string GetDecoratedLocationName() => $"the keyword {Keyword}";
    protected override string GetRawLocationName() => Keyword;

    public override string DiagnosticId => DuneDiagnostic.IllegalKeywordId;
    public override DuneSandboxViolationCategory Category => DuneSandboxViolationCategory.Keyword;
}

public sealed record class DuneSandboxRoslynViolation(
    SyntaxNode LocationNode,
    ISymbol? LocationSymbol,
    DuneSandboxViolation Violation
);

public enum DuneSandboxCecilViolationLocation {
    AssemblyReference,

    AttributeConstructor,
    AttributeArgument,

    GenericParameter,
    GenericConstraint,

    TypeBase,
    TypeInterface,

    MethodReturnType,
    MethodParameter,
    MethodBody,

    FieldType,

    PropertyType,
    PropertySetMethod,
    PropertyGetMethod,

    EventType,
    EventAddMethod,
    EventRaiseMethod,
    EventRemoveMethod,
}

public sealed record class DuneSandboxCecilViolation(
    DuneSandboxViolation Violation,
    DuneAssemblyReference Assembly,
    ImmutableArray<DuneSandboxCecilViolationLocation> Locations,
    string? LocationMemberName,
    CecilSequencePoint? SequencePoint
);