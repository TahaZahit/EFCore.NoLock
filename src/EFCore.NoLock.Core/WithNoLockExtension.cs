using Microsoft.EntityFrameworkCore;

namespace EFCore.NoLock.Core;

/// <summary>
/// Provides the extension method for applying the <c>WITH (NOLOCK)</c> table hint.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="WithNoLock{T}"/> attaches a query tag (<see cref="NoLockTag"/>) to the query. The tag
/// travels with the query through composition (<c>Select</c>, <c>SelectMany</c>, <c>Where</c>, …),
/// through multiple executions of the same <see cref="IQueryable{T}"/>, and through every command of
/// an <c>AsSplitQuery</c>. The registered interceptor (<c>WithNoLockInterceptor</c>) detects the tag in
/// the generated SQL and rewrites every accessed table to include the hint.
/// </para>
/// <para>
/// <b>Warning:</b> Using <c>NOLOCK</c> allows "dirty reads," meaning the query may read uncommitted data
/// from other active transactions. Use this primarily for reporting or high-concurrency read scenarios
/// where strict data consistency is not critical.
/// </para>
/// <para>
/// <b>Important:</b> Ensure that the appropriate interceptor is registered in your ORM configuration;
/// otherwise, this method will have no effect on query behavior.
/// </para>
/// </remarks>
public static class WithNoLockExtension
{
    /// <summary>
    /// The query tag marker emitted by <see cref="WithNoLock{T}"/> and detected by the interceptor
    /// to inject <c>WITH (NOLOCK)</c> hints.
    /// </summary>
    public const string NoLockTag = "__WITH_NOLOCK__";

    /// <summary>
    /// Marks the query to be executed with the <c>WITH (NOLOCK)</c> table hint.
    /// </summary>
    /// <remarks>
    /// Unlike an ambient flag, the hint is bound to the query itself: it survives composition,
    /// repeated execution of the same query and split queries, so every command the query produces
    /// receives the hint.
    /// </remarks>
    /// <param name="query">The source LINQ query to apply the hint to.</param>
    /// <typeparam name="T">The type of the entity being queried.</typeparam>
    /// <returns>The query, tagged for NOLOCK transformation.</returns>
    public static IQueryable<T> WithNoLock<T>(this IQueryable<T> query) => query.TagWith(NoLockTag);
}
