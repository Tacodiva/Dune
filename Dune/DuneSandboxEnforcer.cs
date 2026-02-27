
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Dune;

public static class DuneSandboxEnforcer {

    #region Dune

    private static bool CheckReferencesFrom(this IDuneSandboxRules rules, DuneAssemblyReference referenceAssembly, DuneAssemblyReference? referencingAssembly)
        => referenceAssembly != referencingAssembly || rules.CheckInternalReferences;

    private static bool CheckReferencesFrom(this IDuneSandboxRules rules, IDuneMemberReference symbol, DuneAssemblyReference? referencingAssembly)
        => symbol.Assembly == null || CheckReferencesFrom(rules, symbol.Assembly, referencingAssembly);

    public static DuneSandboxViolation? CheckAssemblyReference(this IDuneSandboxRules rules, DuneAssemblyReference reference, DuneAssemblyReference? referencingAssembly) {
        if (!CheckReferencesFrom(rules, reference, referencingAssembly)) return null;
        if (rules.IsAssemblyAllowed(reference)) return null;
        return new DuneSandboxViolationAssembly(reference, null);
    }

    private static DuneSandboxViolationCause? CheckReference(this IDuneSandboxRules rules, IDuneSymbol reference, DuneAssemblyReference? referencingAssembly) {
        if (reference.Assembly != null) {
            DuneSandboxViolation? violation = CheckAssemblyReference(rules, reference.Assembly, referencingAssembly);
            if (violation != null) return new(DuneSandboxViolationLocation.DeclaringAssembly, violation);
        }
        return null;
    }

    private static DuneSandboxViolationCause? CheckGenericReference(this IDuneSandboxRules rules, IDuneGenericReference refernece, DuneAssemblyReference? referencingAssembly) {
        foreach (DuneTypeReference genericArg in refernece.GenericArguments) {
            DuneSandboxViolation? violation = CheckTypeReference(rules, genericArg, referencingAssembly);
            if (violation != null)
                return new(DuneSandboxViolationLocation.GenericArgument, violation);
        }
        return null;
    }

    public static DuneSandboxViolationTypeReference? CheckTypeReference(this IDuneSandboxRules rules, DuneTypeReference reference, DuneAssemblyReference? referencingAssembly) {
        switch (reference) {

            case DunePointerTypeReference pointerRef: {
                    if (!rules.AllowUnsafe)
                        return new DuneSandboxViolationPointer(pointerRef);

                    return CheckTypeReference(rules, pointerRef.Element, referencingAssembly);
                }

            case DuneFunctionPointerTypeReference funcPointerRef: {
                    if (!rules.AllowUnsafe)
                        return new DuneSandboxViolationFunctionPointer(funcPointerRef);

                    if (funcPointerRef.ReturnType != null) {
                        DuneSandboxViolation? violation = CheckTypeReference(rules, funcPointerRef.ReturnType, referencingAssembly);
                        if (violation != null)
                            return new(reference, new(DuneSandboxViolationLocation.FunctionPointerReturn, violation));
                    }

                    foreach (DuneTypeReference parameter in funcPointerRef.Parameters) {
                        DuneSandboxViolation? violation = CheckTypeReference(rules, parameter, referencingAssembly);
                        if (violation != null)
                            return new(reference, new(DuneSandboxViolationLocation.FunctionPointerParameter, violation));
                    }

                    return null;
                }

            case DuneArrayTypeReference arrayRef:
                return CheckTypeReference(rules, arrayRef.Element, referencingAssembly);

            case DuneRefTypeReference refRef:
                return CheckTypeReference(rules, refRef.Element, referencingAssembly);

            case DuneUnknownTypeReference:
                return null;

            case DuneGenericTypeReference:
                return null;

            case DuneTypeSignatureReference typeRef: {

                    if (CheckReferencesFrom(rules, typeRef, referencingAssembly) && !rules.IsTypeAllowed(typeRef))
                        return new DuneSandboxViolationTypeReference(reference, null);

                    DuneSandboxViolationCause? violationCause = CheckReference(rules, reference, referencingAssembly);
                    if (violationCause != null) return new(reference, violationCause);

                    violationCause = CheckGenericReference(rules, typeRef, referencingAssembly);
                    if (violationCause != null) return new DuneSandboxViolationTypeReference(reference, violationCause);

                    DuneTypeSignatureReference? declaringType = typeRef.DeclaringType;

                    while (declaringType != null) {
                        Debug.Assert(declaringType.Assembly == typeRef.Assembly);

                        if (CheckReferencesFrom(rules, declaringType, referencingAssembly) && !rules.IsTypeAllowed(declaringType)) {
                            return new DuneSandboxViolationTypeReference(reference,
                                new(DuneSandboxViolationLocation.DeclaringType, new DuneSandboxViolationTypeReference(declaringType, null))
                            );
                        }
                        declaringType = declaringType.DeclaringType;
                    }

                    return null;
                }

            default:
                Debug.Fail($"Unhandled type reference {reference.GetType()}.");
                return null;
        }
    }

