
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using Microsoft.CodeAnalysis;
using Mono.Cecil;

namespace Dune;

public abstract class DuneTypeReference : IDuneType, IEquatable<DuneTypeReference> {
    public static DuneTypeReference FromType<T>(DuneReflectionContext? ctx = null)
#if NET
        where T : allows ref struct
#endif
        => FromType(typeof(T), ctx);

    public static DuneTypeReference FromType(Type type, DuneReflectionContext? ctx = null) {
        InternalUtils.ThrowIfArgumentNull(type);

        ctx ??= new();
        if (ctx.TryGetTypeReference(type, out var cached))
            return cached;

        if (type.IsArray) {
            return ctx.PutTypeReference(type, new DuneArrayTypeReference(
                FromType(type.GetElementType()!, ctx),
                type.GetArrayRank()
            ));
        }

        if (type.IsByRef) {
            return ctx.PutTypeReference(type, new DuneRefTypeReference(
                FromType(type.GetElementType()!, ctx)
            ));
        }

        if (type.IsPointer) {
            return ctx.PutTypeReference(type, new DunePointerTypeReference(
                FromType(type.GetElementType()!, ctx)
            ));
        }

#if NET
        if (type.IsFunctionPointer) {
            return ctx.PutTypeReference(type, new DuneFunctionPointerTypeReference(
                FromType(type.GetFunctionPointerReturnType(), ctx),
                type.GetFunctionPointerParameterTypes().Select(param => FromType(param, ctx)),
                type.IsUnmanagedFunctionPointer
            ));
        }
#endif

        if (type.IsGenericParameter) {

            if (type.DeclaringMethod != null) {
                return ctx.PutTypeReference(type, new DuneGenericTypeReference(
                    type.GenericParameterPosition,
                    DuneMethodSignature.FromMethodBase(type.DeclaringMethod, ctx),
                    DuneGenericSource.Method
                ));
            } else {
                return ctx.PutTypeReference(type, new DuneGenericTypeReference(
                    type.GenericParameterPosition,
                    DuneTypeSignature.FromType(type.DeclaringType!, ctx),
                    DuneGenericSource.Type
                ));
            }
        }

        return DuneTypeSignatureReference.FromType(type, ctx);
    }

    public static DuneTypeReference FromTypeReference(CecilTypeReference typeReference, DuneCecilContext? ctx = null) {
        InternalUtils.ThrowIfArgumentNull(typeReference);

        ctx ??= new();
        if (ctx.TryGetTypeReference(typeReference, out var cached))
            return cached;

        return ctx.PutTypeReference(typeReference, typeReference switch {
            ArrayType arrayTypeReference =>
                new DuneArrayTypeReference(
                    FromTypeReference(arrayTypeReference.ElementType, ctx),
                    arrayTypeReference.Dimensions.Count
                ),

            PointerType pointerTypeRefernece =>
                new DunePointerTypeReference(
                    FromTypeReference(pointerTypeRefernece.ElementType, ctx)
                ),

            FunctionPointerType functionPointerTypeReference =>
                new DuneFunctionPointerTypeReference(
                    FromTypeReference(functionPointerTypeReference.ReturnType, ctx),
                    functionPointerTypeReference.Parameters.Select(
                        param => FromTypeReference(param.ParameterType, ctx)
                    ),
                    functionPointerTypeReference.CallingConvention != MethodCallingConvention.Default
                ),

            ByReferenceType refTypeReference =>
                new DuneRefTypeReference(
                    FromTypeReference(refTypeReference.ElementType, ctx)
                ),

            CecilGenericParameter genericParameterReference =>
                genericParameterReference.Type switch {
                    GenericParameterType.Method =>
                        new DuneGenericTypeReference(
                            genericParameterReference.Position,
                            DuneMethodSignature.FromMethodDefinition(genericParameterReference.DeclaringMethod.Resolve(), ctx),
                            DuneGenericSource.Method
                        ),

                    GenericParameterType.Type =>
                        new DuneGenericTypeReference(
                            genericParameterReference.Position,
                            DuneTypeSignature.FromTypeDefinition(genericParameterReference.DeclaringType.Resolve(), ctx),
                            DuneGenericSource.Type
                        ),

                    _ => throw new ArgumentException($"Unknown generic parameter location {genericParameterReference.Type}"),
                },

            // Types like int& modreq(System.Runtime.InteropServices.InAttribute)
            // We throw this information away so just return the element type.
            RequiredModifierType requiredModifierType =>
                FromTypeReference(requiredModifierType.ElementType, ctx),

            _ => DuneTypeSignatureReference.FromTypeReference(typeReference, ctx),
        });
    }

