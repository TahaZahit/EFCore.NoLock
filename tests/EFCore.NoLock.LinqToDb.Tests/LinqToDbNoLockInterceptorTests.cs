using System.Data.Common;
using EFCore.NoLock.Core;
using EFCore.NoLock.LinqToDb;
using LinqToDB.Interceptors;
using Xunit.Abstractions;

namespace EFCore.NoLock.LinqToDb.Tests;

public class LinqToDbNoLockInterceptorTests(ITestOutputHelper testOutputHelper)
{
    /// <summary>
    /// Verifies that the interceptor transforms SQL when the NoLock flag is enabled
    /// to include WITH (NOLOCK) on all table references.
    /// </summary>
    [Fact]
    public void Should_Inject_NoLock_When_Flag_Is_Enabled()
    {
        // ARRANGE
        var interceptor = new LinqToDbWithNoLockInterceptor();
        var command = new FakeDbCommand
        {
            CommandText = """
                          SELECT [o].[Id], [o].[CustomerName], [o0].[Id], [o0].[OrderId], [o0].[Product]
                          FROM [Orders] AS [o]
                          LEFT JOIN [OrderLines] AS [o0] ON [o].[Id] = [o0].[OrderId]
                          WHERE [o].[Id] = 1
                          ORDER BY [o].[Id]
                          """
        };

        var originalSql = command.CommandText;

        // ACT — Simulate calling .WithNoLock() before query execution
        var dummyQuery = Array.Empty<object>().AsQueryable().WithNoLock();
        var eventData = new CommandEventData();
        var result = interceptor.CommandInitialized(eventData, command);

        var transformedSql = result.CommandText;

        // LOGGING
        testOutputHelper.WriteLine("--- ORIGINAL SQL ---");
        testOutputHelper.WriteLine(originalSql);
        testOutputHelper.WriteLine("--- TRANSFORMED SQL ---");
        testOutputHelper.WriteLine(transformedSql);

        // ASSERT
        Assert.NotNull(transformedSql);
        Assert.NotEmpty(transformedSql);
        Assert.Contains("WITH (NOLOCK)", transformedSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[Orders]", transformedSql);
        Assert.Contains("[OrderLines]", transformedSql);
    }

    /// <summary>
    /// Verifies that the interceptor does NOT modify SQL when the NoLock flag is not set.
    /// </summary>
    [Fact]
    public void Should_Not_Modify_Sql_Without_Flag()
    {
        // ARRANGE
        var interceptor = new LinqToDbWithNoLockInterceptor();
        var originalSql = """
                          SELECT [o].[Id], [o].[CustomerName]
                          FROM [Orders] AS [o]
                          WHERE [o].[Id] = 1
                          """;
        var command = new FakeDbCommand { CommandText = originalSql };

        // ACT — No .WithNoLock() call
        var eventData = new CommandEventData();
        interceptor.CommandInitialized(eventData, command);

        // ASSERT
        Assert.Equal(originalSql, command.CommandText);
        Assert.DoesNotContain("WITH (NOLOCK)", command.CommandText, StringComparison.OrdinalIgnoreCase);
    }
}
