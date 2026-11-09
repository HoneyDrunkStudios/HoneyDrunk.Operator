using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;

namespace HoneyDrunk.Operator.Data.Tests;

/// <summary>
/// Reports one commit as failed after it succeeded, as a connection dropping before the server's reply would. Paired
/// with <see cref="RetryOnLostReply"/>, which retries that failure as SQL Server's retrying strategy retries a transient one.
/// </summary>
internal sealed class LostCommitReply : DbTransactionInterceptor
{
    public bool Armed { get; set; }

    /// <summary>Gets or sets work run after the commit and before its reply is lost, as another host would run it.</summary>
    public Func<Task>? MeanwhileAsync { get; set; }

    public override async Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        if (this.Armed)
        {
            this.Armed = false;
            if (this.MeanwhileAsync is { } meanwhile)
            {
                await meanwhile().ConfigureAwait(false);
            }

            throw new LostReplyException();
        }
    }

    /// <summary>The lost reply.</summary>
    internal sealed class LostReplyException : Exception
    {
        public LostReplyException()
            : base("the commit's reply was lost")
        {
        }

        public LostReplyException(string message)
            : base(message)
        {
        }

        public LostReplyException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