    public static DuneTypeReference FromSymbol(ITypeSymbol typeSymbol, RefKind refKind, DuneRoslynContext? ctx = null)
        => FromSymbol(typeSymbol, refKind != RefKind.None, ctx);

    public static DuneTypeReference FromSymbol(ITypeSymbol typeSymbol, bool isRef, DuneRoslynContext? ctx = null) {
        InternalUtils.ThrowIfArgumentNull(typeSymbol);

        ctx ??= new();
        if (ctx.TryGetTypeReference(typeSymbol, isRef, out var cached))
            return cached;

        if (isRef) {
            return ctx.PutTypeReference(typeSymbol, true, new DuneRefTypeReference(
                FromSymbol(typeSymbol, false, ctx)
            ));
        }

        switch (typeSymbol) {
            case INamedTypeSymbol namedTypeSymbol:
                return DuneTypeSignatureReference.FromSymbol(namedTypeSymbol, ctx);

            case IArrayTypeSymbol arrayTypeSymbol:
                return ctx.PutTypeReference(typeSymbol, false, new DuneArrayTypeReference(
                    FromSymbol(arrayTypeSymbol.ElementType, false, ctx),
                    arrayTypeSymbol.Rank
                ));

            case ITypeParameterSymbol typeParamSymbol:
                switch (typeParamSymbol.TypeParameterKind) {
                    case TypeParameterKind.Method:
                        return new DuneGenericTypeReference(
                            typeParamSymbol.Ordinal,
                            DuneMethodSignature.FromSymbol(typeParamSymbol.DeclaringMethod!, ctx),
                            DuneGenericSource.Method
                        );
                    case TypeParameterKind.Type: {
                            /*
                             We need to figure out the ordinal of the generic after flattening. Flattening is adding all
                             generics from declaring types into their inner types. Like this:

                             class Outer<A, B> { class Inner<C, D> { } } -> class Outer<A, B> { class Inner<A, B, C, D> { } }

                             So if we're referencing D, typeParamSymbol.Ordinal will be 1 as D is index 1 of Inner's generics.
                             We need to count all the generics in the generic's declaring types and add them to the index.
                             In this example, Outer has 2 generics, so that is added to the index to get 2 + 1 = 3 as the final
                             post-flattening index. 
                            */
                            INamedTypeSymbol genericDeclaringType = typeParamSymbol.DeclaringType!;

                            INamedTypeSymbol? genericDeclaringTypeOuter = genericDeclaringType.ContainingType;
                            int outerGenericCount = 0;

                            while (genericDeclaringTypeOuter != null) {
                                outerGenericCount += genericDeclaringTypeOuter.TypeParameters.Length;
                                genericDeclaringTypeOuter = genericDeclaringTypeOuter.ContainingType;
                            }

                            DuneTypeSignature genericDeclaringTypeDef = DuneTypeSignature.FromSymbol(genericDeclaringType, ctx);

                            int genericIndex = typeParamSymbol.Ordinal + outerGenericCount;

                            // Sanity check for the above to make sure the index we calculated gives us a generic of the right name.
                            InternalUtils.Assert(typeParamSymbol.Name == genericDeclaringTypeDef.GenericParameterNames[genericIndex]);

                            return ctx.PutTypeReference(typeSymbol, false, 
                                new DuneGenericTypeReference(genericIndex, genericDeclaringTypeDef, DuneGenericSource.Type)
                            );
                        }
                    default:
                        throw new ArgumentException($"Unknown generic parameter location {typeParamSymbol.TypeParameterKind}");
                }

            case IPointerTypeSymbol pointerTypeSymbol:
                return ctx.PutTypeReference(typeSymbol, false, new DunePointerTypeReference(
                    FromSymbol(pointerTypeSymbol.PointedAtType, false, ctx)
                ));

            case IFunctionPointerTypeSymbol functionPointerTypeSymbol:
                IMethodSymbol signature = functionPointerTypeSymbol.Signature;

                return ctx.PutTypeReference(typeSymbol, false, new DuneFunctionPointerTypeReference(
                    FromSymbol(
                        signature.ReturnType,
                        signature.ReturnsByRefReadonly || signature.ReturnsByRef,
                        ctx
                    ),
                    signature.Parameters.Select(param => FromSymbol(param.Type, param.RefKind, ctx)),
                    signature.CallingConvention != SignatureCallingConvention.Default
                ));

            case IDynamicTypeSymbol:
                return ctx.PutTypeReference(typeSymbol, false, DuneTypeSignatureReference.Object);

            default:
                throw new DuneException($"Unknown ITypeSymbol {typeSymbol.GetType()}.");
        }
    }

