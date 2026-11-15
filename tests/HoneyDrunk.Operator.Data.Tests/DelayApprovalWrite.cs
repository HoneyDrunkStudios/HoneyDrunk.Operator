using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;

namespace HoneyDrunk.Operator.Data.Tests;

/// <summary>Advances the clock once after the update predicate is built, optionally forcing a retry.</summary>
/// <param name="delay">Advances the test clock.</param>
/// <param name="retry">Whether to fail the first command before it executes.</param>
internal sealed class DelayApprovalWrite(Action delay, bool retry) : DbCommandInterceptor
{
    private readonly HashSet<Guid> commands = [];

    public bool Armed { get; set; }

    public int Attempts { get; private set; }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (this.Armed && command.CommandText.StartsWith("UPDATE \"Approvals\"", StringComparison.Ordinal) && this.commands.Add(eventData.CommandId))
        {
            this.Attempts++;
            if (this.Attempts == 1)
            {
                delay();
                if (retry)
                {
                    throw new LostCommitReply.LostReplyException("transient failure before the decision write");
                }
            }
        }

        return ValueTask.FromResult(result);
    }
}
