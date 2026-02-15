using Serilog;
using Serilog.Formatting.Json;

namespace Gearify.SharedKernel.Logging;

/// <summary>
/// Centralized Serilog configuration with CorrelationId enrichment.
/// All services use this to ensure consistent log format and context propagation.
/// </summary>
public static class SerilogBootstrap
{
    private static readonly string DefaultSeqUrl =
        Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true"
            ? "http://seq:80"
            : "http://localhost:5341";

    private const string OutputTemplate =
        "[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}";

    /// <summary>
    /// Creates a LoggerConfiguration with plain-text console output + Seq.
    /// CorrelationId appears in brackets: [abc-123].
    /// </summary>
    public static LoggerConfiguration CreateConsole(string? seqUrl = null)
    {
        return new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.Console(outputTemplate: OutputTemplate)
            .WriteTo.Seq(seqUrl ?? Environment.GetEnvironmentVariable("SEQ_URL") ?? DefaultSeqUrl);
    }

    /// <summary>
    /// Creates a LoggerConfiguration with JSON console output + Seq.
    /// CorrelationId appears as a JSON property automatically.
    /// </summary>
    public static LoggerConfiguration CreateJsonConsole(string? seqUrl = null)
    {
        return new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.Console(new JsonFormatter())
            .WriteTo.Seq(seqUrl ?? Environment.GetEnvironmentVariable("SEQ_URL") ?? DefaultSeqUrl);
    }
}
