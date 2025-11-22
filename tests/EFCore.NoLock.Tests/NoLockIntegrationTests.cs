using EFCore.NoLock.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace EFCore.NoLock.Tests;

public class NoLockIntegrationTests(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public void Should_Generate_SqlServer_Syntax_With_NoLock()
    {
        // ARRANGE
        var (context, sqlSpy) = TestDbContext.PrepareSql();

        var query = context.Orders
            .Include(x => x.Lines)
            .Where(x => x.Id == 1);

        // ACT 1: Execute original query (Without NoLock)
        try
        {
            query.ToList();
        }
        catch
        {
            // Ignore mapping errors since we are using a Fake DB connection.
            // We only care about the generated SQL.
        }
        var originalSql = sqlSpy.LastCommandText;

        // ACT 2: Execute query WITH NoLock
        try
        {
            query.WithNoLock().ToList();
        }
        catch
        {
            // Ignore mapping errors
        }
        var generatedSql = sqlSpy.LastCommandText;

        // LOGGING
        testOutputHelper.WriteLine("--- ORIGINAL SQL OUTPUT ---");
        testOutputHelper.WriteLine(originalSql);
        testOutputHelper.WriteLine("--- GENERATED SQL OUTPUT ---");
        testOutputHelper.WriteLine(generatedSql);

        // ASSERT
        Assert.NotNull(generatedSql);
        Assert.NotEmpty(generatedSql);

        // 1. Ensure the original SQL did NOT have the hint (Sanity check)
        Assert.DoesNotContain("WITH (NOLOCK)", originalSql, StringComparison.OrdinalIgnoreCase);

        // 2. Verify SQL Server format validation
        Assert.Contains("WITH (NOLOCK)", generatedSql, StringComparison.OrdinalIgnoreCase);
        
        // 3. Verify table names are present (T-SQL syntax check)
        Assert.Contains("[Orders]", generatedSql);
    }
}