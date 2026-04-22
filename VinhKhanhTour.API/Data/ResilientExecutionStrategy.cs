using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL;
using System.Security.Authentication;

namespace VinhKhanhTour.API.Data;

// Retries on transient Npgsql errors AND on the known Npgsql pool bug where a disposed
// ManualResetEventSlim bubbles up from NpgsqlConnector.ResetCancellation() when a prior
// query was cancelled mid-flight (happens with Supabase pooler under load).
//
// When the disposed-event exception fires, the broken connector is still in the pool and
// will be handed to the next caller, causing the same error on every retry. We clear the
// pool once so subsequent attempts start from fresh connections.
public sealed class ResilientExecutionStrategy : NpgsqlRetryingExecutionStrategy
{
    private static readonly object PoolClearGate = new();
    private static DateTime _lastPoolClearUtc = DateTime.MinValue;
    private static readonly TimeSpan PoolClearCooldown = TimeSpan.FromSeconds(5);

    public ResilientExecutionStrategy(ExecutionStrategyDependencies dependencies)
        : this(dependencies, maxRetryCount: 6, maxRetryDelay: TimeSpan.FromSeconds(5))
    {
    }

    public ResilientExecutionStrategy(
        ExecutionStrategyDependencies dependencies,
        int maxRetryCount,
        TimeSpan maxRetryDelay)
        : base(dependencies, maxRetryCount, maxRetryDelay, errorCodesToAdd: null)
    {
    }

    protected override bool ShouldRetryOn(Exception? exception)
    {
        if (exception == null)
            return false;

        if (IsDisposedWaitHandle(exception))
        {
            ClearPoolIfCooledDown();
            return true;
        }

        if (IsSslHandshakeFailure(exception))
        {
            ClearPoolIfCooledDown();
            return true;
        }

        if (base.ShouldRetryOn(exception))
            return true;

        return false;
    }

    private static bool IsDisposedWaitHandle(Exception exception)
    {
        for (var current = exception; current != null; current = current.InnerException)
        {
            if (current is ObjectDisposedException od &&
                string.Equals(od.ObjectName, "System.Threading.ManualResetEventSlim", StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static bool IsSslHandshakeFailure(Exception exception)
    {
        for (var current = exception; current != null; current = current.InnerException)
        {
            if (current is NpgsqlException npgsql &&
                npgsql.Message.Contains("SSL handshake", StringComparison.OrdinalIgnoreCase))
                return true;

            if (current is AuthenticationException)
                return true;
        }

        return false;
    }

    private static void ClearPoolIfCooledDown()
    {
        var now = DateTime.UtcNow;

        lock (PoolClearGate)
        {
            if (now - _lastPoolClearUtc < PoolClearCooldown)
                return;

            _lastPoolClearUtc = now;
        }

        try
        {
            NpgsqlConnection.ClearAllPools();
        }
        catch
        {
            // Swallow: retry logic will surface the original exception if this does not help.
        }
    }
}
