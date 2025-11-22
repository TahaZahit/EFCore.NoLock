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
}