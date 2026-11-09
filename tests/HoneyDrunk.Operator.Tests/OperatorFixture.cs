using HoneyDrunk.Kernel.Abstractions.Telemetry;
using HoneyDrunk.Operator.Abstractions;
using HoneyDrunk.Operator.Approval;
using HoneyDrunk.Operator.Audit;
using HoneyDrunk.Operator.Breaker;
using HoneyDrunk.Operator.Cost;
using HoneyDrunk.Operator.Events;
using HoneyDrunk.Operator.Pauses;
using HoneyDrunk.Operator.State;
using HoneyDrunk.Operator.Telemetry;
using HoneyDrunk.Vault.Abstractions;
using NSubstitute;

namespace HoneyDrunk.Operator.Tests;

/// <summary>The Operator runtime over one store, clock and recording audit log.</summary>
internal sealed class OperatorFixture
{
    public OperatorFixture(IOperatorStore? store = null, IReadOnlyDictionary<string, string>? config = null, IConfigProvider? configProvider = null)
    {
        this.Store = store ?? new InMemoryOperatorStore();
        var telemetry = new OperatorTelemetry(Substitute.For<ITelemetryActivityFactory>());
        var audit = new OperatorAuditWriter([this.AuditLog]);
        var options = Microsoft.Extensions.Options.Options.Create(new OperatorOptions());
        configProvider ??= TestDoubles.ConfigProvider(config);
        this.Gate = new DefaultApprovalGate(new ApprovalEventEmitter(this.Sink, telemetry), telemetry, audit, this.Clock, this.Store);
        this.Desk = new DefaultApprovalDesk(this.Store, audit, telemetry, this.Clock);
        this.Pauses = new DefaultAgentPauses(this.Store, audit, telemetry, this.Clock);
        this.Breaker = new DefaultCircuitBreaker(configProvider, options, telemetry, audit, this.Clock, this.Pauses);
        this.CostGuard = new DefaultCostGuard(configProvider, options, telemetry, audit, this.Store, this.Clock);
    }

    public MutableTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 11, 8, 12, 0, 0, TimeSpan.Zero));

    public RecordingAuditLog AuditLog { get; } = new();

    public IApprovalEventSink Sink { get; } = Substitute.For<IApprovalEventSink>();

    public IOperatorStore Store { get; }

    public DefaultApprovalGate Gate { get; }

    public DefaultApprovalDesk Desk { get; }

    public DefaultAgentPauses Pauses { get; }

    public DefaultCircuitBreaker Breaker { get; }

    public DefaultCostGuard CostGuard { get; }

    public ApprovalRequest Request(string id = "a1", IReadOnlyDictionary<string, string>? context = null) =>
        new(id, "agent:a", "tool.x", context ?? new Dictionary<string, string>(), "capability/x/1.0.0", this.Clock.GetUtcNow().AddHours(1), "corr");
}
