
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Dune;

public sealed record class DuneMethodParameter(string Name, DuneTypeReference Type) {
    public override string ToString() => ToString(DuneTypeFormat.DefaultMinimal);
    public string ToString(DuneTypeFormat format) {
        StringBuilder sb = new();

        format.AppendType(Type, sb);
        sb.Append(' ');
        sb.Append(Name);

        return sb.ToString();
    }
}

public sealed class DuneMethodSignature : IDuneMemberSignature, IDuneGenericSignature, IEquatable<DuneMethodSignature> {

    /// <summary>
    /// Removes generic arguments from a MethodBase.
    /// Takes in a method like `MyClass<int>:MyMethod<double>` and converts in its definition (like `MyClass<T>:MyMethod<V>`)
    /// </summary>
    internal static T GetGenericMethodDefinition<T>(T method) where T : MethodBase {
        if (method is MethodInfo methodInfo) {
            if (methodInfo.IsGenericMethod && !methodInfo.IsGenericMethodDefinition)
                method = (T)(MethodBase)methodInfo.GetGenericMethodDefinition();
        }

        if (method.DeclaringType != null) {
            Type declaringType = method.DeclaringType;
            if (declaringType.IsGenericType && !declaringType.IsGenericTypeDefinition) {
                // We need to make the declaring type into a generic type definition :c
                method = (T)MethodBase.GetMethodFromHandle(
                    method.MethodHandle, method.DeclaringType.GetGenericTypeDefinition().TypeHandle
                )!;
            }
        }

        return method;
    }

    public static DuneMethodSignature FromDelegate(Delegate @delegate, DuneReflectionContext? ctx = null) {
        InternalUtils.ThrowIfArgumentNull(@delegate);
        return FromMethodInfo(@delegate.Method, ctx);
    }

    public static DuneMethodSignature FromMethodInfo(MethodInfo methodInfo, DuneReflectionContext? ctx = null) {
        InternalUtils.ThrowIfArgumentNull(methodInfo);

        ctx ??= new();
        if (ctx.TryGetMethodSignature(methodInfo, out var cached))
            return cached;

        methodInfo = GetGenericMethodDefinition(methodInfo);

        if (ctx.TryGetMethodSignature(methodInfo, out cached))
            return cached;

        return ctx.PutMethodSignature(methodInfo, new(
            DuneAssemblyReference.FromAssembly(methodInfo.Module.Assembly, ctx),
            methodInfo.DeclaringType == null ?
                null : DuneTypeSignature.FromType(methodInfo.DeclaringType, ctx),
            methodInfo.Name,
            methodInfo.GetGenericArguments().Select(arg => arg.Name),

            (method) => {
                ctx.PutMethodSignature(methodInfo, method);
                return DuneTypeReference.FromType(methodInfo.ReturnType, ctx);
            },

            (method) => {
                ctx.PutMethodSignature(methodInfo, method);
                return methodInfo.GetParameters().Select(
                    param => new DuneMethodParameter(
                        param.Name ?? "?",
                        DuneTypeReference.FromType(param.ParameterType, ctx)
                    )
                );
            }
        ));
    }

    public static DuneMethodSignature FromConstructorInfo(ConstructorInfo constructorInfo, DuneReflectionContext? ctx = null) {
        InternalUtils.ThrowIfArgumentNull(constructorInfo);

        ctx ??= new();
        if (ctx.TryGetMethodSignature(constructorInfo, out var cached))
            return cached;

        constructorInfo = GetGenericMethodDefinition(constructorInfo);

        return ctx.PutMethodSignature(constructorInfo, new(
            DuneAssemblyReference.FromAssembly(constructorInfo.Module.Assembly, ctx),
            constructorInfo.DeclaringType == null ?
                null : DuneTypeSignature.FromType(constructorInfo.DeclaringType, ctx),
            constructorInfo.Name, [], (method) => null,

            (method) => {
                ctx.PutMethodSignature(constructorInfo, method);

                return constructorInfo.GetParameters().Select(
                    param => new DuneMethodParameter(
                        param.Name ?? "?",
                        DuneTypeReference.FromType(param.ParameterType, ctx)
                    )
                );
            }
        ));
    }