    public abstract string Name { get; }

    public abstract DuneAssemblyReference? Assembly { get; }

    public abstract bool IsResolved { get; }

    public bool Equals(IDuneSymbol? obj) => Equals(obj as DuneTypeReference);
    public override bool Equals(object? obj) => Equals(obj as DuneTypeReference);
    public abstract bool Equals(DuneTypeReference? other);

    public static bool operator ==(DuneTypeReference? a, DuneTypeReference? b) => a?.Equals(b) ?? b is null;
    public static bool operator !=(DuneTypeReference? a, DuneTypeReference? b) => !(a == b);

    public override abstract int GetHashCode();
    public virtual bool IsVoid => false;

    public virtual bool HasGenericParameters => false;

    public override string ToString()
        => ToString(DuneTypeFormat.Default);

    public virtual string ToString(in DuneTypeFormat format) {
        return format.AppendType(this).ToString();
    }

    StringBuilder IDuneType.FormatAndAppendName(in DuneTypeFormat format, StringBuilder sb) {
        FormatAndAppendName(format, sb);
        return sb;
    }

    internal protected virtual void FormatAndAppendName(in DuneTypeFormat format, StringBuilder sb) {
        sb.Append(Name);
    }

    internal DuneTypeReference Resolve(DuneTypeSignatureReference? declaringType, bool force = true) {
        if (declaringType is null) {
            if (force) AssertResolved();
            return this;
        }
        return Resolve(declaringType, null, force);
    }

    internal DuneTypeReference Resolve(DuneTypeSignatureReference? declaringType, DuneMethodReference? declaringMethod, bool force = true) {
        DuneTypeReference resolved = TryResolve(declaringType, declaringMethod, force) ?? this;
        if (force) resolved.AssertResolved();
        return resolved;
    }

    internal protected virtual DuneTypeReference? TryResolve(DuneTypeSignatureReference? declaringType, DuneMethodReference? declaringMethod, bool force)
        => null;

    internal void AssertResolved() {
        if (!IsResolved)
            throw new DuneException($"Error resolving type {this}.");
    }

    public StringBuilder FormatAndAppendGenericParameters(in DuneTypeFormat genericFormat, StringBuilder sb) {
        throw new NotImplementedException();
    }
}

public sealed class DuneUnknownTypeReference : DuneTypeReference {
    public static DuneUnknownTypeReference Instance { get; } = new();

    public override string Name => "[?]";
    public override DuneAssemblyReference? Assembly => null;
    public override bool IsResolved => true;

    private DuneUnknownTypeReference() { }

    public override bool Equals(DuneTypeReference? other) => Equals(other as DuneUnknownTypeReference);
    public override bool Equals(object? obj) => Equals(obj as DuneUnknownTypeReference);
    public static bool Equals(DuneUnknownTypeReference? other) => other is not null;

    public override int GetHashCode() => 0;
}

public enum DuneGenericSource {
    Method,
    Type
}

public sealed class DuneGenericTypeReference : DuneTypeReference, IEquatable<DuneGenericTypeReference> {

    public int Index { get; }
    public override string Name { get; }
    public DuneGenericSource Source { get; }
    public override DuneAssemblyReference? Assembly { get; }

    public override bool IsResolved => false;

    internal DuneGenericTypeReference(int index, IDuneGenericSignature signature, DuneGenericSource source) {
        Index = index;
        Name = signature.GenericParameterNames[index];
        Source = source;
        Assembly = signature.Assembly;
    }

