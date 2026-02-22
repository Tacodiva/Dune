
using System;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Dune;

public sealed class DuneEventSignature : DuneMemberSignature, IEquatable<DuneEventSignature> {

    public static DuneEventSignature FromType<T>(string eventName, DuneReflectionContext? ctx = null)
#if NET
        where T : allows ref struct
#endif
        => FromType(typeof(T), eventName, ctx);

    public static DuneEventSignature FromType(Type type, string eventName, DuneReflectionContext? ctx = null)
        => FromEventInfo(
            type.GetEvent(eventName, DuneReflectionContext.EverythingFlags)
                ?? throw new ArgumentException($"No event '{eventName}' found on type {type}."),
            ctx);

    public static DuneEventSignature FromEventInfo(EventInfo eventInfo, DuneReflectionContext? ctx = null) {
        ctx ??= new();
        if (ctx.TryGetEventSignature(eventInfo, out var cached))
            return cached;

        Type? declaringType = eventInfo.DeclaringType;

        if (declaringType != null) {
            if (declaringType.IsGenericType && !declaringType.IsGenericTypeDefinition) {
                declaringType = declaringType.GetGenericTypeDefinition();
                eventInfo = declaringType.GetEvent(eventInfo.Name, DuneReflectionContext.EverythingFlags)!;
                InternalUtils.Assert(eventInfo != null, "Event not found on generic type definition.");
            }
        }

        return ctx.PutEventSignature(eventInfo, new(
            DuneAssemblyReference.FromAssembly(eventInfo.Module.Assembly, ctx),
            declaringType == null ? null : DuneTypeSignature.FromType(declaringType, ctx),
            eventInfo.Name,
            eventInfo.EventHandlerType == null ? null : DuneTypeReference.FromType(eventInfo.EventHandlerType, ctx),
            eventInfo.AddMethod == null ? null : DuneMethodSignature.FromMethodInfo(eventInfo.AddMethod, ctx),
            eventInfo.RaiseMethod == null ? null : DuneMethodSignature.FromMethodInfo(eventInfo.RaiseMethod, ctx),
            eventInfo.RemoveMethod == null ? null : DuneMethodSignature.FromMethodInfo(eventInfo.RemoveMethod, ctx)
        ));
    }

    public static DuneEventSignature FromEventDefinition(CecilEventDefinition eventDefinition, DuneCecilContext? ctx = null) {
        ctx ??= new();

        return new(
            DuneAssemblyReference.FromAssemblyDefinition(eventDefinition.Module.Assembly),
            eventDefinition.DeclaringType == null ? null : DuneTypeSignature.FromTypeDefinition(eventDefinition.DeclaringType, ctx),
            eventDefinition.Name,
            DuneTypeReference.FromTypeReference(eventDefinition.EventType, ctx),
            eventDefinition.AddMethod == null ? null : DuneMethodSignature.FromMethodDefinition(eventDefinition.AddMethod, ctx),
            eventDefinition.InvokeMethod == null ? null : DuneMethodSignature.FromMethodDefinition(eventDefinition.InvokeMethod, ctx),
            eventDefinition.RemoveMethod == null ? null : DuneMethodSignature.FromMethodDefinition(eventDefinition.RemoveMethod, ctx)
        );
    }

    public static DuneEventSignature FromSymbol(IEventSymbol eventSymbol, DuneRoslynContext? ctx = null) {
        ctx ??= new();
        if (ctx.TryGetEventSignature(eventSymbol, out var cached))
            return cached;

        return ctx.PutEventSignature(eventSymbol, new(
            DuneAssemblyReference.FromSymbol(eventSymbol.ContainingAssembly, ctx),
            eventSymbol.ContainingType == null ? null : DuneTypeSignature.FromSymbol(eventSymbol.ContainingType, ctx),
            eventSymbol.MetadataName,
            eventSymbol.Type == null ? null : DuneTypeReference.FromSymbol(eventSymbol.Type, false, ctx),
            eventSymbol.AddMethod == null ? null : DuneMethodSignature.FromSymbol(eventSymbol.AddMethod, ctx),
            eventSymbol.RaiseMethod == null ? null : DuneMethodSignature.FromSymbol(eventSymbol.RaiseMethod, ctx),
            eventSymbol.RemoveMethod == null ? null : DuneMethodSignature.FromSymbol(eventSymbol.RemoveMethod, ctx)
        ));
    }

    public DuneTypeReference? Type { get; }

    public DuneMethodSignature? AddMethod { get; }
    public DuneMethodSignature? RaiseMethod { get; }
    public DuneMethodSignature? RemoveMethod { get; }

    private DuneEventSignature(DuneAssemblyReference assembly, DuneTypeSignature? declaringType, string name, DuneTypeReference? type,
        DuneMethodSignature? addMethod, DuneMethodSignature? raiseMethod, DuneMethodSignature? removeMethod) : base(name, declaringType, assembly) {
        Type = type;
        AddMethod = addMethod;
        RaiseMethod = raiseMethod;
        RemoveMethod = removeMethod;
    }

    public DuneEventReference CreateReference(DuneTypeSignatureReference declaringTypeReference) {
        if (!declaringTypeReference.Signature.Equals(DeclaringType))
            throw new ArgumentException($"{nameof(declaringTypeReference)} must reference the same type definition as the event's declaring type.");

        return new(this, declaringTypeReference);
    }

    public override string ToString()
        => ToString(DuneTypeFormat.Default, DuneTypeFormat.DefaultMinimal);

    public string ToString(in DuneTypeFormat eventFormat)
        => ToString(eventFormat, DuneTypeFormat.DefaultMinimal);

    public string ToString(in DuneTypeFormat eventFormat, DuneTypeFormat? typeFormat) {
        StringBuilder sb = new();

        if (typeFormat != null) {
            if (Type == null) sb.Append('?');
            else typeFormat.Value.AppendType(Type, sb);
            sb.Append(' ');
        }

        eventFormat.AppendMemberName(this, sb);
        eventFormat.AppendAssemblySuffix(this, sb);

        return sb.ToString();
    }

    public override bool Equals(object? obj) => Equals(obj as DuneEventSignature);
    public override bool Equals(IDuneSymbol? other) => Equals(other as DuneEventSignature);
    public bool Equals(DuneEventSignature? other) {
        if (ReferenceEquals(this, other)) return true;
        if (other is null) return false;

        if (Name != other.Name) return false;
        if (Assembly != other.Assembly) return false;
        if (DeclaringType != other.DeclaringType) return false;
        if (Type != other.Type) return false;
        return true;
    }

    public override int GetHashCode()
        => InternalUtils.HashCodeCombine(DeclaringType, Name, Type);

    public static bool operator ==(DuneEventSignature? a, DuneEventSignature? b) => a?.Equals(b) ?? b is null;
    public static bool operator !=(DuneEventSignature? a, DuneEventSignature? b) => !(a == b);
}