using System.Diagnostics;
using Serilog.Core;
using Serilog.Events;

namespace DaftechCrm.Infrastructure.Logging;

/// <summary>
/// Adds process-level context to every log event so entries from multiple
/// instances/containers can be told apart in aggregated log storage.
/// </summary>
public class LogEnricher : ILogEventEnricher
{
    private static readonly int ProcessId = Environment.ProcessId;
    private static readonly string ProcessName = Process.GetCurrentProcess().ProcessName;

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("Application", "DaftechCrm.Api"));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("MachineName", Environment.MachineName));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ProcessId", ProcessId));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ProcessName", ProcessName));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ThreadId", Environment.CurrentManagedThreadId));
    }
}
