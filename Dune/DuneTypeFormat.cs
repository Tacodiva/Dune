using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dune;

public readonly record struct DuneTypeFormat(
    bool IncludeAssembly = false,
    bool IncludeNamespace = true,
    bool IncludeDeclaringTypes = true,
    bool IncludeGenericParameters = true
) {
    public static readonly DuneTypeFormat All = new(true, true, true, true);
    public static readonly DuneTypeFormat Default = new(false, true, true, true);
    public static readonly DuneTypeFormat DefaultMinimal = new(false, false, true, true);
    public static readonly DuneTypeFormat NameOnly = new(false, false, false, false);
    public static readonly DuneTypeFormat FullNameOnly = new(false, true, true, false);

    internal static readonly DuneTypeFormat _defaultGenericArgument = DefaultMinimal;

    public DuneTypeFormat WithIncludeAssembly(bool includeAssembly = true)
        => new(includeAssembly, IncludeNamespace, IncludeDeclaringTypes, IncludeGenericParameters);

    public DuneTypeFormat WithIncludeNamespace(bool includeNamespace = true)
        => new(IncludeAssembly, includeNamespace, IncludeDeclaringTypes, IncludeGenericParameters);

    public DuneTypeFormat WithIncludeDeclaringTypes(bool includeDeclaringTypes = true)
        => new(IncludeAssembly, IncludeNamespace, includeDeclaringTypes, IncludeGenericParameters);

    public DuneTypeFormat WithIncludeGenericParameters(bool includeGenericParameters = true)
        => new(IncludeAssembly, IncludeNamespace, IncludeDeclaringTypes, includeGenericParameters);

    internal StringBuilder AppendMemberName(IDuneMember member, StringBuilder? sb = null) {
        sb ??= new();

        IDuneType? declaringType = member.DeclaringType;

        if (declaringType != null && (IncludeDeclaringTypes || IncludeNamespace)) {
            AppendTypeName(declaringType, sb);
            sb.Append(':');
        }

        member.FormatAndAppendName(this, sb);

        return sb;
    }

    private StringBuilder AppendTypeName(IDuneType? type, StringBuilder? sb = null) {
        sb ??= new();
        type ??= DuneTypeSignature.Void;

        type.FormatAndAppendName(this, sb);
        AppendGenericParameters(type, sb);
        return sb;
    }

    internal StringBuilder AppendType(IDuneType? type, StringBuilder? sb = null) {
        sb ??= new();
        type ??= DuneTypeSignature.Void;

        AppendTypeName(type, sb);
        AppendAssemblySuffix(type.Assembly, sb);

        return sb;
    }

    internal StringBuilder AppendTypes(IEnumerable<IDuneType?> types, StringBuilder sb, string separator = ", ") {
        DuneTypeFormat @this = this;
        return sb.AppendEnumerable(types, (type, sb) => @this.AppendType(type, sb), separator);
    }

    internal StringBuilder AppendGenericParameters(IDuneGenericSymbol symbol, StringBuilder sb) {
        if (IncludeGenericParameters && symbol.HasGenericParameters) {
            sb.Append('<');
            symbol.FormatAndAppendGenericParameters(_defaultGenericArgument, sb);
            sb.Append('>');
        }
        return sb;
    }

    internal StringBuilder AppendAssemblySuffix(IDuneSymbol symbol, StringBuilder sb)
        => AppendAssemblySuffix(symbol.Assembly, sb);

    internal StringBuilder AppendAssemblySuffix(DuneAssemblyReference? assembly, StringBuilder sb) {
        if (IncludeAssembly && assembly != null) {
            sb.Append(" [");
            sb.Append(assembly.ToString());
            sb.Append(']');
        }
        return sb;
    }
}
