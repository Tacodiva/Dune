using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Text;
using Mono.Cecil;

namespace Dune;


public interface IDuneCustomAttributeArgument {

    public static IDuneCustomAttributeArgument? FromCustomAttributeTypedArgument(CustomAttributeTypedArgument arg, DuneReflectionContext? ctx = null) {
        if (arg.Value == null) return null;

        if (arg.Value is ReadOnlyCollection<CustomAttributeTypedArgument> argArray) {
            return new DuneCustomAttributeArgumentArray(
                (DuneArrayTypeReference)DuneTypeReference.FromType(arg.ArgumentType, ctx),
                argArray.Select(arg => DuneCustomAttributeArgumentValue.FromCustomAttributeTypedArgument(arg, ctx))
            );
        }

        return DuneCustomAttributeArgumentValue.FromCustomAttributeTypedArgument(arg, ctx);
    }

    public static IDuneCustomAttributeArgument? FromCustomAttributeArgument(CustomAttributeArgument arg, DuneCecilContext? ctx = null) {
        if (arg.Value == null) return null;

        if (arg.Value is CustomAttributeArgument[] argArray) {
            return new DuneCustomAttributeArgumentArray(
                (DuneArrayTypeReference)DuneTypeReference.FromTypeReference(arg.Type, ctx),
                argArray.Select(arg => DuneCustomAttributeArgumentValue.FromCustomAttributeArgument(arg, ctx))
            );
        }

        return DuneCustomAttributeArgumentValue.FromCustomAttributeArgument(arg, ctx);
    }

    public DuneTypeReference Type { get; }
    public object Value { get; }
    public string ToString(DuneTypeFormat format);
}

public sealed class DuneCustomAttributeArgumentArray : IDuneCustomAttributeArgument {
    public DuneArrayTypeReference Type { get; }
    public ImmutableArray<DuneCustomAttributeArgumentValue?> Values { get; }

    DuneTypeReference IDuneCustomAttributeArgument.Type => Type;
    object IDuneCustomAttributeArgument.Value => Values;

    internal DuneCustomAttributeArgumentArray(DuneArrayTypeReference type, IEnumerable<DuneCustomAttributeArgumentValue?> values) {
        Type = type;
        Values = values.ToImmutableArray();
    }

    public override string ToString() => ToString(DuneTypeFormat.DefaultMinimal);

    public string ToString(DuneTypeFormat format) {
        StringBuilder sb = new();

        sb.Append('(');
        format.AppendType(Type, sb);
        sb.Append(") [");

        sb.AppendEnumerable(Values, (value, sb) => {
            if (value is null) sb.Append("null");
            else sb.Append(value.ToString(format));
        });

        sb.Append(']');

        return sb.ToString();
    }
}

public sealed class DuneCustomAttributeArgumentValue : IDuneCustomAttributeArgument {

    internal static DuneCustomAttributeArgumentValue FromCustomAttributeTypedArgument(CustomAttributeTypedArgument arg, DuneReflectionContext? ctx) {
        InternalUtils.Assert(arg.Value != null);
        InternalUtils.Assert(arg.Value is not ReadOnlyCollection<CustomAttributeTypedArgument>);

        object value = arg.Value;

        if (value is Type valueType)
            value = DuneTypeReference.FromType(valueType, ctx);

        return new((DuneTypeSignatureReference)DuneTypeReference.FromType(arg.ArgumentType, ctx), value);
    }

    internal static DuneCustomAttributeArgumentValue? FromCustomAttributeArgument(CustomAttributeArgument arg, DuneCecilContext? ctx) {
        InternalUtils.Assert(arg.Value != null);
        InternalUtils.Assert(arg.Value is not Array);

        object value = arg.Value;

        if (value is CecilTypeReference valueType)
            value = DuneTypeReference.FromTypeReference(valueType, ctx);

        if (value is CustomAttributeArgument argArg)
            return FromCustomAttributeArgument(argArg, ctx);

        return new((DuneTypeSignatureReference)DuneTypeReference.FromTypeReference(arg.Type, ctx), value);
    }


    public DuneTypeSignatureReference Type { get; }
    public object Value { get; }

    DuneTypeReference IDuneCustomAttributeArgument.Type => Type;

    internal DuneCustomAttributeArgumentValue(DuneTypeSignatureReference type, object value) {
        Type = type;
        Value = value;

        switch (Value) {
            case bool:
            case byte:
            case char:
            case double:
            case float:
            case int:
            case long:
            case sbyte:
            case short:
            case string:
            case uint:
            case ulong:
            case ushort:
            case DuneTypeReference:
                break;
            case null:
                throw new ArgumentNullException(nameof(value));
            default:
                throw new ArgumentException($"Value has unknown type {value.GetType()}");
        }
    }

    public override string ToString() => ToString(DuneTypeFormat.DefaultMinimal);

    public string ToString(DuneTypeFormat format) {
        StringBuilder sb = new();

        sb.Append('(');
        format.AppendType(Type, sb);
        sb.Append(") ");

        switch (Value) {
            case bool:
            case byte:
            case char:
            case double:
            case float:
            case int:
            case long:
            case sbyte:
            case short:
            case uint:
            case ulong:
            case ushort:
            case DuneTypeReference:
                sb.Append(Value.ToString());
                break;

            case string str:
                sb.Append('"');
                sb.Append(str.Replace("\\", "\\\\").Replace("\"", "\\\""));
                sb.Append('"');
                break;
        }

        return sb.ToString();
    }
}