    internal DuneGenericTypeReference(int index, string name, DuneAssemblyReference? assembly, DuneGenericSource source) {
        Index = index;
        Name = name;
        Source = source;
        Assembly = assembly;
    }

    protected internal override DuneTypeReference? TryResolve(DuneTypeSignatureReference? declaringType, DuneMethodReference? declaringMethod, bool force) {
        IDuneGenericReference? reference;

        if (Source == DuneGenericSource.Type) {
            reference = declaringType;
        } else {
            reference = declaringMethod;
        }

        if (reference == null)
            return force ? DuneUnknownTypeReference.Instance : null;

        InternalUtils.Assert(Index < reference.GenericArguments.Length);
        InternalUtils.Assert(Name == reference.Signature.GenericParameterNames[Index]);

        DuneTypeReference resolved = reference.GenericArguments[Index];

        if (resolved.IsResolved)
            return resolved;

        return resolved.TryResolve(declaringType, declaringMethod, force) ?? resolved;
    }

    public override bool Equals(object? obj) => Equals(obj as DuneGenericTypeReference);
    public override bool Equals(DuneTypeReference? other) => Equals(other as DuneGenericTypeReference);

    public bool Equals(DuneGenericTypeReference? other) {
        if (ReferenceEquals(this, other)) return true;
        if (other is null) return false;

        if (Index != other.Index) return false;
        if (Name != other.Name) return false;
        if (Assembly != other.Assembly) return false;

        return true;
    }

    public override int GetHashCode() => InternalUtils.HashCodeCombine(Index, Name, Source, Assembly);

}

public abstract class DuneDecoratorTypeReference : DuneTypeReference {
    public abstract DuneTypeReference Element { get; }
    public override DuneAssemblyReference? Assembly => Element.Assembly;
    public override bool IsResolved => Element.IsResolved;

    protected internal override DuneTypeReference? TryResolve(DuneTypeSignatureReference? declaringType, DuneMethodReference? declaringMethod, bool force) {
        if (IsResolved) return null;
        DuneTypeReference? elementResolved = Element.TryResolve(declaringType, declaringMethod, force);
        if (elementResolved == null) return null;
        return CloneWithElement(elementResolved);
    }

    protected abstract DuneDecoratorTypeReference CloneWithElement(DuneTypeReference newElement);
}

public sealed class DuneArrayTypeReference : DuneDecoratorTypeReference, IEquatable<DuneArrayTypeReference> {
    public override DuneTypeReference Element { get; }
    public int DimensionCount { get; }

    public override string Name => $"{Element.Name}[{new string(',', DimensionCount - 1)}]";

    internal DuneArrayTypeReference(DuneTypeReference element, int dimensionCount) {
        Element = element;
        DimensionCount = dimensionCount;
    }

    protected override DuneDecoratorTypeReference CloneWithElement(DuneTypeReference newElement)
        => new DuneArrayTypeReference(newElement, DimensionCount);

    protected internal override void FormatAndAppendName(in DuneTypeFormat format, StringBuilder sb) {
        Element.FormatAndAppendName(format, sb);
        sb.Append('[');
        sb.Append(',', DimensionCount - 1);
        sb.Append(']');
    }

    public override bool Equals(object? obj) => Equals(obj as DuneArrayTypeReference);
    public override bool Equals(DuneTypeReference? other) => Equals(other as DuneArrayTypeReference);
    public bool Equals(DuneArrayTypeReference? other) {
        if (ReferenceEquals(this, other)) return true;
        if (other is null) return false;
        if (DimensionCount != other.DimensionCount) return false;
        if (!Element.Equals(other.Element)) return false;
        return true;
    }

    public override int GetHashCode() => InternalUtils.HashCodeCombine(Element, DimensionCount);
}

public sealed class DunePointerTypeReference : DuneDecoratorTypeReference, IEquatable<DunePointerTypeReference> {
    public override DuneTypeReference Element { get; }

    public override string Name => $"{Element.Name}*";

    internal DunePointerTypeReference(DuneTypeReference element) {
        Element = element;
    }

