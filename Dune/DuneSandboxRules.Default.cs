
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Dune;

partial class DuneSandboxRules {
    private static void InitDefaultRules(DuneSandboxRules rules) {
#if NET
        DuneSandboxReflectionRuleBuilder AllowType(Type type) {
            DuneSandboxReflectionRuleBuilder builder = rules.AllowType(type);

            if (type.GetMethod("ToString", []) != null)
                builder.AllowMethod(nameof(ToString), []);

            if (type.GetConstructor([]) != null)
                builder.AllowConstructor([]);

            return builder;
        }

        DuneSandboxReflectionRuleBuilder Allow<T>() => AllowType(typeof(T));
        void AllowAll<T>() => rules.AllowAll<T>();


        AllowType(typeof(void));

        Allow<string>();
        Allow<char>();
        Allow<bool>();
        Allow<sbyte>();
        Allow<byte>();
        Allow<int>();
        Allow<uint>();
        Allow<long>();
        Allow<ulong>();
        Allow<float>();
        Allow<double>();

        Allow<object>()
            .AllowConstructors()
            .AllowMethod(nameof(object.ReferenceEquals))
            .AllowMethod(nameof(object.GetHashCode))
            .AllowMethods(nameof(object.Equals));
        
        AllowAll<ValueType>();

        Allow<Exception>()
            .AllowProperty(nameof(Exception.Data))
            .AllowProperty(nameof(Exception.HelpLink))
            .AllowProperty(nameof(Exception.HResult))
            .AllowProperty(nameof(Exception.InnerException))
            .AllowProperty(nameof(Exception.Message))
            .AllowProperty(nameof(Exception.Source))

            .AllowMethod(nameof(Exception.GetBaseException));

        AllowAll<IDisposable>();

        AllowAll<IEnumerable>();
        AllowAll<IEnumerator>();
        AllowAll<ICollection>();
        AllowAll<IList>();
        AllowAll<IDictionary>();

        AllowAll<IEnumerable<object>>();
        AllowAll<IEnumerator<object>>();
        AllowAll<ICollection<object>>();
        AllowAll<IList<object>>();
        AllowAll<IReadOnlyCollection<object>>();
        AllowAll<IReadOnlyList<object>>();
        AllowAll<IDictionary<object, object>>();
        AllowAll<IReadOnlyDictionary<object, object>>();

        Allow<List<object>>()
            .AllowConstructors()

            .AllowProperty(nameof(List<object>.Capacity))
            .AllowProperty(nameof(List<object>.Count))

            .AllowMethod(nameof(List<object>.Add))
            .AllowMethod(nameof(List<object>.AddRange))

            ;

        AllowAll<List<object>.Enumerator>();

        AllowAll<CompilationRelaxationsAttribute>();
        AllowAll<CompilationRelaxations>();
        AllowAll<DebuggableAttribute>();
        AllowAll<DebuggableAttribute.DebuggingModes>();
        AllowAll<RefSafetyRulesAttribute>();
        AllowAll<DebuggerHiddenAttribute>();
        AllowAll<NullableAttribute>();
        AllowAll<NullableContextAttribute>();
        AllowAll<RuntimeCompatibilityAttribute>();

        Allow<Task>();

        Allow<Task<object>>();

        AllowAll<AsyncTaskMethodBuilder>();
        AllowAll<AsyncTaskMethodBuilder<object>>();
        AllowAll<IAsyncStateMachine>();

#endif
    }
}

