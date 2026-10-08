using System.Collections.Concurrent;
using System.Data.Common;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace EFCore.NoLock.Core;

/// <summary>
/// Provides the shared, ORM-agnostic SQL transformation engine for injecting <c>WITH (NOLOCK)</c> table hints.
/// </summary>
/// <remarks>
/// <para>
/// This class is consumed by both the Entity Framework Core and LinqToDB interceptors.
/// It checks for the <see cref="WithNoLockExtension.NoLockTag"/> query tag (added by <c>.WithNoLock()</c>)
/// and uses <see cref="Microsoft.SqlServer.TransactSql.ScriptDom"/> for safe SQL parsing.
/// A thread-safe cache avoids re-parsing identical queries.
/// </para>
/// </remarks>
public static class NoLockSqlTransformer
{
    private static readonly ConcurrentDictionary<string, string> SqlCache = new();

    /// <summary>
    /// Inspects the current execution context and, if the NOLOCK flag is enabled,
    /// modifies the <see cref="DbCommand.CommandText"/> to include <c>WITH (NOLOCK)</c> hints.
    /// </summary>
    /// <param name="command">The database command whose SQL may be transformed.</param>
    public static void ApplyNoLock(DbCommand command)
    {
        var sql = command.CommandText;
        if (string.IsNullOrWhiteSpace(sql))
            return;

        // EF Core renders query tags as leading comment lines, so the marker is always near the
        // start. Bound the scan to that prefix instead of the whole SQL to keep the per-command
        // cost of untagged queries negligible.
        // ponytail: 256-char prefix window; widen if a caller ever chains enough tags to push ours past it.
        var scan = sql.Length < 256 ? sql.Length : 256;
        if (sql.IndexOf(WithNoLockExtension.NoLockTag, 0, scan, StringComparison.Ordinal) < 0)
            return;

        command.CommandText = SqlCache.GetOrAdd(sql, TransformSql);
    }

    private static string TransformSql(string originalSql)
    {
        using var reader = new StringReader(originalSql);

        var parser = new TSql170Parser(true);
        var fragment = parser.Parse(reader, out var errors);

        if (errors.Count > 0)
        {
            return originalSql;
        }

        var visitor = new WithNoLockVisitor();
        fragment.Accept(visitor);

        var generator = new Sql170ScriptGenerator(new SqlScriptGeneratorOptions
        {
            KeywordCasing = KeywordCasing.Uppercase,
            IncludeSemicolons = true,
            AlignClauseBodies = false
        });

        generator.GenerateScript(fragment, out var transformedSql);

        return transformedSql;
    }
}
