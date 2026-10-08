using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace EFCore.NoLock;

#pragma warning disable EF1001 // Internal EF Core API usage.

/// <summary>
/// Registration helpers for the generator-based <c>WITH (NOLOCK)</c> support.
/// </summary>
public static class WithNoLockOptionsExtensions
{
    /// <summary>
    /// Replaces the SQL Server query SQL generator with <see cref="WithNoLockQueryGeneratorFactory"/>,
    /// so queries tagged via <c>.WithNoLock()</c> get the <c>WITH (NOLOCK)</c> hint emitted during SQL
    /// generation (visible in EF Core logs). Prefer this over the <see cref="WithNoLockInterceptor"/>.
    /// </summary>
    public static DbContextOptionsBuilder UseNoLock(this DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.ReplaceService<IQuerySqlGeneratorFactory, WithNoLockQueryGeneratorFactory>();
        return optionsBuilder;
    }

    /// <inheritdoc cref="UseNoLock(DbContextOptionsBuilder)"/>
    public static DbContextOptionsBuilder<TContext> UseNoLock<TContext>(this DbContextOptionsBuilder<TContext> optionsBuilder)
        where TContext : DbContext
    {
        ((DbContextOptionsBuilder)optionsBuilder).UseNoLock();
        return optionsBuilder;
    }
}

#pragma warning restore EF1001
