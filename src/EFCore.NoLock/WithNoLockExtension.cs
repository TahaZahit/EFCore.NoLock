using Microsoft.EntityFrameworkCore;

namespace EFCore.NoLock;

/// <summary>
/// Provides extension methods for applying the <c>WITH (NOLOCK)</c> table hint to Entity Framework Core LINQ queries.
/// </summary>
public static class WithNoLockExtension
{
    /// <summary>
    /// Marks the current query to be executed with the <c>WITH (NOLOCK)</c> table hint.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method tags the query with a specific internal marker. The <see cref="WithNoLockInterceptor"/> 
    /// detects this marker and modifies the generated SQL to include <c>WITH (NOLOCK)</c> on all accessed tables.
    /// </para>
    /// <para>
    /// <b>Important:</b> Ensure that the <c>WithNoLockInterceptor</c> is registered in your DbContext configuration; 
    /// otherwise, this method will only add a comment to the SQL without changing the locking behavior.
    /// </para>
    /// <para>
    /// <b>Warning:</b> Using <c>NOLOCK</c> allows "dirty reads," meaning the query may read uncommitted data 
    /// from other active transactions. Use this primarily for reporting or high-concurrency read scenarios 
    /// where strict data consistency is not critical.
    /// </para>
    /// </remarks>
    /// <param name="query">The source LINQ query to apply the hint to.</param>
    /// <typeparam name="T">The type of the entity being queried.</typeparam>
    /// <returns>A new <see cref="IQueryable{T}"/> containing the necessary tag for the interceptor.</returns>
    public static IQueryable<T> WithNoLock<T>(this IQueryable<T> query)
    {
        return query.TagWith("NOLOCK_MODE");
    }
}