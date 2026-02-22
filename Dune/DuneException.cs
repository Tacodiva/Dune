
using System;

public class DuneException : Exception
{
    public DuneException() { }
    public DuneException(string message) : base(message) { }
    public DuneException(string message, Exception inner) : base(message, inner) { }
}