    protected override DuneDecoratorTypeReference CloneWithElement(DuneTypeReference newElement)
        => new DunePointerTypeReference(newElement);

    protected internal override void FormatAndAppendName(in DuneTypeFormat format, StringBuilder sb) {
        Element.FormatAndAppendName(format, sb);
        sb.Append('*');
    }

    public override bool Equals(object? obj) => Equals(obj as DunePointerTypeReference);
    public override bool Equals(DuneTypeReference? other) => Equals(other as DunePointerTypeReference);
    public bool Equals(DunePointerTypeReference? other) {
        if (ReferenceEquals(this, other)) return true;
        if (other is null) return false;
        if (!Element.Equals(other.Element)) return false;
        return true;
    }

    public override int GetHashCode() => InternalUtils.HashCodeCombine(Element.GetHashCode(), 128302190);
}

public sealed class DuneRefTypeReference : DuneDecoratorTypeReference, IEquatable<DuneRefTypeReference> {
    public override DuneTypeReference Element { get; }

    public override string Name => $"{Element.Name}&";

    internal DuneRefTypeReference(DuneTypeReference element) {
        Element = element;
    }

    protected override DuneDecoratorTypeReference CloneWithElement(DuneTypeReference newElement)
        => new DuneRefTypeReference(newElement);

    protected internal override void FormatAndAppendName(in DuneTypeFormat format, StringBuilder sb) {
        Element.FormatAndAppendName(format, sb);
        sb.Append('&');
    }

    public override bool Equals(object? obj) => Equals(obj as DuneRefTypeReference);
    public override bool Equals(DuneTypeReference? other) => Equals(other as DuneRefTypeReference);
    public bool Equals(DuneRefTypeReference? other) {
        if (ReferenceEquals(this, other)) return true;
        if (other is null) return false;
        if (!Element.Equals(other.Element)) return false;
        return true;
    }

    public override int GetHashCode() => InternalUtils.HashCodeCombine(Element.GetHashCode(), 218937);
}


public sealed class DuneFunctionPointerTypeReference : DuneTypeReference, IEquatable<DuneFunctionPointerTypeReference> {

    public DuneTypeReference? ReturnType { get; }
    public ImmutableArray<DuneTypeReference> Parameters { get; }
    public bool IsUnmanaged { get; }
    public override bool IsResolved { get; }

    public override string Name => IsUnmanaged ? "delegate* unmanaged" : "delegate* managed";
    public override DuneAssemblyReference? Assembly => null;

    internal DuneFunctionPointerTypeReference(DuneTypeReference? returnType, IEnumerable<DuneTypeReference> parameters, bool isUnmanaged) {
        ReturnType = returnType == null || returnType.IsVoid ? null : returnType;
        Parameters = [.. parameters];
        IsUnmanaged = isUnmanaged;
        IsResolved = (ReturnType?.IsResolved ?? true) && !Parameters.Any(arg => !arg.IsResolved);
    }

    protected internal override DuneTypeReference? TryResolve(DuneTypeSignatureReference? declaringType, DuneMethodReference? declaringMethod, bool force) {
        if (IsResolved) return null;

        bool didResolveAny = false;

        DuneTypeReference? returnType = ReturnType;

        if (returnType != null) {
            returnType = returnType.TryResolve(declaringType, declaringMethod, force);
            if (returnType == null) returnType = ReturnType;
            else didResolveAny = true;
        }

        DuneTypeReference[] parameters = new DuneTypeReference[Parameters.Length];

        for (int i = 0; i < parameters.Length; i++) {
            DuneTypeReference? resolvedGeneric = Parameters[i].TryResolve(declaringType, declaringMethod, force);

            if (resolvedGeneric == null) {
                parameters[i] = Parameters[i];
            } else {
                parameters[i] = resolvedGeneric;
                didResolveAny = true;
            }
        }

        if (!didResolveAny) return null;

        return new DuneFunctionPointerTypeReference(returnType, parameters, IsUnmanaged);
    }

    protected internal override void FormatAndAppendName(in DuneTypeFormat format, StringBuilder sb) {
        sb.Append(Name);
        sb.Append('<');
        if (Parameters.Length != 0) {
            DuneTypeFormat._defaultGenericArgument.AppendTypes(Parameters, sb);
            sb.Append(", ");
        }
        DuneTypeFormat._defaultGenericArgument.AppendType(ReturnType, sb);
        sb.Append('>');
    }

