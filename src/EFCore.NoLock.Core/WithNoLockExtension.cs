namespace EFCore.NoLock.Core;

/// <summary>
/// Provides the unified, ORM-agnostic extension method for applying the <c>WITH (NOLOCK)</c> table hint.
/// </summary>
/// <remarks>
/// <para>
/// This extension works with both Entity Framework Core and LinqToDB query providers.
/// It sets an <see cref="AsyncLocal{T}"/> flag that is detected by the registered interceptor
/// (<c>WithNoLockInterceptor</c> or <c>LinqToDbWithNoLockInterceptor</c>) during command execution.
/// </para>
/// <para>
/// <b>Warning:</b> Using <c>NOLOCK</c> allows "dirty reads," meaning the query may read uncommitted data
/// from other active transactions. Use this primarily for reporting or high-concurrency read scenarios
/// where strict data consistency is not critical.
/// </para>
/// </remarks>
public static class WithNoLockExtension
{
    private static readonly AsyncLocal<bool> NoLockFlag = new();

    /// <summary>
    /// Gets a value indicating whether the current execution context has NOLOCK enabled.
    /// </summary>
    internal static bool IsEnabled => NoLockFlag.Value;

    /// <summary>
    /// Resets the NOLOCK flag for the current execution context.
    /// Called by interceptors after applying the transformation.
    /// </summary>
    internal static void Reset() => NoLockFlag.Value = false;

    /// <summary>
    /// Marks the current query to be executed with the <c>WITH (NOLOCK)</c> table hint.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method sets an execution-context flag that the registered interceptor detects.
    /// The flag is automatically consumed (reset) after the interceptor transforms the SQL,
    /// so only the immediately executed query is affected.
    /// </para>
    /// <para>
    /// <b>Important:</b> Ensure that the appropriate interceptor is registered in your ORM configuration;
    /// otherwise, this method will have no effect on query behavior.
    /// </para>
    /// </remarks>
    /// <param name="query">The source LINQ query to apply the hint to.</param>
    /// <typeparam name="T">The type of the entity being queried.</typeparam>
    /// <returns>The same <see cref="IQueryable{T}"/> instance, now flagged for NOLOCK transformation.</returns>
    public static IQueryable<T> WithNoLock<T>(this IQueryable<T> query)
    {
        NoLockFlag.Value = true;
        return query;
    }
}
