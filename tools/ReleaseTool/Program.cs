using System;
using ReleaseTool;

try
{
    ReleaseBuilder.Run(ReleaseOptions.Parse(args));
    return 0;
}
catch (ReleaseException problem)
{
    // A problem the release author must fix: print it plainly (GitHub shows ::error lines prominently).
    Console.Error.WriteLine($"::error::{problem.Message}");
    return 1;
}
