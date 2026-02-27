
using System;

namespace Dune.Attributes {

    [AttributeUsage(
        AttributeTargets.Assembly |
        AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Struct | AttributeTargets.Enum | AttributeTargets.Event |
        AttributeTargets.Field | AttributeTargets.Interface | AttributeTargets.Property | AttributeTargets.Delegate | AttributeTargets.Constructor,

        AllowMultiple = true,
        Inherited = false
    )]
    public sealed class DuneAllowAttribute : Attribute {

        public const bool DefaultIsRecursive = false;
        public const ulong DefaultMask = 0;

        public bool IsRecursive { get; }
        public ulong Mask { get; }

        public DuneAllowAttribute(ulong mask) {
            IsRecursive = DefaultIsRecursive;
            Mask = mask;
        }

        public DuneAllowAttribute(bool isRecursive = DefaultIsRecursive, ulong mask = DefaultMask) {
            IsRecursive = isRecursive;
            Mask = mask;
        }
    }
}