    public static DuneMethodSignature FromMethodBase(MethodBase methodBase, DuneReflectionContext? ctx = null) {
        InternalUtils.ThrowIfArgumentNull(methodBase);

        return methodBase switch {
            MethodInfo methodInfo => FromMethodInfo(methodInfo, ctx),
            ConstructorInfo constructorInfo => FromConstructorInfo(constructorInfo, ctx),
            _ => throw new ArgumentException($"Unknown method type {methodBase.GetType()}"),
        };
    }

    public static DuneMethodSignature FromMethodDefinition(CecilMethodDefinition methodDefinition, DuneCecilContext? ctx = null) {
        InternalUtils.ThrowIfArgumentNull(methodDefinition);

        ctx ??= new();
        if (ctx.TryGetMethodSignature(methodDefinition, out var cached))
            return cached;

        return ctx.PutMethodSignature(methodDefinition, new(
            DuneAssemblyReference.FromAssemblyDefinition(methodDefinition.Module.Assembly, ctx),
            methodDefinition.DeclaringType == null ?
                null : DuneTypeSignature.FromTypeDefinition(methodDefinition.DeclaringType, ctx),
            methodDefinition.Name,
            methodDefinition.GenericParameters.Select(param => param.Name),

            (method) => {
                ctx.PutMethodSignature(methodDefinition, method);
                return DuneTypeReference.FromTypeReference(methodDefinition.ReturnType, ctx);
            },

            (method) => {
                ctx.PutMethodSignature(methodDefinition, method);
                return methodDefinition.Parameters.Select(
                    param => new DuneMethodParameter(
                        param.Name,
                        DuneTypeReference.FromTypeReference(param.ParameterType, ctx)
                    )
                );
            }
        ));
    }

    public static DuneMethodSignature FromSymbol(IMethodSymbol methodSymbol, DuneRoslynContext? ctx = null) {
        InternalUtils.ThrowIfArgumentNull(methodSymbol);

        methodSymbol = methodSymbol.OriginalDefinition;

        ctx ??= new();
        if (ctx.TryGetMethodSignature(methodSymbol, out var cached))
            return cached;

        return ctx.PutMethodSignature(methodSymbol, new(
            DuneAssemblyReference.FromSymbol(methodSymbol.ContainingAssembly, ctx),
            methodSymbol.ContainingType == null ?
                null : DuneTypeSignature.FromSymbol(methodSymbol.ContainingType, ctx),
            methodSymbol.MetadataName,
            methodSymbol.TypeParameters.Select(param => param.Name),

            (method) => {
                ctx.PutMethodSignature(methodSymbol, method);

                return DuneTypeReference.FromSymbol(
                    methodSymbol.ReturnType, 
                    methodSymbol.ReturnsByRefReadonly || methodSymbol.ReturnsByRef,
                    ctx
                );
            },

            (method) => {
                ctx.PutMethodSignature(methodSymbol, method);

                return methodSymbol.Parameters.Select(
                    param => new DuneMethodParameter(
                        param.MetadataName,
                        DuneTypeReference.FromSymbol(
                            param.Type, param.RefKind, ctx
                        )
                    )
                );
            }
        ));
    }

    public DuneAssemblyReference Assembly { get; }

    public DuneTypeSignature? DeclaringType { get; }
    public string Name { get; }
    public ImmutableArray<string> GenericParameterNames { get; }
    public DuneTypeReference? ReturnType { get; }
    public ImmutableArray<DuneMethodParameter> Parameters { get; }

    IDuneType? IDuneMember.DeclaringType => DeclaringType;

    public bool HasGenericParameters => !GenericParameterNames.IsEmpty;
    public int GenericParameterCount => GenericParameterNames.Length;

