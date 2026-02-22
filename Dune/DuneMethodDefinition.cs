
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;

namespace Dune;

public sealed record class DuneMethodParameterDefinition(
    DuneMethodParameter InnerParameter,
    DuneCustomAttributeContainer CustomAttributes
) {
    public string Name => InnerParameter.Name;
    public DuneTypeReference Type => InnerParameter.Type;
}

public sealed class DuneMethodDefinition : DuneDefinition<DuneMethodSignature> {

    public static DuneMethodDefinition FromDelegate(Delegate @delegate, DuneReflectionContext? ctx = null) {
        InternalUtils.ThrowIfArgumentNull(@delegate);
        return FromMethodInfo(@delegate.Method, ctx);
    }

    public static DuneMethodDefinition FromMethodInfo(MethodInfo methodInfo, DuneReflectionContext? ctx = null) {
        ctx ??= new();

        methodInfo = DuneMethodSignature.GetGenericMethodDefinition(methodInfo);

        return new(
            DuneMethodSignature.FromMethodInfo(methodInfo, ctx),
            DuneCustomAttributeContainer.FromMemberInfo(methodInfo, ctx),
            DuneCustomAttributeContainer.FromParameterInfo(methodInfo.ReturnParameter, ctx),
            methodInfo.GetParameters().Select(param => DuneCustomAttributeContainer.FromParameterInfo(param, ctx)),
            methodInfo.GetGenericArguments().Select(arg => DuneCustomAttributeContainer.FromMemberInfo(arg, ctx))
        );
    }

    public static DuneMethodDefinition FromConstructorInfo(ConstructorInfo constructorInfo, DuneReflectionContext? ctx = null) {
        ctx ??= new();

        constructorInfo = DuneMethodSignature.GetGenericMethodDefinition(constructorInfo);

        return new(
            DuneMethodSignature.FromConstructorInfo(constructorInfo, ctx),
            DuneCustomAttributeContainer.FromMemberInfo(constructorInfo, ctx),
            DuneCustomAttributeContainer.Empty,
            constructorInfo.GetParameters().Select(param => DuneCustomAttributeContainer.FromParameterInfo(param, ctx)),
            []
        );
    }

    public static DuneMethodDefinition FromMethodBase(MethodBase methodBase, DuneReflectionContext? ctx = null) {
        InternalUtils.ThrowIfArgumentNull(methodBase);

        return methodBase switch {
            MethodInfo methodInfo => FromMethodInfo(methodInfo, ctx),
            ConstructorInfo constructorInfo => FromConstructorInfo(constructorInfo, ctx),
            _ => throw new ArgumentException($"Unknown method type {methodBase.GetType()}"),
        };
    }

    public static DuneMethodDefinition FromMethodDefinition(CecilMethodDefinition methodDefinition, DuneCecilContext? ctx = null) {
        ctx ??= new();

        return new(
            DuneMethodSignature.FromMethodDefinition(methodDefinition, ctx),
            DuneCustomAttributeContainer.FromCecil(methodDefinition, ctx),
            DuneCustomAttributeContainer.FromCecil(methodDefinition.MethodReturnType, ctx),
            methodDefinition.Parameters.Select(param => DuneCustomAttributeContainer.FromCecil(param, ctx)),
            methodDefinition.GenericParameters.Select(arg => DuneCustomAttributeContainer.FromCecil(arg, ctx))
        );
    }


    public DuneCustomAttributeContainer ReturnTypeCustomAttributes { get; }
    public ImmutableArray<DuneMethodParameterDefinition> Parameters { get; }
    public ImmutableArray<DuneGenericParameterDefinition> GenericParameters { get; }

    public DuneTypeReference? ReturnType => Signature.ReturnType;
    public DuneTypeSignature? DeclaringType => Signature.DeclaringType;

    internal DuneMethodDefinition(
        DuneMethodSignature signature,
        DuneCustomAttributeContainer customAttributes,
        DuneCustomAttributeContainer returnTypeCustomAttributes,
        IEnumerable<DuneCustomAttributeContainer> parameterCustomAttributes,
        IEnumerable<DuneCustomAttributeContainer> genericCustomAttributes
    ) : base(signature, customAttributes) {
        ReturnTypeCustomAttributes = returnTypeCustomAttributes;

        Parameters = parameterCustomAttributes
            .Select((paramAttributes, i) => new DuneMethodParameterDefinition(signature.Parameters[i], paramAttributes))
            .ToImmutableArray();
        InternalUtils.Assert(Parameters.Length == signature.Parameters.Length);

        GenericParameters = genericCustomAttributes.Select(
            (paramAttributes, i) => new DuneGenericParameterDefinition(signature.GenericParameterNames[i], paramAttributes)
        ).ToImmutableArray();
        InternalUtils.Assert(GenericParameters.Length == signature.GenericParameterNames.Length);
    }

}