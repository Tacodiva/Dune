using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;


namespace Dune;

public static class CecilExt {

    private static IDuneMemberSignature? FromCecil(IMemberDefinition? member, DuneCecilContext? ctx) {
        return member switch {
            CecilTypeDefinition typeDef => DuneTypeSignature.FromTypeDefinition(typeDef),
            CecilMethodDefinition methodDef => DuneMethodSignature.FromMethodDefinition(methodDef),
            CecilFieldDefinition fieldDef => DuneFieldSignature.FromFieldDefinition(fieldDef),
            _ => null,
        };
    }

    public static DuneMethodSignature GetDuneMemberMethod(this CecilAssemblyDefinition assemblyDefinition, string path, DuneCecilContext? ctx = null)
        => TryGetDuneMemberMethod(assemblyDefinition, path, ctx) ?? throw new KeyNotFoundException($"Method '{path}' does not exist.");

    public static DuneTypeSignature GetDuneMemberType(this CecilAssemblyDefinition assemblyDefinition, string path, DuneCecilContext? ctx = null)
        => TryGetDuneMemberType(assemblyDefinition, path, ctx) ?? throw new KeyNotFoundException($"Type '{path}' does not exist.");

    public static DuneFieldSignature GetDuneMemberField(this CecilAssemblyDefinition assemblyDefinition, string path, DuneCecilContext? ctx = null)
        => TryGetDuneMemberField(assemblyDefinition, path, ctx) ?? throw new KeyNotFoundException($"Field '{path}' does not exist.");

    public static DuneMethodSignature? TryGetDuneMemberMethod(this CecilAssemblyDefinition assemblyDefinition, string path, DuneCecilContext? ctx = null)
        => TryGetDuneMember(assemblyDefinition, path, ctx) as DuneMethodSignature;

    public static DuneTypeSignature? TryGetDuneMemberType(this CecilAssemblyDefinition assemblyDefinition, string path, DuneCecilContext? ctx = null)
        => TryGetDuneMember(assemblyDefinition, path, ctx) as DuneTypeSignature;

    public static DuneFieldSignature? TryGetDuneMemberField(this CecilAssemblyDefinition assemblyDefinition, string path, DuneCecilContext? ctx = null)
        => TryGetDuneMember(assemblyDefinition, path, ctx) as DuneFieldSignature;

    public static IDuneMemberSignature? TryGetDuneMember(this CecilAssemblyDefinition assemblyDefinition, string path, DuneCecilContext? ctx = null)
        => FromCecil(TryGetMember(assemblyDefinition, path.Split('.')), ctx);

    public static CecilMethodDefinition? TryGetMemberMethod(this CecilAssemblyDefinition assemblyDefinition, string path)
        => TryGetMember(assemblyDefinition, path) as CecilMethodDefinition;

    public static CecilTypeDefinition? TryGetMemberType(this CecilAssemblyDefinition assemblyDefinition, string path)
        => TryGetMember(assemblyDefinition, path) as CecilTypeDefinition;

    public static CecilPropertyDefinition? TryGetMemberProperty(this CecilAssemblyDefinition assemblyDefinition, string path)
        => TryGetMember(assemblyDefinition, path) as CecilPropertyDefinition;

    public static CecilEventDefinition? TryGetMemberEvent(this CecilAssemblyDefinition assemblyDefinition, string path)
        => TryGetMember(assemblyDefinition, path) as CecilEventDefinition;

    public static CecilFieldDefinition? TryGetMemberField(this CecilAssemblyDefinition assemblyDefinition, string path)
        => TryGetMember(assemblyDefinition, path) as CecilFieldDefinition;

    public static IMemberDefinition? TryGetMember(this CecilAssemblyDefinition assemblyDefinition, string path)
        => TryGetMember(assemblyDefinition, path.Split('.'));

    public static IMemberDefinition? TryGetMember(this CecilAssemblyDefinition assemblyDefinition, params string[] pathParts) {
        foreach (CecilModuleDefinition moduleDefinition in assemblyDefinition.Modules) {

            foreach (CecilTypeDefinition typeDefinition in moduleDefinition.Types) {

                string[] nameParts = typeDefinition.FullName.Split('.');

                if (nameParts.Length > pathParts.Length)
                    continue;

                int i = 0;

                for (; i < nameParts.Length; i++) {
                    if (nameParts[i] != pathParts[i])
                        break;
                }

                if (i != nameParts.Length)
                    continue;

                return TryGetMember(typeDefinition, [.. pathParts.Skip(i)]);
            }
        }

        return null;
    }

