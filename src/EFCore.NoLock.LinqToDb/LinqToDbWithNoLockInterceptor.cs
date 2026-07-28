using System.Data.Common;
using EFCore.NoLock.Core;
using LinqToDB.Interceptors;

namespace EFCore.NoLock.LinqToDb;

/// <summary>
/// A LinqToDB interceptor that injects <c>WITH (NOLOCK)</c> hints into SQL Server queries.
/// </summary>
/// <remarks>
/// <para>
/// This interceptor hooks into LinqToDB's command pipeline. Every initialized command is handed to
/// <see cref="NoLockSqlTransformer"/>, which safely modifies the SQL syntax tree only when the current
/// async flow has been flagged by the <see cref="WithNoLockExtension.WithNoLock{T}"/> extension. That
/// flag is consumed by the first command it affects, so only the immediately executed query changes.
/// </para>
/// <para>
/// <b>Registration:</b> Add the interceptor to your <c>DataConnection</c> or <c>DataContext</c>:
/// <code>
/// dataConnection.AddInterceptor(new LinqToDbWithNoLockInterceptor());
/// </code>
/// Or via fluent configuration:
/// <code>
/// var builder = new LinqToDbConnectionOptionsBuilder()
///     .UseSqlServer(connectionString)
///     .WithInterceptor(new LinqToDbWithNoLockInterceptor());
/// </code>
/// </para>
/// <para>
/// <b>Performance Note:</b> The underlying transformer uses <see cref="Microsoft.SqlServer.TransactSql.ScriptDom"/>
/// for safe SQL parsing and includes a thread-safe cache to prevent re-parsing identical queries.
/// </para>
/// </remarks>
public class LinqToDbWithNoLockInterceptor : CommandInterceptor
{
    /// <summary>
    /// Called when a LinqToDB command is initialized, before execution.
    /// Both synchronous and asynchronous queries pass through this method.
    /// </summary>
    /// <param name="eventData">Contextual information about the command event.</param>
    /// <param name="command">The <see cref="DbCommand"/> to potentially transform.</param>
    /// <returns>The (potentially modified) <see cref="DbCommand"/>.</returns>
    public override DbCommand CommandInitialized(CommandEventData eventData, DbCommand command)
    {
        NoLockSqlTransformer.ApplyNoLock(command);
        return base.CommandInitialized(eventData, command);
    }
}
