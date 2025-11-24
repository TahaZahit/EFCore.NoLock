using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace EFCore.NoLock;

internal class WithNoLockVisitor : TSqlFragmentVisitor
{
    private bool _isInSelect;

    public override void ExplicitVisit(SelectStatement node)
    {
        _isInSelect = true;
        base.ExplicitVisit(node); // Process children (FROM clauses, etc.)
        _isInSelect = false;
    }

    public override void ExplicitVisit(NamedTableReference node)
    {
        if (!_isInSelect) return;
        var hasNoLock = node.TableHints.Any(hint => hint.HintKind == TableHintKind.NoLock);

        if (!hasNoLock)
        {
            var noLockHint = new TableHint
            {
                HintKind = TableHintKind.NoLock
            };
            node.TableHints.Add(noLockHint);
        }

        base.ExplicitVisit(node);
    }
}