    public static DuneMethodSignature GetDuneMemberMethod(this CecilTypeDefinition typeDefinition, string path, DuneCecilContext? ctx = null)
        => TryGetDuneMemberMethod(typeDefinition, path, ctx) ?? throw new KeyNotFoundException($"Method '{path}' does not exist.");

    public static DuneTypeSignature GetDuneMemberType(this CecilTypeDefinition typeDefinition, string path, DuneCecilContext? ctx = null)
        => TryGetDuneMemberType(typeDefinition, path, ctx) ?? throw new KeyNotFoundException($"Type '{path}' does not exist.");

    public static DuneFieldSignature GetDuneMemberField(this CecilTypeDefinition typeDefinition, string path, DuneCecilContext? ctx = null)
        => TryGetDuneMemberField(typeDefinition, path, ctx) ?? throw new KeyNotFoundException($"Field '{path}' does not exist.");

    public static DuneMethodSignature? TryGetDuneMemberMethod(this CecilTypeDefinition typeDefinition, string path, DuneCecilContext? ctx = null)
        => TryGetDuneMember(typeDefinition, path, ctx) as DuneMethodSignature;

    public static DuneTypeSignature? TryGetDuneMemberType(this CecilTypeDefinition typeDefinition, string path, DuneCecilContext? ctx = null)
        => TryGetDuneMember(typeDefinition, path, ctx) as DuneTypeSignature;

    public static DuneFieldSignature? TryGetDuneMemberField(this CecilTypeDefinition typeDefinition, string path, DuneCecilContext? ctx = null)
        => TryGetDuneMember(typeDefinition, path, ctx) as DuneFieldSignature;

    public static IDuneMemberSignature? TryGetDuneMember(this CecilTypeDefinition typeDefinition, string path, DuneCecilContext? ctx = null)
        => FromCecil(TryGetMember(typeDefinition, path.Split('.')), ctx);

    public static CecilMethodDefinition? TryGetMemberMethod(this CecilTypeDefinition typeDefinition, string path)
        => TryGetMember(typeDefinition, path) as CecilMethodDefinition;

    public static CecilTypeDefinition? TryGetMemberType(this CecilTypeDefinition typeDefinition, string path)
        => TryGetMember(typeDefinition, path) as CecilTypeDefinition;

    public static CecilPropertyDefinition? TryGetMemberProperty(this CecilTypeDefinition typeDefinition, string path)
        => TryGetMember(typeDefinition, path) as CecilPropertyDefinition;

    public static CecilEventDefinition? TryGetMemberEvent(this CecilTypeDefinition typeDefinition, string path)
        => TryGetMember(typeDefinition, path) as CecilEventDefinition;

    public static CecilFieldDefinition? TryGetMemberField(this CecilTypeDefinition typeDefinition, string path)
        => TryGetMember(typeDefinition, path) as CecilFieldDefinition;

    public static IMemberDefinition? TryGetMember(this CecilTypeDefinition typeDefinition, string path)
        => TryGetMember(typeDefinition, path.Split('.'));

    public static IMemberDefinition? TryGetMember(this CecilTypeDefinition typeDefinition, params string[] pathParts) {
        if (pathParts.Length == 0)
            return typeDefinition;

        if (pathParts.Length == 1) {

            foreach (IMemberDefinition def in (IEnumerable<IMemberDefinition>)[
                ..typeDefinition.Methods,
                ..typeDefinition.Fields,
                ..typeDefinition.Properties,
                ..typeDefinition.Events,
            ]) {
                if (def.Name == pathParts[0]) return def;
            }
        }

        foreach (CecilTypeDefinition def in typeDefinition.NestedTypes) {
            if (def.Name == pathParts[0]) {
                return TryGetMember(def, [.. pathParts.Skip(1)]);
            }
        }

        return null;
    }
}