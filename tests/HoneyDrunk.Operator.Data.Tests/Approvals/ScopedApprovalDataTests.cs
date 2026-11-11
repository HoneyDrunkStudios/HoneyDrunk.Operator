using HoneyDrunk.Operator.Abstractions;
using HoneyDrunk.Operator.Abstractions.Approvals;
using HoneyDrunk.Operator.State;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HoneyDrunk.Operator.Data.Tests.Approvals;

/// <summary>The real relational provider filters and limits in SQL, before materializing approval records.</summary>
public sealed class ScopedApprovalDataTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 11, 10, 0, 0, TimeSpan.Zero);

    /// <summary>A SQL page includes older matching subjects and materializes only the requested number of records.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Sql_filters_before_limit_and_materializes_only_the_page()
    {
        var capture = new ApprovalQueryCapture();
        using var database = new SqliteDatabase();
        await using (var context = await database.Factory.CreateDbContextAsync())
        {
            context.Approvals.AddRange(Row("a", "agent:a"), Row("b", "agent:b"), Row("c", "agent:a"));
            var expired = Row("expired", "agent:a");
            expired.ExpiryTicks = Now.UtcTicks;
            expired.RequestedAtTicks = Now.AddMinutes(2).UtcTicks;
            context.Approvals.Add(expired);
            var decided = Row("decided", "agent:a");
            decided.Outcome = (int)ApprovalOutcome.Denied;
            decided.RequestedAtTicks = Now.AddMinutes(2).UtcTicks;
            context.Approvals.Add(decided);
            for (var number = 0; number < 200; number++)
            {
                var row = Row($"noise-{number}", "user:a");
                row.RequestedAtTicks = Now.AddMinutes(1).UtcTicks;
                context.Approvals.Add(row);
            }

            await context.SaveChangesAsync();
        }

        var services = new ServiceCollection();
        services.AddOperatorDataStore(options =>
        {
            options.UseSqlite(database.ConnectionString);

            // Data configures both its factory and scoped context; record each materialization only once.
            if (options.Options.FindExtension<CoreOptionsExtension>()?.Interceptors?.Contains(capture) != true)
            {
                options.AddInterceptors(capture);
            }
        });
        using var provider = services.BuildServiceProvider();
        var scoped = Assert.IsType<ISubjectScopedOperatorStore>(provider.GetRequiredService<IOperatorStore>(), exactMatch: false);
        capture.Reset();

        var result = await scoped.ListPendingAsync(Now, new PendingApprovalQuery(["agent:a", "agent:b"], 2), CancellationToken.None);

        Assert.Equal(["a", "b"], result.Select(item => item.Request.ApprovalId));
        Assert.Equal(2, capture.Materialized);
        Assert.Equal(1, capture.Commands);
        Assert.Contains("Subject", capture.Command, StringComparison.Ordinal);
        Assert.Contains("WHERE", capture.Command, StringComparison.Ordinal);
        Assert.Contains("ORDER BY", capture.Command, StringComparison.Ordinal);
        Assert.Contains("LIMIT", capture.Command, StringComparison.Ordinal);

        capture.Reset();
        Assert.Empty(await scoped.ListPendingAsync(Now, new PendingApprovalQuery([]), CancellationToken.None));
        Assert.Null(capture.Command);
        Assert.Equal(0, capture.Commands);
        Assert.Equal(0, capture.Materialized);
    }

    private static ApprovalEntity Row(string id, string subject) => new()
    {
        ApprovalId = id,
        Subject = subject,
        Action = "tool.publish",
        RequestedScope = "tool:any@1",
        RequesterCorrelationId = "correlation-1",
        Outcome = (int)ApprovalOutcome.Pending,
        RequestedAtTicks = Now.UtcTicks,
        ExpiryTicks = Now.AddHours(1).UtcTicks,
    };
}
