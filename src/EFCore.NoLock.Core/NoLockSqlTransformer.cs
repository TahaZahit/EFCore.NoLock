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
/// It checks the <see cref="WithNoLockExtension.IsEnabled"/> flag (set by <c>.WithNoLock()</c>)
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
        if (!WithNoLockExtension.IsEnabled)
            return;

        if (string.IsNullOrWhiteSpace(command.CommandText))
            return;

        var newSql = SqlCache.GetOrAdd(command.CommandText, TransformSql);
        command.CommandText = newSql;

        // Consume the flag so subsequent queries are not affected
        WithNoLockExtension.Reset();
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
