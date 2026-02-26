
using System;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Mono.Cecil;

namespace Dune;

public sealed class DuneEventReference : DuneMemberReference<DuneEventSignature>, IEquatable<DuneEventReference> {

    public static DuneEventReference FromType<T>(string eventName, DuneReflectionContext? ctx = null)
#if NET
        where T : allows ref struct
#endif
        => FromType(typeof(T), eventName, ctx);

    public static DuneEventReference FromType(Type type, string eventName, DuneReflectionContext? ctx = null) {
        InternalUtils.ThrowIfArgumentNull(type);
        InternalUtils.ThrowIfArgumentNullOrWhitespace(eventName);

        return FromEventInfo(
           type.GetEvent(eventName, DuneReflectionContext.EverythingFlags)
               ?? throw new ArgumentException($"No event '{eventName}' found on type {type}."),
           ctx);
    }

    public static DuneEventReference FromEventInfo(EventInfo eventInfo, DuneReflectionContext? ctx = null) {
        InternalUtils.ThrowIfArgumentNull(eventInfo);
        InternalUtils.Assert(eventInfo.DeclaringType != null);

        ctx ??= new();
        if (ctx.TryGetEventReference(eventInfo, out var cached))
            return cached;

        return ctx.PutEventReference(eventInfo, new(
            DuneEventSignature.FromEventInfo(eventInfo, ctx),
            DuneTypeSignatureReference.FromType(eventInfo.DeclaringType, ctx)
        ));
    }

    public static DuneEventReference FromCecilReference(CecilEventReference eventReference, DuneCecilContext? ctx = null) {
        ctx ??= new();

        return new(
            DuneEventSignature.FromCecilDefinition(eventReference.Resolve(), ctx),
            DuneTypeSignatureReference.FromCecilReference(eventReference.DeclaringType, ctx)
        );
    }

    public static DuneEventReference FromSymbol(IEventSymbol eventSymbol, DuneRoslynContext? ctx = null) {
        InternalUtils.ThrowIfArgumentNull(eventSymbol);

        ctx ??= new();
        if (ctx.TryGetEventReference(eventSymbol, out var cached))
            return cached;

        return ctx.PutEventReference(eventSymbol, new(
            DuneEventSignature.FromSymbol(eventSymbol, ctx),
            DuneTypeSignatureReference.FromSymbol(eventSymbol.ContainingType, ctx)
        ));
    }

    public DuneTypeReference? Type { get; }

    public DuneMethodReference? AddMethod { get; }
    public DuneMethodReference? RaiseMethod { get; }
    public DuneMethodReference? RemoveMethod { get; }

    internal DuneEventReference(DuneEventSignature signature, DuneTypeSignatureReference declaringType) : base(signature, declaringType) {
        Type = signature.Type?.Resolve(declaringType);
        AddMethod = signature.AddMethod?.CreateReference(declaringType);
        RaiseMethod = signature.RaiseMethod?.CreateReference(declaringType);
        RemoveMethod = signature.RemoveMethod?.CreateReference(declaringType);
    }

    public override string ToString()
        => ToString(DuneTypeFormat.Default, DuneTypeFormat.DefaultMinimal);

    public string ToString(in DuneTypeFormat fieldFormat)
        => ToString(fieldFormat, DuneTypeFormat.DefaultMinimal);

    public string ToString(in DuneTypeFormat eventFormat, DuneTypeFormat? typeFormat) {
        StringBuilder sb = new();

        if (typeFormat != null) {
            if (Type != null) typeFormat.Value.AppendType(
                    eventFormat.IncludeGenericParameters ? Signature.Type : Type, sb
                );
            else sb.Append('?');

            sb.Append(' ');
        }

        eventFormat.AppendMemberName(this, sb);
        eventFormat.AppendAssemblySuffix(this, sb);

        return sb.ToString();
    }

    public override bool Equals(object? obj) => Equals(obj as DuneEventReference);
    public override bool Equals(IDuneSymbol? other) => Equals(other as DuneEventReference);
    public bool Equals(DuneEventReference? other) {
        if (ReferenceEquals(this, other)) return true;
        if (other is null) return false;

        if (Name != other.Name) return false;
        if (DeclaringType != other.DeclaringType) return false;
        if (Type != other.Type) return false;

        return true;
    }

    public override int GetHashCode()
        => InternalUtils.HashCodeCombine(DeclaringType, Name, Type);

    public static bool operator ==(DuneEventReference? a, DuneEventReference? b) => a?.Equals(b) ?? b is null;
    public static bool operator !=(DuneEventReference? a, DuneEventReference? b) => !(a == b);

}