    public static DuneSandboxViolationCause? CheckMember(this IDuneSandboxRules rules, IDuneMemberReference reference, DuneAssemblyReference? referencingAssembly) {
        DuneSandboxViolationCause? violationCause = CheckReference(rules, reference, referencingAssembly);
        if (violationCause != null) return violationCause;

        if (reference.DeclaringType != null) {
            DuneSandboxViolation? violation = CheckTypeReference(rules, reference.DeclaringType, referencingAssembly);
            if (violation != null) return new(DuneSandboxViolationLocation.DeclaringType, violation);
        }

        return null;
    }

    public static DuneSandboxViolationMethod? CheckMethodReference(this IDuneSandboxRules rules, DuneMethodReference reference, DuneAssemblyReference? referencingAssembly) {
        if (CheckReferencesFrom(rules, reference, referencingAssembly) && !rules.IsMethodAllowed(reference))
            return new DuneSandboxViolationMethod(reference, null);

        DuneMethodSignature signature = reference.Signature;

        if (signature.ReturnType != null) {
            DuneSandboxViolation? violation = CheckTypeReference(rules, signature.ReturnType, referencingAssembly);
            if (violation != null)
                return new DuneSandboxViolationMethod(reference, new(DuneSandboxViolationLocation.MethodReturn, violation));
        }

        foreach (DuneMethodParameter parameter in signature.Parameters) {
            DuneSandboxViolation? violation = CheckTypeReference(rules, parameter.Type, referencingAssembly);
            if (violation != null)
                return new DuneSandboxViolationMethod(reference, new(DuneSandboxViolationLocation.MethodParameter, violation));
        }

        DuneSandboxViolationCause? violationCause = CheckGenericReference(rules, reference, referencingAssembly);
        violationCause ??= CheckMember(rules, reference, referencingAssembly);
        if (violationCause != null) return new DuneSandboxViolationMethod(reference, violationCause);

        return null;
    }

    public static DuneSandboxViolationField? CheckFieldReference(this IDuneSandboxRules rules, DuneFieldReference reference, DuneAssemblyReference? referencingAssembly) {
        if (CheckReferencesFrom(rules, reference, referencingAssembly) && !rules.IsFieldAllowed(reference))
            return new DuneSandboxViolationField(reference, null);

        DuneSandboxViolation? violation = CheckTypeReference(rules, reference.Type, referencingAssembly);
        if (violation != null)
            return new DuneSandboxViolationField(reference, new(DuneSandboxViolationLocation.FieldType, violation));

        DuneSandboxViolationCause? violationCause = CheckMember(rules, reference, referencingAssembly);
        if (violationCause != null) return new DuneSandboxViolationField(reference, violationCause);

        return null;
    }

