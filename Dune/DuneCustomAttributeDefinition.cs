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
                (DuneArrayTypeReference)DuneTypeReference.FromCecilReference(arg.Type, ctx),
                argArray.Select(arg => DuneCustomAttributeArgumentValue.FromCustomAttributeArgument(arg, ctx))
            );
        }

        return DuneCustomAttributeArgumentValue.FromCustomAttributeArgument(arg, ctx);
    }

    public DuneTypeReference Type { get; }
    public object Value { get; }
    public string ToString(DuneTypeFormat format);
    public object GetRuntimeValue(DuneReflectionContext? ctx = null);
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

    public Array GetRuntimeValue(DuneReflectionContext? ctx = null) {
        Type arrayType = Type.GetRuntimeType(ctx);
        Array array = Array.CreateInstanceFromArrayType(arrayType, Values.Length);

        for (int i = 0; i < array.Length; i++)
            array.SetValue(Values[i]?.GetRuntimeValue(ctx), i);

        return array;
    }

    object IDuneCustomAttributeArgument.GetRuntimeValue(DuneReflectionContext? ctx)
        => GetRuntimeValue(ctx);

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
            value = DuneTypeReference.FromCecilReference(valueType, ctx);

        if (value is CustomAttributeArgument argArg)
            return FromCustomAttributeArgument(argArg, ctx);

        return new((DuneTypeSignatureReference)DuneTypeReference.FromCecilReference(arg.Type, ctx), value);
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

    public object GetRuntimeValue(DuneReflectionContext? ctx = null) {
        object value = Value;
        if (value is DuneTypeReference typeReference)
            value = typeReference.GetRuntimeType(ctx);

        Type type = Type.GetRuntimeType(ctx);

        if (value.GetType().IsAssignableTo(type))
            return value;

        if (type.IsEnum)
            return Enum.ToObject(type, value);

        if (value is IConvertible)
            return Convert.ChangeType(value, type);

        throw new NotSupportedException($"Cannot convert value from type {value.GetType()} into {type}.");
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

public sealed class DuneCustomAttributeContainer : IReadOnlyList<DuneCustomAttributeDefinition> {

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
    public DuneCustomAttributeDefinition this[int index] => Attributes[index];

    internal DuneCustomAttributeContainer(IEnumerable<DuneCustomAttributeDefinition> attributes) {
        Attributes = attributes.ToImmutableArray();
    }

    public IEnumerator<DuneCustomAttributeDefinition> GetEnumerator() => (Attributes as IEnumerable<DuneCustomAttributeDefinition>).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public override string ToString() => ToString(DuneTypeFormat.Default, DuneTypeFormat.DefaultMinimal);

    public T? TryGetAttribute<T>(DuneReflectionContext? ctx = null) where T : Attribute
        => GetAttributes<T>(ctx).FirstOrDefault();

    public IEnumerable<T> GetAttributes<T>(DuneReflectionContext? ctx = null) where T : Attribute {
        return GetAttributes(typeof(T), ctx).Cast<T>();
    }
    
    public IEnumerable<Attribute> GetAttributes(Type attributeType, DuneReflectionContext? ctx = null) {
        DuneTypeSignature attributeSignature = DuneTypeSignature.FromType(attributeType, ctx);

        foreach (DuneCustomAttributeDefinition attribute in this) {
            MethodBase? foundConstructorMethod = attributeType.TryGetMethod(attribute.Constructor, ctx);

            if (foundConstructorMethod is not ConstructorInfo foundConstructor)
                continue;

            object?[] runtimeConstructorArguments = attribute.ConstructorArguments
                .Select(arg => arg?.GetRuntimeValue(ctx))
                .ToArray();

            Attribute runtimeAttribute = (Attribute)foundConstructor.Invoke(runtimeConstructorArguments);

            foreach (DuneCustomAttributeDefinition.NamedArgument namedArg in attribute.NamedArguments) {
                object? namedArgValue = namedArg.Value?.GetRuntimeValue(ctx);

                switch (namedArg.Type) {
                    case DuneCustomAttributeDefinition.NamedArgumentType.Property: {
                            PropertyInfo property = attributeType.GetProperty(namedArg.Name, DuneReflectionContext.EverythingPublicFlags)
                                ?? throw new DuneException($"Could not find property named '{namedArg.Name}' on type {attributeType}");

                            property.SetValue(runtimeAttribute, namedArgValue);
                            break;
                        }
                    case DuneCustomAttributeDefinition.NamedArgumentType.Field: {
                            FieldInfo field = attributeType.GetField(namedArg.Name, DuneReflectionContext.EverythingPublicFlags)
                                ?? throw new DuneException($"Could not find field named '{namedArg.Name}' on type {attributeType}");

                            field.SetValue(runtimeAttribute, namedArgValue);
                            break;
                        }
                }
            }

            yield return runtimeAttribute;
        }
    }

    public DuneCustomAttributeDefinition? GetAttribute(DuneTypeSignature attributeType)
        => GetAttributes(attributeType).FirstOrDefault();

    public IEnumerable<DuneCustomAttributeDefinition> GetAttributes(DuneTypeSignature attributeType)
        => this.Where(attribute => attribute.Type == attributeType);

    public string ToString(DuneTypeFormat constructorFormat, DuneTypeFormat argumentFormat) {
        return new StringBuilder()
            .Append('[')
            .AppendEnumerable(this, (attribute, sb) => sb.Append(attribute.ToString(constructorFormat, argumentFormat)))
            .Append(']')
            .ToString();
    }
}

public sealed class DuneCustomAttributeDefinition {

    public enum NamedArgumentType {
        Field,
        Property
    }

    public record class NamedArgument(
        NamedArgumentType Type,
        string Name,
        IDuneCustomAttributeArgument? Value
    );

    public static DuneCustomAttributeDefinition FromCustomAttributeData(CustomAttributeData data, DuneReflectionContext? ctx = null) {
        return new(
            DuneMethodSignature.FromConstructorInfo(data.Constructor, ctx),
            data.ConstructorArguments.Select(arg => IDuneCustomAttributeArgument.FromCustomAttributeTypedArgument(arg, ctx)),
            data.NamedArguments.Select(
                arg => new NamedArgument(
                    arg.IsField ? NamedArgumentType.Field : NamedArgumentType.Property,
                    arg.MemberName,
                    IDuneCustomAttributeArgument.FromCustomAttributeTypedArgument(arg.TypedValue, ctx)
                )
            )
        );
    }

    public static DuneCustomAttributeDefinition FromCustomAttribute(CecilCustomAttribute attr, DuneCecilContext? ctx = null) {
        return new(
            DuneMethodSignature.FromCecilDefinition(attr.Constructor.Resolve(), ctx),
            attr.ConstructorArguments.Select(arg => IDuneCustomAttributeArgument.FromCustomAttributeArgument(arg, ctx)),
            attr.Fields.Select(
                arg => new NamedArgument(NamedArgumentType.Field, arg.Name, IDuneCustomAttributeArgument.FromCustomAttributeArgument(arg.Argument, ctx))
            ).Concat(attr.Properties.Select(
                arg => new NamedArgument(NamedArgumentType.Property, arg.Name, IDuneCustomAttributeArgument.FromCustomAttributeArgument(arg.Argument, ctx))
            ))
        );
    }

    public DuneMethodSignature Constructor { get; }
    public DuneTypeSignature Type => Constructor.DeclaringType!;

    public ImmutableArray<IDuneCustomAttributeArgument?> ConstructorArguments { get; }
    public ImmutableArray<NamedArgument> NamedArguments { get; }

    internal DuneCustomAttributeDefinition(DuneMethodSignature constructor, IEnumerable<IDuneCustomAttributeArgument?> constructorArguments, IEnumerable<NamedArgument> namedArguments) {
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
                .Concat(NamedArguments.Select(arg => ((string?)arg.Name, arg.Value))),
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
