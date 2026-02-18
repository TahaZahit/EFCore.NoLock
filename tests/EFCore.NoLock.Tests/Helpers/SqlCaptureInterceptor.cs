using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EFCore.NoLock.Tests.Helpers;

public class SqlCaptureInterceptor : DbCommandInterceptor
{
    public string LastCommandText { get; private set; } = string.Empty;

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, 
        CommandEventData eventData, 
        InterceptionResult<DbDataReader> result)
    {
        LastCommandText = command.CommandText;
        return base.ReaderExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        LastCommandText = command.CommandText;
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }
}