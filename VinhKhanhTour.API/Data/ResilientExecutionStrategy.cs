using Microsoft.EntityFrameworkCore.Storage;
using Npgsql.EntityFrameworkCore.PostgreSQL;

namespace VinhKhanhTour.API.Data;

// Retries on transient Npgsql errors AND on the known Npgsql pool bug where a disposed
// ManualResetEventSlim bubbles up from NpgsqlConnector.ResetCancellation() when a prior
// query was cancelled mid-flight (happens with Supabase pooler under load).
public sealed class ResilientExecutionStrategy : NpgsqlRetryingExecutionStrategy
{
    public ResilientExecutionStrategy(ExecutionStrategyDependencies dependencies)
        : base(dependencies, maxRetryCount: 4, maxRetryDelay: TimeSpan.FromSeconds(3), errorCodesToAdd: null)
    {
    }

    protected override bool ShouldRetryOn(Exception exception)
    {
        if (base.ShouldRetryOn(exception))
            return true;

        for (var current = exception; current != null; current = current.InnerException)
        {
            if (current is ObjectDisposedException od &&
                string.Equals(od.ObjectName, "System.Threading.ManualResetEventSlim", StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
