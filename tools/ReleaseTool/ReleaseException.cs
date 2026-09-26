using System;

namespace ReleaseTool;

/// <summary>A problem the release author must fix (bad input, missing changelog, …). Reported as
/// a plain message without a stack trace.</summary>
internal sealed class ReleaseException : Exception
{
    public ReleaseException(string message) : base(message) { }
}
