
using System;

namespace Dune.Attributes {

    [AttributeUsage(
        AttributeTargets.Assembly |
        AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Struct | AttributeTargets.Enum | AttributeTargets.Event |
        AttributeTargets.Field | AttributeTargets.Interface | AttributeTargets.Property | AttributeTargets.Delegate | AttributeTargets.Constructor,

        AllowMultiple = true,
        Inherited = false
    )]
    public sealed class DuneDenyAttribute : Attribute {

        public ulong Mask { get; }

        public DuneDenyAttribute(ulong mask = DuneAllowAttribute.DefaultMask) {
            Mask = mask;
        }
    }
}
