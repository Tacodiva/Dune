
using System;

namespace Dune;

public class DuneAssemblyNotFoundExceptionException(DuneAssemblyReference failedQuery) :
    Exception($"Could not find assembly matching {failedQuery}.") {

    public DuneAssemblyReference FailedQuery { get; } = failedQuery;
}