using System.Linq.Expressions;
using EFCore.NoLock.Core;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.SqlServer.Query.Internal;
#if NET7_0_OR_GREATER
using Microsoft.EntityFrameworkCore.Storage;
#endif
#if NET8_0_OR_GREATER
using Microsoft.EntityFrameworkCore.SqlServer.Infrastructure.Internal;
#endif

namespace EFCore.NoLock;

#pragma warning disable EF1001 // Internal EF Core API usage (query SQL generator).

/// <summary>
/// A <see cref="SqlServerQuerySqlGenerator"/> that emits the <c>WITH (NOLOCK)</c> table hint while the
/// SQL is being generated — so the hint is part of the SQL EF Core produces, appears in EF Core's own
/// command logs, and needs no post-execution rewrite. The hint is applied only to queries tagged with
/// <see cref="WithNoLockExtension.NoLockTag"/> (i.e. built with <c>.WithNoLock()</c>).
/// </summary>
public sealed class WithNoLockQueryGenerator : SqlServerQuerySqlGenerator
{
#if NET8_0_OR_GREATER
    public WithNoLockQueryGenerator(QuerySqlGeneratorDependencies dependencies, IRelationalTypeMappingSource typeMappingSource, ISqlServerSingletonOptions sqlServerSingletonOptions)
        : base(dependencies, typeMappingSource, sqlServerSingletonOptions) { }
#elif NET7_0
    public WithNoLockQueryGenerator(QuerySqlGeneratorDependencies dependencies, IRelationalTypeMappingSource typeMappingSource)
        : base(dependencies, typeMappingSource) { }
#else
    public WithNoLockQueryGenerator(QuerySqlGeneratorDependencies dependencies)
        : base(dependencies) { }
#endif

    private bool _noLock;

    protected override Expression VisitSelect(SelectExpression selectExpression)
    {
        if (!_noLock && selectExpression.Tags.Contains(WithNoLockExtension.NoLockTag))
            _noLock = true;

        return base.VisitSelect(selectExpression);
    }

    protected override Expression VisitTable(TableExpression tableExpression)
    {
        var result = base.VisitTable(tableExpression);
        if (_noLock)
            Sql.Append(" WITH (NOLOCK)");

        return result;
    }
}

/// <summary>
/// Registers <see cref="WithNoLockQueryGenerator"/> in place of the default SQL Server query SQL
/// generator. Wire it up with <c>optionsBuilder.UseNoLock()</c>.
/// </summary>
public sealed class WithNoLockQueryGeneratorFactory : SqlServerQuerySqlGeneratorFactory
{
    private readonly QuerySqlGeneratorDependencies _dependencies;
#if NET7_0_OR_GREATER
    private readonly IRelationalTypeMappingSource _typeMappingSource;
#endif
#if NET8_0_OR_GREATER
    private readonly ISqlServerSingletonOptions _sqlServerSingletonOptions;

    public WithNoLockQueryGeneratorFactory(QuerySqlGeneratorDependencies dependencies, IRelationalTypeMappingSource typeMappingSource, ISqlServerSingletonOptions sqlServerSingletonOptions)
        : base(dependencies, typeMappingSource, sqlServerSingletonOptions)
    {
        _dependencies = dependencies;
        _typeMappingSource = typeMappingSource;
        _sqlServerSingletonOptions = sqlServerSingletonOptions;
    }

    public override QuerySqlGenerator Create()
        => new WithNoLockQueryGenerator(_dependencies, _typeMappingSource, _sqlServerSingletonOptions);
#elif NET7_0
    public WithNoLockQueryGeneratorFactory(QuerySqlGeneratorDependencies dependencies, IRelationalTypeMappingSource typeMappingSource)
        : base(dependencies, typeMappingSource)
    {
        _dependencies = dependencies;
        _typeMappingSource = typeMappingSource;
    }

    public override QuerySqlGenerator Create()
        => new WithNoLockQueryGenerator(_dependencies, _typeMappingSource);
#else
    public WithNoLockQueryGeneratorFactory(QuerySqlGeneratorDependencies dependencies)
        : base(dependencies)
    {
        _dependencies = dependencies;
    }

    public override QuerySqlGenerator Create()
        => new WithNoLockQueryGenerator(_dependencies);
#endif
}

#pragma warning restore EF1001