    public static DuneSandboxViolationProperty? CheckPropertyReference(this IDuneSandboxRules rules, DunePropertyReference reference, DuneAssemblyReference? referencingAssembly) {
        if (reference.SetMethod != null) {
            DuneSandboxViolationMethod? methodViolation = CheckMethodReference(rules, reference.SetMethod, referencingAssembly);
            if (methodViolation != null)
                return new DuneSandboxViolationProperty(reference, new(DuneSandboxViolationLocation.PropertySetMethod, methodViolation));
        }

        if (reference.GetMethod != null) {
            DuneSandboxViolationMethod? methodViolation = CheckMethodReference(rules, reference.GetMethod, referencingAssembly);
            if (methodViolation != null)
                return new DuneSandboxViolationProperty(reference, new(DuneSandboxViolationLocation.PropertyGetMethod, methodViolation));
        }

        DuneSandboxViolation? violation = CheckTypeReference(rules, reference.Type, referencingAssembly);
        if (violation != null)
            return new DuneSandboxViolationProperty(reference, new(DuneSandboxViolationLocation.PropertyType, violation));

        DuneSandboxViolationCause? violationCause = CheckMember(rules, reference, referencingAssembly);
        if (violationCause != null) return new DuneSandboxViolationProperty(reference, violationCause);

        return null;
    }

    public static DuneSandboxViolationEvent? CheckEventReference(this IDuneSandboxRules rules, DuneEventReference reference, DuneAssemblyReference? referencingAssembly) {
        if (reference.AddMethod != null) {
            DuneSandboxViolationMethod? methodViolation = CheckMethodReference(rules, reference.AddMethod, referencingAssembly);
            if (methodViolation != null)
                return new DuneSandboxViolationEvent(reference, new(DuneSandboxViolationLocation.EventAddMethod, methodViolation));
        }

        if (reference.RemoveMethod != null) {
            DuneSandboxViolationMethod? methodViolation = CheckMethodReference(rules, reference.RemoveMethod, referencingAssembly);
            if (methodViolation != null)
                return new DuneSandboxViolationEvent(reference, new(DuneSandboxViolationLocation.EventRemoveMethod, methodViolation));
        }

        if (reference.RaiseMethod != null) {
            DuneSandboxViolationMethod? methodViolation = CheckMethodReference(rules, reference.RaiseMethod, referencingAssembly);
            if (methodViolation != null)
                return new DuneSandboxViolationEvent(reference, new(DuneSandboxViolationLocation.EventRaiseMethod, methodViolation));
        }

        if (reference.Type != null) {
            DuneSandboxViolation? violation = CheckTypeReference(rules, reference.Type, referencingAssembly);
            if (violation != null)
                return new DuneSandboxViolationEvent(reference, new(DuneSandboxViolationLocation.EventType, violation));
        }

        DuneSandboxViolationCause? violationCause = CheckMember(rules, reference, referencingAssembly);
        if (violationCause != null) return new DuneSandboxViolationEvent(reference, violationCause);

        return null;
    }
    #endregion

    #region Roslyn

    public static DuneSandboxViolation? CheckSymbol(this IDuneSandboxRules rules, ISymbol? symbol, DuneAssemblyReference? sourceAssembly, DuneRoslynContext? ctx = null) {

        if (symbol is IDynamicTypeSymbol && !rules.AllowDynamicKeyword)
            return new DuneSandboxViolationKeyword("dynamic");

        return symbol switch {
            ITypeSymbol typeSymbol => CheckTypeReference(rules, DuneTypeReference.FromSymbol(typeSymbol, false, ctx), sourceAssembly),
            IMethodSymbol methodSymbol => CheckMethodReference(rules, DuneMethodReference.FromSymbol(methodSymbol, ctx), sourceAssembly),
            IFieldSymbol fieldSymbol => CheckFieldReference(rules, DuneFieldReference.FromSymbol(fieldSymbol, ctx), sourceAssembly),
            IPropertySymbol propertySymbol => CheckPropertyReference(rules, DunePropertyReference.FromSymbol(propertySymbol, ctx), sourceAssembly),
            IEventSymbol eventSymbol => CheckEventReference(rules, DuneEventReference.FromSymbol(eventSymbol, ctx), sourceAssembly),
            _ => null,
        };
    }

