# HoneyDrunk.Operator.Data

Durable storage for HoneyDrunk.Operator, over HoneyDrunk.Data's Entity Framework layer. With it, approvals and their decisions, manual pauses, and budget spend survive a restart and are shared by every host that uses the same database.

```csharp
services.AddHoneyDrunkOperator();
services.AddOperatorDataStore(options => options.UseSqlServer(connectionString));
```

`AddOperatorDataStore` replaces Operator's in-memory store whichever order it is called in.

The package ships no migrations; the host creates the `operator` schema. `Database.EnsureCreated()` works only on a database with no tables yet. On a database shared with other tables, add migrations for `OperatorDbContext` in the host's own project (set `MigrationsAssembly` in the options) and apply them at deployment.

| Table | Holds |
|---|---|
| `operator.Approvals` | Each request, its context, and its current decision |
| `operator.Pauses` | Who paused each agent, why, and since when |
| `operator.Spend` | Spend per agent and budget window |
| `operator.PendingAudits` | Audit entries saved with each change and not yet delivered to the audit sinks |

Times are stored as UTC ticks and money as whole micro-units (one millionth of the currency unit, each charge rounded up so none is lost), so comparisons and sums run in SQL on any provider. Decisions use compare-and-set and spend uses a single update statement, so both stay correct when several hosts share the database. Each change is saved with its audit entries in one transaction, and a charge that reaches its budget is saved with its pause, so a change is never kept without its evidence or its stop. Each outbox entry is claimed by one host at a time, and removed once delivered. Writes run under the provider's execution strategy; when a commit's reply is lost and the strategy retries, a change whose audit entry is already stored is reported as done instead of being applied twice.

Key columns use the database's default collation. On SQL Server that usually ignores case, so `agent:Bot` and `agent:bot` are the same pause there, while the in-memory store and SQLite compare exactly. Keep approval ids and agent ids in one case, or give the key columns a binary collation in a migration.
