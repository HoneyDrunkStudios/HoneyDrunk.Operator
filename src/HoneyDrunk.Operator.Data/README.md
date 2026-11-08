# HoneyDrunk.Operator.Data

Durable storage for HoneyDrunk.Operator, over HoneyDrunk.Data's Entity Framework layer. With it, approvals and their decisions, manual pauses, and budget spend survive a restart and are shared by every host that uses the same database.

```csharp
services.AddHoneyDrunkOperator();
services.AddOperatorDataStore(options => options.UseSqlServer(connectionString));
```

`AddOperatorDataStore` replaces Operator's in-memory store whichever order it is called in. The host creates the `operator` schema, through migrations or `Database.EnsureCreated()`.

| Table | Holds |
|---|---|
| `operator.Approvals` | Each request, its context, and its current decision |
| `operator.Pauses` | Who paused each agent, why, and since when |
| `operator.Spend` | Spend per agent and budget window |

Times are stored as UTC ticks and money as whole micro-units (one millionth of the currency unit, each charge rounded up so none is lost), so comparisons and sums run in SQL on any provider. Decisions use compare-and-set and spend uses a single update statement, so both stay correct when several hosts share the database.