    public static DuneSandboxViolation? CheckSyntaxNode(this IDuneSandboxRules rules, SyntaxNode node, ISymbol? symbol, DuneAssemblyReference? sourceAssembly, DuneRoslynContext? ctx = null) {
        SyntaxKind kind = node.Kind();

        switch (kind) {
            case SyntaxKind.MakeRefKeyword:
            case SyntaxKind.MakeRefExpression:
                if (!rules.AllowTypedReferenceKeywords)
                    return new DuneSandboxViolationKeyword("__makeref");
                break;
            case SyntaxKind.RefTypeKeyword:
            case SyntaxKind.RefTypeExpression:
                if (!rules.AllowTypedReferenceKeywords)
                    return new DuneSandboxViolationKeyword("__reftype");
                break;
            case SyntaxKind.RefValueKeyword:
            case SyntaxKind.RefValueExpression:
                if (!rules.AllowTypedReferenceKeywords)
                    return new DuneSandboxViolationKeyword("__refvalue");
                break;
            case SyntaxKind.ArgListKeyword:
            case SyntaxKind.ArgListExpression:
                if (!rules.AllowTypedReferenceKeywords)
                    return new DuneSandboxViolationKeyword("__arglist");
                break;
            case SyntaxKind.Argument:
                if (!rules.AllowTypedReferenceKeywords &&
                     node is ParameterSyntax paramSyntax &&
                     paramSyntax.Identifier.Text == "__arglist") {
                    return new DuneSandboxViolationKeyword("__arglist");
                }
                break;
            case SyntaxKind.UnsafeKeyword:
            case SyntaxKind.UnsafeStatement:
                if (!rules.AllowUnsafe)
                    return new DuneSandboxViolationKeyword("unsafe");
                break;
        }

        if (symbol != null) {
            DuneSandboxViolation? violation = CheckSymbol(rules, symbol, sourceAssembly, ctx);
            if (violation != null) return violation;
        }

        return null;
    }

    public static ImmutableArray<DuneSandboxRoslynViolation> CheckSyntaxTree(this IDuneSandboxRules rules, SyntaxTree tree, SemanticModel? treeSemantics, DuneAssemblyReference? sourceAssembly, DuneRoslynContext? ctx = null) {
        List<DuneSandboxRoslynViolation> violations = [];
        ctx ??= new();

        void CheckNode(SyntaxNode node) {
            ISymbol? nodeSymbol = treeSemantics?.GetSymbolInfo(node).Symbol;
            DuneSandboxViolation? violation = CheckSyntaxNode(rules, node, nodeSymbol, sourceAssembly, ctx);

            if (violation != null) {
                violations.Add(new(node, nodeSymbol, violation));
                return;
            }

            foreach (SyntaxNode child in node.ChildNodes())
                CheckNode(child);
        }

        CheckNode(tree.GetRoot());

        return violations.ToImmutableArray();
    }
    #endregion

    #region Cecil

    private sealed record class CecilCheckCtx(
        IDuneSandboxRules Rules,
        DuneAssemblyReference Assembly,
        List<DuneSandboxCecilViolation> Violations,
        DuneCecilContext CecilContext,
        ImmutableArray<DuneSandboxCecilViolationLocation> Locations,
        IMemberDefinition? Member = null,
        CecilSequencePoint? SequencePoint = null
    ) {
        public CecilCheckCtx IntoLocation(DuneSandboxCecilViolationLocation newLocation)
            => new(Rules, Assembly, Violations, CecilContext, [.. Locations, newLocation], Member, SequencePoint);

        public CecilCheckCtx IntoMember(IMemberDefinition newMember)
            => new(Rules, Assembly, Violations, CecilContext, [], newMember, SequencePoint);

        public CecilCheckCtx IntoSequencePoint(CecilSequencePoint sequencePoint)
            => new(Rules, Assembly, Violations, CecilContext, Locations, Member, sequencePoint);

        public bool AddViolation(DuneSandboxViolation? violation) {
            if (violation == null) return false;

            string? memberName = null;

            if (Member != null) {

                static void AppendTypeName(StringBuilder sb, CecilTypeDefinition typeDefinition) {
                    if (typeDefinition.DeclaringType == null) {
                        if (!string.IsNullOrWhiteSpace(typeDefinition.Namespace)) {
                            sb.Append(typeDefinition.Namespace);
                            sb.Append('.');
                        }
                    } else {
                        AppendTypeName(sb, typeDefinition.DeclaringType);
                        sb.Append('.');
                    }

                    sb.Append(typeDefinition.Name);
                }

                StringBuilder memberNameBuilder = new();

                if (Member.DeclaringType != null) {
                    AppendTypeName(memberNameBuilder, Member.DeclaringType);
                    memberNameBuilder.Append(':');
                }

                memberNameBuilder.Append(Member.Name);

                memberName = memberNameBuilder.ToString();
            }

            Violations.Add(new(violation, Assembly, Locations, memberName, SequencePoint));
            return true;
        }
    }