    internal DuneMethodSignature(
        DuneAssemblyReference assembly,
        DuneTypeSignature? declaringType,
        string name,
        IEnumerable<string> genericParamNames,
        Func<DuneMethodSignature, DuneTypeReference?> returnTypeDelegate,
        Func<DuneMethodSignature, IEnumerable<DuneMethodParameter>> parametersDelegate
    ) {
        Assembly = assembly;
        DeclaringType = declaringType;
        GenericParameterNames = [.. genericParamNames];
        Name = name;
        DuneTypeReference? returnType = returnTypeDelegate(this);
        ReturnType = (returnType?.IsVoid ?? true) ? null : returnType;
        Parameters = [.. parametersDelegate(this)];
    }

    public DuneMethodReference CreateReference(DuneTypeSignatureReference declaringTypeReference)
        => CreateReference(declaringTypeReference, [.. Enumerable.Repeat<DuneTypeReference>(DuneUnknownTypeReference.Instance, GenericParameterCount)]);

    public DuneMethodReference CreateReference(DuneTypeSignatureReference declaringTypeReference, params ImmutableArray<DuneTypeReference> genericArgs) {
        if (!declaringTypeReference.Signature.Equals(DeclaringType))
            throw new ArgumentException($"{nameof(declaringTypeReference)} must reference the same type definition as the method's declaring type.");

        if (genericArgs.Length != GenericParameterCount)
            throw new ArgumentException($"Wrong number of generic arguments. Expected {GenericParameterCount} got {genericArgs.Length}.");

        return new(this, declaringTypeReference, genericArgs);
    }

    public override string ToString()
        => ToString(DuneTypeFormat.Default, DuneTypeFormat.DefaultMinimal, DuneTypeFormat.DefaultMinimal);

    public string ToString(in DuneTypeFormat methodFormat)
        => ToString(methodFormat, DuneTypeFormat.DefaultMinimal, DuneTypeFormat.DefaultMinimal);

    public string ToString(in DuneTypeFormat methodFormat, DuneTypeFormat? returnFormat, DuneTypeFormat? parameterFormat) {
        StringBuilder sb = new();

        if (returnFormat != null) {
            returnFormat.Value.AppendType(ReturnType, sb);
            sb.Append(' ');
        }

        methodFormat.AppendMemberName(this, sb);
        methodFormat.AppendGenericParameters(this, sb);

        if (parameterFormat != null) {
            sb.Append('(');

            sb.AppendEnumerable(
                Parameters,
                (param, sb) => {
                    parameterFormat.Value.AppendType(param.Type, sb);
                    sb.Append(' ');
                    sb.Append(param.Name);
                }
            );

            sb.Append(')');
        }

        methodFormat.AppendAssemblySuffix(this, sb);

        return sb.ToString();
    }

    public override bool Equals(object? obj) => Equals(obj as DuneMethodSignature);
    public bool Equals(IDuneSymbol? other) => Equals(other as DuneMethodSignature);
    public bool Equals(DuneMethodSignature? other) {
        if (ReferenceEquals(this, other)) return true;
        if (other is null) return false;

        if (Name != other.Name) return false;
        if (DeclaringType != other.DeclaringType) return false;
        if (ReturnType != other.ReturnType) return false;
        if (!Parameters.SequenceEqual(other.Parameters)) return false;

        return true;
    }

    public static bool operator ==(DuneMethodSignature? a, DuneMethodSignature? b) => a?.Equals(b) ?? b is null;
    public static bool operator !=(DuneMethodSignature? a, DuneMethodSignature? b) => !(a == b);
    
    public override int GetHashCode() {
        int hashCode = InternalUtils.HashCodeCombine(DeclaringType, Name, GenericParameterCount, ReturnType);
        foreach (DuneMethodParameter param in Parameters)
            hashCode = InternalUtils.HashCodeCombine(hashCode, param);
        return hashCode;
    }

    StringBuilder IDuneMember.FormatAndAppendName(in DuneTypeFormat format, StringBuilder sb) {
        return sb.Append(Name);
    }

    StringBuilder IDuneGenericSymbol.FormatAndAppendGenericParameters(in DuneTypeFormat genericFormat, StringBuilder sb) {
        return sb.Append(string.Join(", ", GenericParameterNames));
    }

}