public sealed class DuneCustomAttributeContainer : IReadOnlyCollection<DuneCustomAttributeDefinition> {

    public static DuneCustomAttributeContainer Empty { get; } = new([]);

    internal static DuneCustomAttributeContainer FromMemberInfo(MemberInfo memberInfo, DuneReflectionContext? ctx)
        => FromCustomAttributeData(memberInfo.GetCustomAttributesData(), ctx);

    internal static DuneCustomAttributeContainer FromParameterInfo(ParameterInfo parameterInfo, DuneReflectionContext? ctx)
        => FromCustomAttributeData(parameterInfo.GetCustomAttributesData(), ctx);

    internal static DuneCustomAttributeContainer FromCustomAttributeData(IEnumerable<CustomAttributeData> customAttributeDatas, DuneReflectionContext? ctx)
        => new(customAttributeDatas.Select(data => DuneCustomAttributeDefinition.FromCustomAttributeData(data, ctx)));

    internal static DuneCustomAttributeContainer FromCecil(CecilCustomAttributeProvider provider, DuneCecilContext? ctx)
        => new(provider.CustomAttributes.Select(attr => DuneCustomAttributeDefinition.FromCustomAttribute(attr, ctx)));

    public ImmutableArray<DuneCustomAttributeDefinition> Attributes { get; }
    public int Count => Attributes.Length;

    internal DuneCustomAttributeContainer(IEnumerable<DuneCustomAttributeDefinition> attributes) {
        Attributes = attributes.ToImmutableArray();
    }

    public IEnumerator<DuneCustomAttributeDefinition> GetEnumerator() => (Attributes as IEnumerable<DuneCustomAttributeDefinition>).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public override string ToString() => ToString(DuneTypeFormat.Default, DuneTypeFormat.DefaultMinimal);

    public string ToString(DuneTypeFormat constructorFormat, DuneTypeFormat argumentFormat) {
        return new StringBuilder()
            .Append('[')
            .AppendEnumerable(this, (attribute, sb) => sb.Append(attribute.ToString(constructorFormat, argumentFormat)))
            .Append(']')
            .ToString();
    }
}

public sealed class DuneCustomAttributeDefinition {

    public static DuneCustomAttributeDefinition FromCustomAttributeData(CustomAttributeData data, DuneReflectionContext? ctx = null) {
        return new(
            DuneMethodSignature.FromConstructorInfo(data.Constructor, ctx),
            data.ConstructorArguments.Select(arg => IDuneCustomAttributeArgument.FromCustomAttributeTypedArgument(arg, ctx)),
            data.NamedArguments.Select(arg => (arg.MemberName, IDuneCustomAttributeArgument.FromCustomAttributeTypedArgument(arg.TypedValue, ctx)))
        );
    }

    public static DuneCustomAttributeDefinition FromCustomAttribute(CecilCustomAttribute attr, DuneCecilContext? ctx = null) {
        return new(
            DuneMethodSignature.FromMethodDefinition(attr.Constructor.Resolve(), ctx),
            attr.ConstructorArguments.Select(arg => IDuneCustomAttributeArgument.FromCustomAttributeArgument(arg, ctx)),
            attr.Fields.Concat(attr.Properties)
                .Select(arg => (arg.Name, IDuneCustomAttributeArgument.FromCustomAttributeArgument(arg.Argument, ctx)))
        );
    }

    public DuneMethodSignature Constructor { get; }
    public DuneTypeSignature Type => Constructor.DeclaringType!;

    public ImmutableArray<IDuneCustomAttributeArgument?> ConstructorArguments { get; }
    public ImmutableArray<(string Name, IDuneCustomAttributeArgument? Argument)> NamedArguments { get; }

    internal DuneCustomAttributeDefinition(DuneMethodSignature constructor, IEnumerable<IDuneCustomAttributeArgument?> constructorArguments, IEnumerable<(string, IDuneCustomAttributeArgument?)> namedArguments) {
        Constructor = constructor;
        ConstructorArguments = constructorArguments.ToImmutableArray();
        NamedArguments = namedArguments.ToImmutableArray();
    }

    public override string ToString() => ToString(DuneTypeFormat.Default, DuneTypeFormat.DefaultMinimal);

    public string ToString(DuneTypeFormat constructorFormat, DuneTypeFormat argumentFormat) {
        StringBuilder sb = new();
        constructorFormat.AppendType(Type, sb);
        sb.Append('(');
        sb.AppendEnumerable(
            ConstructorArguments.Select(arg => ((string?)null, arg))
                .Concat(NamedArguments.Select(arg => ((string?)arg.Name, arg.Argument))),
            (arg, sb) => {
                if (arg.Item1 != null) {
                    sb.Append(arg.Item1);
                    sb.Append(" = ");
                }
                sb.Append(arg.Item2?.ToString(argumentFormat) ?? "null");
            }
        );
        sb.Append(')');

        return sb.ToString();
    }
}