    private static void CheckCecilDefinitionAttributes(CecilCustomAttributeProvider attributeProvider, CecilCheckCtx ctx) {
        void CheckAttributeArgument(object? value) {
            switch (value) {
                case CecilTypeReference typeArg:
                    CheckCecilTypeReference(typeArg, ctx.IntoLocation(DuneSandboxCecilViolationLocation.AttributeArgument));
                    return;

                case CustomAttributeArgument argArg:
                    CheckCecilTypeReference(argArg.Type, ctx.IntoLocation(DuneSandboxCecilViolationLocation.AttributeArgument));
                    CheckAttributeArgument(argArg.Value);
                    return;

                case CustomAttributeArgument[] arrayArg:
                    foreach (CustomAttributeArgument arrayArgElement in arrayArg)
                        CheckAttributeArgument(arrayArgElement);
                    return;

                case string:
                case char:
                case bool:
                case sbyte:
                case byte:
                case int:
                case uint:
                case long:
                case ulong:
                case float:
                case double:
                case null:
                    // All the primitives don't need to be checked
                    return;

                default:
                    Debug.Fail($"Unknown custom attribute argument type {value.GetType()}.");
                    return;
            }
        }

        foreach (CustomAttribute attribute in attributeProvider.CustomAttributes) {
            CheckCecilMethodReference(
                attribute.Constructor,
                ctx.IntoLocation(DuneSandboxCecilViolationLocation.AttributeConstructor)
            );

            foreach (CustomAttributeArgument arg in attribute.ConstructorArguments)
                CheckAttributeArgument(arg);

            foreach (CustomAttributeNamedArgument arg in attribute.Fields)
                CheckAttributeArgument(arg.Argument);

            foreach (CustomAttributeNamedArgument arg in attribute.Properties)
                CheckAttributeArgument(arg.Argument);
        }
    }

    private static void CheckCecilGenericParameterDefinition(IGenericParameterProvider provider, CecilCheckCtx ctx) {
        foreach (GenericParameter parameter in provider.GenericParameters) {
            CheckCecilDefinitionAttributes(parameter, ctx.IntoLocation(DuneSandboxCecilViolationLocation.GenericParameter));

            foreach (GenericParameterConstraint constraint in parameter.Constraints) {

                CecilCheckCtx constaintCtx = ctx.IntoLocation(DuneSandboxCecilViolationLocation.GenericConstraint);

                CheckCecilDefinitionAttributes(constraint, constaintCtx);
                CheckCecilTypeReference(constraint.ConstraintType, constaintCtx);
            }
        }
    }

    private static void CheckCecilTypeReference(CecilTypeReference typeReference, CecilCheckCtx ctx) {
        ctx.AddViolation(CheckTypeReference(ctx.Rules, DuneTypeReference.FromCecilReference(typeReference, ctx.CecilContext), ctx.Assembly));
    }

    private static void CheckCecilTypeDefinition(CecilTypeDefinition typeDefinition, CecilCheckCtx ctx) {
        CheckCecilDefinitionAttributes(typeDefinition, ctx);
        CheckCecilGenericParameterDefinition(typeDefinition, ctx);

        if (typeDefinition.BaseType != null) {
            CheckCecilTypeReference(
                typeDefinition.BaseType,
                ctx.IntoLocation(DuneSandboxCecilViolationLocation.TypeBase)
            );
        }

        foreach (InterfaceImplementation @interface in typeDefinition.Interfaces) {
            CecilCheckCtx interfaceCtx = ctx.IntoLocation(DuneSandboxCecilViolationLocation.TypeInterface);

            CheckCecilDefinitionAttributes(@interface, interfaceCtx);
            CheckCecilTypeReference(@interface.InterfaceType, interfaceCtx);
        }

        foreach (CecilTypeDefinition nestedType in typeDefinition.NestedTypes)
            CheckCecilTypeDefinition(nestedType, ctx.IntoMember(nestedType));

        foreach (CecilMethodDefinition methodDef in typeDefinition.Methods)
            CheckCecilMethodDefinition(methodDef, ctx.IntoMember(methodDef));

        foreach (CecilFieldDefinition fieldDef in typeDefinition.Fields)
            CheckCecilFieldDefinition(fieldDef, ctx.IntoMember(fieldDef));

        foreach (CecilPropertyDefinition propertyDef in typeDefinition.Properties)
            CheckCecilPropertyDefinition(propertyDef, ctx.IntoMember(propertyDef));

        foreach (CecilEventDefinition eventDef in typeDefinition.Events)
            CheckCecilEventDefinition(eventDef, ctx.IntoMember(eventDef));
    }

