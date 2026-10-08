using EFCore.NoLock.Core;
using Xunit;

namespace EFCore.NoLock.Tests;

/// <summary>
/// The transform cache must not grow per user/request. Leading comment tags (APM/correlation ids,
/// the NOLOCK marker) are stripped before the SQL is used as the cache key, so queries that differ
/// only in those tags collapse to a single cache entry.
/// </summary>
public class CacheKeyTests
{
    private const string Body = "SELECT [x].[Id] FROM [T] AS [x]";

    [Fact]
    public void Strips_Leading_Line_Comments()
    {
        var sql = "--cid:AAAA\n--uid:BBBB\n-- " + WithNoLockExtension.NoLockTag + "\n\n" + Body;
        Assert.Equal(Body, NoLockSqlTransformer.StripLeadingComments(sql));
    }

    [Fact]
    public void Strips_Leading_Block_Comments()
    {
        var sql = "/* cid:AAAA */ /* uid:BBBB */\n" + Body;
        Assert.Equal(Body, NoLockSqlTransformer.StripLeadingComments(sql));
    }

    [Fact]
    public void Two_Users_Same_Query_Collapse_To_One_Key()
    {
        var userA = "--cid:11111111-1111-1111-1111-111111111111\n--uid:aaaa\n" + Body;
        var userB = "--cid:22222222-2222-2222-2222-222222222222\n--uid:bbbb\n" + Body;
        Assert.Equal(
            NoLockSqlTransformer.StripLeadingComments(userA),
            NoLockSqlTransformer.StripLeadingComments(userB));
    }

    [Fact]
    public void Leaves_Comment_Free_Sql_Unchanged()
    {
        Assert.Equal(Body, NoLockSqlTransformer.StripLeadingComments(Body));
    }

    [Fact]
    public void Does_Not_Strip_Comments_After_The_Sql_Body()
    {
        var sql = Body + " -- trailing";
        Assert.Equal(sql, NoLockSqlTransformer.StripLeadingComments(sql));
    }
}
