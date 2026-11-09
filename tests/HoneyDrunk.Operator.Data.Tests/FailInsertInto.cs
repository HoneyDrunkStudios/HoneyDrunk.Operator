using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;

namespace HoneyDrunk.Operator.Data.Tests;

/// <summary>Fails any command that inserts into one table, as a database failing partway through a write would.</summary>
/// <param name="table">The table name, for example <c>Pauses</c>.</param>
internal sealed class FailInsertInto(string table) : DbCommandInterceptor
{
    public bool Armed { get; set; }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        this.Check(command);
        return ValueTask.FromResult(result);
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        this.Check(command);
        return ValueTask.FromResult(result);
    }

    private void Check(DbCommand command)
    {
        if (this.Armed && command.CommandText.Contains("INSERT INTO", StringComparison.OrdinalIgnoreCase) && command.CommandText.Contains($"\"{table}\"", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"insert into {table} failed");
        }
    }
}