    public override bool Equals(object? obj) => Equals(obj as DuneFunctionPointerTypeReference);
    public override bool Equals(DuneTypeReference? other) => Equals(other as DuneFunctionPointerTypeReference);
    public bool Equals(DuneFunctionPointerTypeReference? other) {
        if (ReferenceEquals(this, other)) return true;
        if (other is null) return false;
        if (other.IsUnmanaged != IsUnmanaged) return false;
        if (!other.Parameters.SequenceEqual(Parameters)) return false;
        return true;
    }

    public override int GetHashCode() {
        int hash = InternalUtils.HashCodeCombine(IsUnmanaged, ReturnType);
        foreach (DuneTypeReference param in Parameters)
            hash = InternalUtils.HashCodeCombine(hash, param);
        return hash;
    }
}

public sealed class DuneTypeSignatureReference : DuneTypeReference, IDuneMemberReference, IDuneGenericReference, IEquatable<DuneTypeSignatureReference> {

    public static new DuneTypeReference FromType<T>(DuneReflectionContext? ctx = null)
#if NET
        where T : allows ref struct
#endif
        => FromType(typeof(T), ctx);

    public static new DuneTypeSignatureReference FromType(Type type, DuneReflectionContext? ctx = null) {
        InternalUtils.ThrowIfArgumentNull(type);

        if (type.IsArray || type.IsPointer || type.IsGenericParameter
#if NET
            || type.IsFunctionPointer
#endif
        )
            throw new ArgumentException($"Cannot create a type signature reference from a decorated type.");

        ctx ??= new();
        if (ctx.TryGetTypeReference(type, out var cached))
            return (DuneTypeSignatureReference)cached;

        DuneTypeSignatureReference defRef = new(
            DuneTypeSignature.FromType(type, ctx),
            [.. type.GetGenericArguments().Select(arg => DuneTypeReference.FromType(arg, ctx))]
        );

        ctx.PutTypeReference(type, defRef);

        return defRef;
    }

    public static new DuneTypeSignatureReference FromTypeReference(CecilTypeReference typeReference, DuneCecilContext? ctx = null) {
        InternalUtils.ThrowIfArgumentNull(typeReference);

        if (typeReference.IsArray || typeReference.IsPointer || typeReference.IsFunctionPointer || typeReference.IsGenericParameter)
            throw new ArgumentException($"Cannot create a type signature reference from a decorated type.");

        ctx ??= new();
        if (ctx.TryGetTypeReference(typeReference, out var cached))
            return (DuneTypeSignatureReference)cached;

        IEnumerable<DuneTypeReference> genericArguments = [];

        DuneTypeSignature signature = DuneTypeSignature.FromTypeDefinition(typeReference.Resolve());

        if (typeReference is GenericInstanceType instanceRef) {

            genericArguments = instanceRef.GenericArguments.Select(param => DuneTypeReference.FromTypeReference(param, ctx));

            typeReference = instanceRef.ElementType;

        } else if (signature.GenericParameterCount != 0) {

            // Unbound generic type. We represent this by filling the generic arguments with the generic parameters
            genericArguments = typeReference.GenericParameters.Select(param => DuneTypeReference.FromTypeReference(param, ctx));

        }

        DuneTypeSignatureReference defRef = new(signature, [.. genericArguments]);

        ctx.PutTypeReference(typeReference, defRef);

        return defRef;
    }

    public static DuneTypeSignatureReference FromSymbol(INamedTypeSymbol namedTypeSymbol, DuneRoslynContext? ctx = null) {
        InternalUtils.ThrowIfArgumentNull(namedTypeSymbol);

        ctx ??= new();
        if (ctx.TryGetTypeReference(namedTypeSymbol, false, out var cached))
            return (DuneTypeSignatureReference)cached;

        // We need to gather all the type arguments from the declaring types of the symbol to
        //  flatten them.
        IEnumerable<ITypeSymbol> typeArguments = namedTypeSymbol.TypeArguments;

        INamedTypeSymbol? declaringType = namedTypeSymbol.ContainingType;
        while (declaringType != null) {
            typeArguments = declaringType.TypeArguments.Concat(typeArguments);
            declaringType = declaringType.ContainingType;
        }

        DuneTypeSignature typeSignature = DuneTypeSignature.FromSymbol(namedTypeSymbol, ctx);

        InternalUtils.Assert(typeArguments.Count() == typeSignature.GenericParameterCount);

        DuneTypeSignatureReference defRef = new(
            typeSignature, [.. typeArguments.Select(arg => FromSymbol(arg, false, ctx))]
        );

        ctx.PutTypeReference(namedTypeSymbol, false, defRef);

        return defRef;
    }

