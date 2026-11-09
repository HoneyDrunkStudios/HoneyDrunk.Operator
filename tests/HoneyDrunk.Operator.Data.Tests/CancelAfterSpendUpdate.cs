using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;

namespace HoneyDrunk.Operator.Data.Tests;

/// <summary>Cancels a token right after a spend update has changed a row, as a caller cancelling at that moment would.</summary>
internal sealed class CancelAfterSpendUpdate : DbCommandInterceptor
{
    public CancellationTokenSource Cancellation { get; } = new();

    public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        if (result > 0 && command.CommandText.Contains("UPDATE", StringComparison.OrdinalIgnoreCase) && command.CommandText.Contains("Spend", StringComparison.Ordinal))
        {
            this.Cancellation.Cancel();
        }

        return ValueTask.FromResult(result);
    }
}
