using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace HoneyDrunk.Operator.Data.Tests;

/// <summary>A throwaway SQLite file with the Operator schema; each context opens its own connection.</summary>
internal sealed class SqliteDatabase : IDisposable
{
    private readonly string path = Path.Join(Path.GetTempPath(), "operator-" + Guid.NewGuid().ToString("N") + ".db");

    public SqliteDatabase(params IInterceptor[] interceptors)
    {
        this.ConnectionString = $"Data Source={this.path};Default Timeout=30;Pooling=False";
        this.Factory = new ContextFactory(this.ConnectionString, interceptors);
        using var context = this.Factory.CreateDbContext();
        context.Database.EnsureCreated();
    }

    public string ConnectionString { get; }

    public IDbContextFactory<OperatorDbContext> Factory { get; }

    public void Dispose()
    {
        try
        {
            File.Delete(this.path);
        }
        catch (IOException)
        {
            // A connection still closing on another thread; the temp folder is cleaned eventually.
        }
    }

    private sealed class ContextFactory(string connectionString, IInterceptor[] interceptors) : IDbContextFactory<OperatorDbContext>
    {
        public OperatorDbContext CreateDbContext() =>
            new(new DbContextOptionsBuilder<OperatorDbContext>().UseSqlite(connectionString).AddInterceptors(interceptors).Options);
    }
}