    public DuneTypeSignature Signature { get; }
    public ImmutableArray<DuneTypeReference> GenericArguments { get; }
    public DuneTypeSignatureReference? DeclaringType { get; }
    public override bool IsResolved { get; }
    public override bool HasGenericParameters => !GenericArguments.IsEmpty;

    public override string Name => Signature.Name;
    public override DuneAssemblyReference Assembly => Signature.Assembly;
    public override bool IsVoid => Signature.IsVoid;

    IDuneGenericSignature IDuneGenericReference.Signature => Signature;
    IDuneMemberSignature IDuneMemberReference.Signature => Signature;
    IDuneType? IDuneMember.DeclaringType => DeclaringType;

    public static DuneTypeSignatureReference Void { get; } = DuneTypeSignature.Void.CreateReference();
    public static DuneTypeSignatureReference Object { get; } = DuneTypeSignature.Object.CreateReference();

    internal DuneTypeSignatureReference(DuneTypeSignature signature, ImmutableArray<DuneTypeReference> genericArgs) {
        InternalUtils.Assert(genericArgs.Length == signature.GenericParameterCount);

        Signature = signature;
        GenericArguments = [.. genericArgs];
        IsResolved = !GenericArguments.Any(arg => !arg.IsResolved);

        if (signature.DeclaringType != null) {
            DeclaringType = new(signature.DeclaringType, genericArgs[..signature.DeclaringType.GenericParameterCount]);
        }

    }

    protected internal override DuneTypeReference? TryResolve(DuneTypeSignatureReference? declaringType, DuneMethodReference? declaringMethod, bool force) {
        if (IsResolved) return null;

        DuneTypeReference[] resolvedGenerics = new DuneTypeReference[GenericArguments.Length];
        bool didResolveAny = false;

        for (int i = 0; i < resolvedGenerics.Length; i++) {
            DuneTypeReference? resolvedGeneric = GenericArguments[i].TryResolve(declaringType, declaringMethod, force);

            if (resolvedGeneric == null) {
                resolvedGenerics[i] = GenericArguments[i];
            } else {
                resolvedGenerics[i] = resolvedGeneric;
                didResolveAny = true;
            }
        }

        if (!didResolveAny) return null;

        return new DuneTypeSignatureReference(Signature, [.. resolvedGenerics]);
    }


    StringBuilder IDuneMember.FormatAndAppendName(in DuneTypeFormat format, StringBuilder sb) {
        FormatAndAppendName(format, sb);
        return sb;
    }

    StringBuilder IDuneGenericSymbol.FormatAndAppendGenericParameters(in DuneTypeFormat genericFormat, StringBuilder sb) {
        return genericFormat.AppendTypes(GenericArguments, sb);
    }

    protected internal override void FormatAndAppendName(in DuneTypeFormat format, StringBuilder sb) {
        (Signature as IDuneType).FormatAndAppendName(format, sb);
    }

    public override bool Equals(object? obj) => Equals(obj as DuneTypeSignatureReference);
    public override bool Equals(DuneTypeReference? other) => Equals(other as DuneTypeSignatureReference);
    public bool Equals(DuneTypeSignatureReference? other) {
        if (ReferenceEquals(this, other)) return true;
        if (other is null) return false;
        if (!other.Signature.Equals(Signature)) return false;
        if (!other.GenericArguments.SequenceEqual(GenericArguments)) return false;
        return true;
    }

    public override int GetHashCode() {
        int hash = Signature.GetHashCode();
        foreach (DuneTypeReference generic in GenericArguments)
            hash = InternalUtils.HashCodeCombine(hash, generic);
        return hash;
    }

}
