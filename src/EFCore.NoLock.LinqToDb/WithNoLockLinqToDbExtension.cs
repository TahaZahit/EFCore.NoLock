using EFCore.NoLock.Core;
using LinqToDB;

namespace EFCore.NoLock.LinqToDb;

/// <summary>
/// LinqToDB counterpart of <see cref="WithNoLockExtension.WithNoLock{T}"/>.
/// </summary>
/// <remarks>
/// Tags the query with <see cref="WithNoLockExtension.NoLockTag"/> via LinqToDB's
/// <c>TagQuery</c>, which renders it as a leading SQL comment. The registered
/// <see cref="LinqToDbWithNoLockInterceptor"/> detects the tag and injects the
/// <c>WITH (NOLOCK)</c> hint. Because the tag is part of the query it survives composition,
/// repeated execution and split queries.
/// </remarks>
public static class WithNoLockLinqToDbExtension
{
    /// <summary>
    /// Marks the query to be executed with the <c>WITH (NOLOCK)</c> table hint.
    /// </summary>
    /// <param name="query">The source LinqToDB query.</param>
    /// <typeparam name="T">The type of the entity being queried.</typeparam>
    /// <returns>The query, tagged for NOLOCK transformation.</returns>
    public static IQueryable<T> WithNoLock<T>(this IQueryable<T> query)
        => query.TagQuery(WithNoLockExtension.NoLockTag);
}
