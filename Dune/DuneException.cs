
using System;

namespace Dune;

public class DuneException : Exception {
    public DuneException() { }
    public DuneException(string message, Exception? inner = null) : base(message, inner) { }
}

public class DuneTypeNotFoundException(DuneTypeSignature failedType, Exception? inner = null) :
    DuneException($"Could not find type '{failedType}'.", inner) {

    public DuneTypeSignature FailedType { get; } = failedType;
}