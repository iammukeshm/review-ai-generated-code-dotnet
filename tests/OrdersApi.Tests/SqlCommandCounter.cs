using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace OrdersApi.Tests;

// Counts every SQL command EF Core sends to the database.
public sealed class SqlCommandCounter : DbCommandInterceptor
{
    private int _count;

    public int Count => _count;

    public void Reset() => Interlocked.Exchange(ref _count, 0);

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _count);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _count);
        return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
    }
}
