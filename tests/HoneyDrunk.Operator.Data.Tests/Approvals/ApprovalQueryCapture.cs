using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;

namespace HoneyDrunk.Operator.Data.Tests.Approvals;

/// <summary>Records the generated query and actual approval entities materialized by EF.</summary>
internal sealed class ApprovalQueryCapture : DbCommandInterceptor, IMaterializationInterceptor
{
    public string? Command { get; private set; }

    public int Commands { get; private set; }

    public int Materialized { get; private set; }

    public object InitializedInstance(MaterializationInterceptionData materializationData, object entity)
    {
        if (entity is ApprovalEntity)
        {
            this.Materialized++;
        }

        return entity;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        this.Command = command.CommandText;
        this.Commands++;
        return ValueTask.FromResult(result);
    }

    public void Reset()
    {
        this.Command = null;
        this.Commands = 0;
        this.Materialized = 0;
    }
}
