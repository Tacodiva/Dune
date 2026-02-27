
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Dune;

partial class DuneSandboxRules {

    public static string Stringify(DuneSandboxRules rules) {
        StringBuilder sb = new();

        if (rules.AllowTypedReferenceKeywords) sb.AppendLine($"Allow TypeRef: {rules.AllowTypedReferenceKeywords}");
        if (rules.AllowDynamicKeyword) sb.AppendLine($"Allow Dynamic: {rules.AllowDynamicKeyword}");
        if (rules.AllowUnsafe) sb.AppendLine($"Allow Unsafe: {rules.AllowUnsafe}");

        foreach (DuneSandboxAssemblyRules assemblyRules in rules._assemblyRules.Values.OrderBy(rules => rules.Assembly.Name)) {

            sb.AppendLine($"Assembly '{assemblyRules.Assembly.ToString()}'");
            if (!assemblyRules.Allow) sb.AppendLine($"  Allow: {assemblyRules.Allow}");

            foreach (DuneSandboxTypeRules typeRules in assemblyRules._types.Values.OrderBy(rules => rules.Type.Name)) {
                sb.AppendLine($"  Type '{typeRules.Type}'");
                if (!assemblyRules.Allow) sb.AppendLine($"    Allow: {typeRules.Allow}");

                DuneTypeFormat memberFormat = DuneTypeFormat.Default
                    .WithIncludeDeclaringTypes(false)
                    .WithIncludeNamespace(false);

                foreach (DuneMethodSignature allowedMethod in typeRules._allowedMethods.OrderBy(method => method.Name))
                    sb.AppendLine($"    Method: '{allowedMethod.ToString(memberFormat)}'");

                foreach (DuneFieldSignature allowedField in typeRules._allowedFields.OrderBy(method => method.Name))
                    sb.AppendLine($"    Field: '{allowedField.ToString(memberFormat)}'");

            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    private const byte LinePrefixAssemblyDef = (byte)'a';
    private const byte LinePrefixAssemblyAllow = (byte)'b';
    private const byte LinePrefixTypeDef = (byte)'t';
    private const byte LinePrefixTypeAllow = (byte)'u';
    private const byte LinePrefixMethodAllowDef = (byte)'m';
    private const byte LinePrefixFieldAllowDef = (byte)'f';
    private const byte LinePrefixEOF = (byte)'\0';

    private const byte TypeCharPointer = (byte)'*';
    private const byte TypeCharRef = (byte)'&';
    private const byte TypeCharArray = (byte)'a';
    private const byte TypeCharUnknown = (byte)'?';
    private const byte TypeCharFunctionPointer = (byte)'f';
    private const byte TypeCharTypeGeneric = (byte)'t';
    private const byte TypeCharMethodGeneric = (byte)'m';
    private const byte TypeCharType = (byte)'@';
    private const byte TypeCharNull = (byte)'\0';


    private const string Header = "Dune Rule File (v1)";
    private static readonly byte[] _HeaderBytes = Encoding.UTF8.GetBytes(Header);

    public static byte[] Serialize(DuneSandboxRules rules) {
        using MemoryStream ms = new();
        Serialize(rules, ms);
        return ms.ToArray();
    }

    public static void Serialize(DuneSandboxRules rules, Stream output) {
        using BinaryWriter writer = new(output, Encoding.UTF8);

        List<DuneAssemblyReference> assemblies = [];
        List<DuneTypeSignature> types = [];

        void AppendAndResetLine(BinaryWriter dst, MemoryStream src) {
            dst.Write(src.ToArray(), 0, (int)src.Position);
            src.Seek(0, SeekOrigin.Begin);
        }

        int GetAssemblyReference(DuneAssemblyReference assembly) {
            int index = assemblies.IndexOf(assembly);

            if (index == -1) {
                index = assemblies.Count;
                assemblies.Add(assembly);

                writer.Write(LinePrefixAssemblyDef);
                writer.Write(assembly.Name);
                writer.Write(assembly.CultureName ?? "");
                writer.Write(assembly.Version?.ToString() ?? "");
            }

            return index;
        }

        int GetTypeDefinitionReference(DuneTypeSignature type) {
            int index = types.IndexOf(type);

            if (index == -1) {
                int assemblyReference = GetAssemblyReference(type.Assembly);
                int declaringTypeReference = type.HasDeclaringType ?
                     (GetTypeDefinitionReference(type.DeclaringType) + 1) : 0;

                index = types.Count;
                types.Add(type);

                writer.Write(LinePrefixTypeDef);
                writer.DuneWrite7BitEncodedInt(assemblyReference);
                writer.DuneWrite7BitEncodedInt(declaringTypeReference);
                writer.Write(type.Namespace ?? "");
                writer.Write(type.RawName);

                writer.DuneWrite7BitEncodedInt(type.GenericParameterCount);
                foreach (string genericName in type.GenericParameterNames)
                    writer.Write(genericName);
            }

            return index;
        }

        void AppendTypeReference(BinaryWriter sb, DuneTypeReference? typeRef) {
            switch (typeRef) {
                case null:
                    sb.Write(TypeCharNull);
                    break;

                case DunePointerTypeReference pointerRef: {
                        sb.Write(TypeCharPointer);
                        AppendTypeReference(sb, pointerRef.Element);
                        break;
                    }

                case DuneFunctionPointerTypeReference funcPointerRef: {
                        sb.Write(TypeCharFunctionPointer);
                        sb.Write(funcPointerRef.IsUnmanaged);
                        AppendTypeReference(sb, funcPointerRef.ReturnType);
                        sb.DuneWrite7BitEncodedInt(funcPointerRef.Parameters.Length);
                        foreach (DuneTypeReference parameter in funcPointerRef.Parameters)
                            AppendTypeReference(sb, parameter);
                        break;
                    }

                case DuneArrayTypeReference arrayRef: {
                        sb.Write(TypeCharArray);
                        sb.DuneWrite7BitEncodedInt(arrayRef.DimensionCount);
                        AppendTypeReference(sb, arrayRef.Element);
                        break;
                    }

                case DuneRefTypeReference refRef: {
                        sb.Write(TypeCharRef);
                        AppendTypeReference(sb, refRef.Element);
                        break;
                    }

                case DuneUnknownTypeReference:
                    sb.Write(TypeCharUnknown);
                    break;

                case DuneGenericTypeReference genericRef: {
                        if (genericRef.Source == DuneGenericSource.Method) {
                            sb.Write(TypeCharMethodGeneric);
                        } else {
                            sb.Write(TypeCharTypeGeneric);
                        }

                        sb.DuneWrite7BitEncodedInt(genericRef.Index);
                        sb.Write(genericRef.Name);
                        sb.DuneWrite7BitEncodedInt(genericRef.Assembly == null ?
                            0 : (GetAssemblyReference(genericRef.Assembly) + 1));
                        break;
                    }

                case DuneTypeSignatureReference defRef: {
                        sb.Write(TypeCharType);
                        sb.DuneWrite7BitEncodedInt(GetTypeDefinitionReference(defRef.Signature));

                        foreach (DuneTypeReference genericArg in defRef.GenericArguments)
                            AppendTypeReference(sb, genericArg);

                        break;
                    }

                default:
                    Debug.Fail($"Unhandled type reference {typeRef.GetType()}.");
                    break;

            }
        }

        writer.Write(_HeaderBytes);

        writer.Write(rules.AllowTypedReferenceKeywords);
        writer.Write(rules.AllowDynamicKeyword);
        writer.Write(rules.AllowUnsafe);

        using MemoryStream lineMemory = new();
        using BinaryWriter line = new(lineMemory);

        foreach (DuneSandboxAssemblyRules assemblyRules in rules._assemblyRules.Values) {

            if (assemblyRules.Allow) {
                int assemblyReference = GetAssemblyReference(assemblyRules.Assembly);

                writer.Write(LinePrefixAssemblyAllow);
                writer.DuneWrite7BitEncodedInt(assemblyReference);
            }

            foreach (DuneSandboxTypeRules typeRules in assemblyRules._types.Values) {

                if (typeRules.Allow) {
                    int typeReference = GetTypeDefinitionReference(typeRules.Type);

                    writer.Write(LinePrefixTypeAllow);
                    writer.DuneWrite7BitEncodedInt(typeReference);
                }

                foreach (DuneFieldSignature allowedField in typeRules._allowedFields) {
                    line.Write(LinePrefixFieldAllowDef);

                    line.DuneWrite7BitEncodedInt(GetTypeDefinitionReference(allowedField.DeclaringType!));
                    line.Write(allowedField.Name);
                    AppendTypeReference(line, allowedField.Type);

                    AppendAndResetLine(writer, lineMemory);
                }

                foreach (DuneMethodSignature allowedMethod in typeRules._allowedMethods) {
                    line.Write(LinePrefixMethodAllowDef);

                    line.DuneWrite7BitEncodedInt(GetTypeDefinitionReference(allowedMethod.DeclaringType!));
                    line.Write(allowedMethod.Name);
                    
                    // Skip writing the return type for constructors as the return type is inferred when we deserialize.
                    AppendTypeReference(line, allowedMethod.IsConstructor ? null : allowedMethod.ReturnType);

                    line.DuneWrite7BitEncodedInt(allowedMethod.Parameters.Length);

                    foreach (DuneMethodParameter param in allowedMethod.Parameters) {
                        line.Write(param.Name);
                        AppendTypeReference(line, param.Type);
                    }

                    line.DuneWrite7BitEncodedInt(allowedMethod.GenericParameterCount);
                    foreach (string param in allowedMethod.GenericParameterNames)
                        line.Write(param);

                    AppendAndResetLine(writer, lineMemory);
                }
            }
        }

        writer.Write(LinePrefixEOF);
    }

    public static DuneSandboxRules Deserialize(byte[] input) {
        using MemoryStream ms = new(input);
        return Deserialize(ms);
    }

    public static DuneSandboxRules Deserialize(Stream input) {
        BinaryReader reader = new(input);

        DuneSandboxRules rules = new(false);

        List<DuneAssemblyReference> assemblies = [];
        List<DuneTypeSignature> types = [];

        byte[] header = reader.ReadBytes(_HeaderBytes.Length);

        if (!header.SequenceEqual(_HeaderBytes))
            throw new InvalidDataException("Unknown file type (invalid dune header).");

        rules.AllowTypedReferenceKeywords = reader.ReadBoolean();
        rules.AllowDynamicKeyword = reader.ReadBoolean();
        rules.AllowUnsafe = reader.ReadBoolean();

        DuneTypeReference ReadTypeReference() =>
            ReadNullableTypeReference() ?? throw new InvalidDataException("Unexpected null type.");

        DuneTypeReference? ReadNullableTypeReference() {
            byte typeChar = reader.ReadByte();

            switch (typeChar) {
                case TypeCharNull:
                    return null;

                case TypeCharPointer:
                    return new DunePointerTypeReference(ReadTypeReference());

                case TypeCharFunctionPointer: {
                        bool isUnmanaged = reader.ReadBoolean();
                        DuneTypeReference? returnType = ReadNullableTypeReference();

                        int paramCount = reader.DuneRead7BitEncodedInt();
                        DuneTypeReference[] parameters = new DuneTypeReference[paramCount];
                        for (int i = 0; i < paramCount; i++)
                            parameters[i] = ReadTypeReference();

                        return new DuneFunctionPointerTypeReference(
                            returnType, parameters, isUnmanaged
                        );
                    }

                case TypeCharArray: {
                        int dimensions = reader.DuneRead7BitEncodedInt();
                        return new DuneArrayTypeReference(ReadTypeReference(), dimensions);
                    }

                case TypeCharRef:
                    return new DuneRefTypeReference(ReadTypeReference());

                case TypeCharUnknown:
                    return DuneUnknownTypeReference.Instance;

                case TypeCharMethodGeneric:
                case TypeCharTypeGeneric: {

                        DuneGenericSource source = typeChar == TypeCharMethodGeneric ? DuneGenericSource.Method : DuneGenericSource.Type;
                        int index = reader.DuneRead7BitEncodedInt();
                        string name = reader.ReadString();
                        int assemblyIndex = reader.DuneRead7BitEncodedInt() - 1;
                        DuneAssemblyReference? assembly = null;
                        if (assemblyIndex != -1) assembly = assemblies[assemblyIndex];

                        return new DuneGenericTypeReference(index, name, assembly, source);
                    }

                case TypeCharType: {
                        int defIndex = reader.DuneRead7BitEncodedInt();
                        DuneTypeSignature def = types[defIndex];

                        DuneTypeReference[] genericArgs = new DuneTypeReference[def.GenericParameterCount];

                        for (int i = 0; i < def.GenericParameterCount; i++) {
                            genericArgs[i] = ReadTypeReference();
                        }

                        return new DuneTypeSignatureReference(def, genericArgs);
                    }

                default:
                    throw new InvalidDataException($"Unknown type char '{typeChar}'");
            }
        }

        for (; ; ) {
            byte linePrefix = reader.ReadByte();

            switch (linePrefix) {
                case LinePrefixAssemblyDef: {

                        string name = reader.ReadString();

                        string? cultureName = reader.ReadString();
                        if (cultureName.Length == 0) cultureName = null;

                        string versionString = reader.ReadString();
                        Version? version = null;
                        if (versionString.Length != 0)
                            version = Version.Parse(versionString);

                        assemblies.Add(new(name, version, cultureName));

                        break;
                    }
                case LinePrefixAssemblyAllow: {

                        int assemblyIndex = reader.DuneRead7BitEncodedInt();
                        DuneAssemblyReference allowedAssembly = assemblies[assemblyIndex];

                        rules.GetAssemblyRules(allowedAssembly).Allow = true;

                        break;
                    }
                case LinePrefixTypeDef: {
                        int assemblyIndex = reader.DuneRead7BitEncodedInt();
                        DuneAssemblyReference typeAssembly = assemblies[assemblyIndex];

                        int declaringTypeIndex = reader.DuneRead7BitEncodedInt() - 1;
                        DuneTypeSignature? declaringType = null;
                        if (declaringTypeIndex != -1) declaringType = types[declaringTypeIndex];

                        string? @namespace = reader.ReadString();
                        if (@namespace.Length == 0) @namespace = null;

                        string name = reader.ReadString();

                        int genericCount = reader.DuneRead7BitEncodedInt();
                        string[] genericNames = new string[genericCount];

                        for (int i = 0; i < genericCount; i++)
                            genericNames[i] = reader.ReadString();

                        types.Add(new(typeAssembly, @namespace, name, genericNames, declaringType));

                        break;
                    }

                case LinePrefixTypeAllow: {
                        int typeIndex = reader.DuneRead7BitEncodedInt();
                        DuneTypeSignature type = types[typeIndex];

                        rules.GetAssemblyRules(type.Assembly).GetTypeRules(type).Allow = true;
                        break;
                    }

                case LinePrefixFieldAllowDef: {
                        int declaringTypeIndex = reader.DuneRead7BitEncodedInt();
                        DuneTypeSignature declaringType = types[declaringTypeIndex];

                        string name = reader.ReadString();

                        DuneTypeReference fieldType = ReadTypeReference();

                        DuneFieldSignature field = new(declaringType.Assembly, declaringType, name, fieldType);

                        rules.GetAssemblyRules(field.Assembly).GetTypeRules(field.DeclaringType!).AllowField(field);

                        break;
                    }

                case LinePrefixMethodAllowDef: {
                        int declaringTypeIndex = reader.DuneRead7BitEncodedInt();
                        DuneTypeSignature declaringType = types[declaringTypeIndex];

                        string name = reader.ReadString();

                        DuneTypeReference? returnType = ReadNullableTypeReference();

                        int paramCount = reader.DuneRead7BitEncodedInt();
                        DuneMethodParameter[] parameters = new DuneMethodParameter[paramCount];
                        for (int i = 0; i < paramCount; i++) {
                            string paramName = reader.ReadString();
                            DuneTypeReference paramType = ReadTypeReference();
                            parameters[i] = new(paramName, paramType);
                        }

                        int genericParamCount = reader.DuneRead7BitEncodedInt();
                        string[] genericParams = new string[genericParamCount];
                        for (int i = 0; i < genericParamCount; i++)
                            genericParams[i] = reader.ReadString();

                        DuneMethodSignature method = new(
                            declaringType.Assembly,
                            declaringType,
                            name,
                            genericParams,
                            (ctx) => returnType,
                            (ctx) => parameters
                        );

                        rules.GetAssemblyRules(method.Assembly).GetTypeRules(method.DeclaringType!).AllowMethod(method);

                        break;
                    }
                case LinePrefixEOF:
                    return rules;

                default:
                    throw new InvalidDataException($"Unknown line prefix '{linePrefix}'");
            }
        }
    }
}