    private static void CheckCecilMethodDefinition(CecilMethodDefinition methodDefinition, CecilCheckCtx ctx) {
        CheckCecilDefinitionAttributes(methodDefinition, ctx);
        CheckCecilGenericParameterDefinition(methodDefinition, ctx);

        CheckCecilTypeReference(methodDefinition.ReturnType, ctx.IntoLocation(DuneSandboxCecilViolationLocation.MethodReturnType));

        foreach (ParameterDefinition parameter in methodDefinition.Parameters) {
            CecilCheckCtx paramCtx = ctx.IntoLocation(DuneSandboxCecilViolationLocation.MethodParameter);
            CheckCecilDefinitionAttributes(parameter, paramCtx);
            CheckCecilTypeReference(parameter.ParameterType, paramCtx);
        }

        IDictionary<Instruction, CecilSequencePoint> sequencePoints = methodDefinition.DebugInformation.GetSequencePointMapping();

        if (methodDefinition.Body != null) {

            CecilCheckCtx ctxBody = ctx.IntoLocation(DuneSandboxCecilViolationLocation.MethodBody);

            foreach (VariableDefinition varDef in methodDefinition.Body.Variables) {
                CheckCecilTypeReference(varDef.VariableType, ctxBody);
            }

            CecilCheckCtx ctxInst = ctxBody;

            foreach (Instruction inst in methodDefinition.Body.Instructions) {
                if (sequencePoints.TryGetValue(inst, out CecilSequencePoint? point))
                    ctxInst = ctxInst.IntoSequencePoint(point);

                switch (inst.Operand) {
                    case CecilTypeReference operandType:
                        CheckCecilTypeReference(operandType, ctxInst);
                        break;
                    case CallSite operandSite:
                        throw new NotSupportedException();
                    case CecilMethodReference operandMethod:
                        CheckCecilMethodReference(operandMethod, ctxInst);
                        break;
                    case CecilFieldReference operandField:
                        CheckCecilFieldReference(operandField, ctxInst);
                        break;

                    case VariableDefinition:
                    case ParameterDefinition:
                        // These both reference local variables & operands which we've already checked
                        break;

                    case Instruction:
                    case Instruction[]:
                        // These have to be instructions in this method's body, so we'll check them when we
                        //  get to them if we haven't already.
                        break;

                    case string:
                    case char:
                    case bool:
                    case sbyte:
                    case byte:
                    case int:
                    case uint:
                    case long:
                    case ulong:
                    case float:
                    case double:
                        // All the primitives don't need to be checked
                        break;

                    case null:
                        // Instruction with no operand
                        break;

                    default:
                        Debug.Fail($"Unknown operand type '{inst.Operand.GetType()}'.");
                        break;
                }
            }
        }
    }

    private static void CheckCecilFieldReference(CecilFieldReference fieldReference, CecilCheckCtx ctx) {
        ctx.AddViolation(CheckFieldReference(ctx.Rules, DuneFieldReference.FromCecilReference(fieldReference, ctx.CecilContext), ctx.Assembly));
    }

    private static void CheckCecilFieldDefinition(CecilFieldDefinition fieldDefinition, CecilCheckCtx ctx) {
        CheckCecilDefinitionAttributes(fieldDefinition, ctx);
        CheckCecilTypeReference(fieldDefinition.FieldType, ctx.IntoLocation(DuneSandboxCecilViolationLocation.FieldType));
    }

