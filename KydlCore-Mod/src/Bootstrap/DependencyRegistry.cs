using System;
using KydlCore.Common;

namespace KydlCore.Bootstrap;

/// <summary>The shared services of KydlCore's features (spec "DependencyRegistry"): the trace sink of pure code (ModLog.Verbose).</summary>
internal sealed class DependencyRegistry
{
    internal Action<string> Trace { get; }
    internal DependencyRegistry() { Trace = line => ModLog.Verbose(line); }
}
