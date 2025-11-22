using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace EFCore.NoLock;

internal class WithNoLockVisitor : TSqlFragmentVisitor
{
    public override void ExplicitVisit(NamedTableReference node)
    {
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