    private static void CheckCecilPropertyDefinition(CecilPropertyDefinition propertyDefinition, CecilCheckCtx ctx) {
        InternalUtils.Assert(!propertyDefinition.HasOtherMethods);
        InternalUtils.Assert(!propertyDefinition.HasParameters);

        CheckCecilDefinitionAttributes(propertyDefinition, ctx);

        CheckCecilTypeReference(propertyDefinition.PropertyType, ctx.IntoLocation(DuneSandboxCecilViolationLocation.PropertyType));

        if (propertyDefinition.GetMethod != null)
            CheckCecilMethodReference(propertyDefinition.GetMethod, ctx.IntoLocation(DuneSandboxCecilViolationLocation.PropertyGetMethod));

        if (propertyDefinition.SetMethod != null)
            CheckCecilMethodReference(propertyDefinition.SetMethod, ctx.IntoLocation(DuneSandboxCecilViolationLocation.PropertySetMethod));
    }

    private static void CheckCecilEventDefinition(CecilEventDefinition eventDefinition, CecilCheckCtx ctx) {
        InternalUtils.Assert(!eventDefinition.HasOtherMethods);

        CheckCecilDefinitionAttributes(eventDefinition, ctx);

        CheckCecilTypeReference(eventDefinition.EventType, ctx.IntoLocation(DuneSandboxCecilViolationLocation.EventType));

        if (eventDefinition.AddMethod != null)
            CheckCecilMethodReference(eventDefinition.AddMethod, ctx.IntoLocation(DuneSandboxCecilViolationLocation.EventAddMethod));

        if (eventDefinition.RemoveMethod != null)
            CheckCecilMethodReference(eventDefinition.RemoveMethod, ctx.IntoLocation(DuneSandboxCecilViolationLocation.EventRemoveMethod));

        if (eventDefinition.InvokeMethod != null)
            CheckCecilMethodReference(eventDefinition.InvokeMethod, ctx.IntoLocation(DuneSandboxCecilViolationLocation.EventRaiseMethod));
    }

    private static void CheckCecilMethodReference(CecilMethodReference methodReference, CecilCheckCtx ctx) {
        ctx.AddViolation(CheckMethodReference(ctx.Rules, DuneMethodReference.FromCecilReference(methodReference, ctx.CecilContext), ctx.Assembly));
    }

    private static void CheckCecilAssemblyNameReference(AssemblyNameReference assemblyNameReference, CecilCheckCtx ctx) {
        ctx.AddViolation(CheckAssemblyReference(ctx.Rules, DuneAssemblyReference.FromCecilReference(assemblyNameReference, ctx.CecilContext), ctx.Assembly));
    }

    public static ImmutableArray<DuneSandboxCecilViolation> CheckCecilAssemblyDefinition(this IDuneSandboxRules rules, CecilAssemblyDefinition assemblyDefinition, DuneCecilContext? cecilCtx = null) {

        CecilCheckCtx ctx = new(rules, DuneAssemblyReference.FromCecilDefinition(assemblyDefinition), [], cecilCtx ?? new(), []);

        CheckCecilDefinitionAttributes(assemblyDefinition, ctx);

        foreach (CecilModuleDefinition moduleDefinition in assemblyDefinition.Modules) {

            int moduleStartViolationCount = ctx.Violations.Count;

            CheckCecilDefinitionAttributes(moduleDefinition, ctx);

            foreach (CecilTypeDefinition typeDefinition in moduleDefinition.Types) {
                CheckCecilTypeDefinition(typeDefinition, ctx.IntoMember(typeDefinition));
            }

            bool didAddViolations = ctx.Violations.Count != moduleStartViolationCount;

            if (!didAddViolations) {
                // If we didn't find any more specific violations, we are gonna double check the assembly references.

                foreach (AssemblyNameReference reference in moduleDefinition.AssemblyReferences) {
                    CheckCecilAssemblyNameReference(reference, ctx.IntoLocation(DuneSandboxCecilViolationLocation.AssemblyReference));
                }
            }
        }

        return ctx.Violations.ToImmutableArray();
    }

    #endregion
}