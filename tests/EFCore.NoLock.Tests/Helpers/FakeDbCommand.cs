using System.Collections;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace EFCore.NoLock.Tests.Helpers;

public class FakeDbCommand : DbCommand
{
    [AllowNull]
    public override string CommandText { get; set; } = string.Empty;
    public override int CommandTimeout { get; set; }
    public override CommandType CommandType { get; set; }
    public override UpdateRowSource UpdatedRowSource { get; set; }
    protected override DbConnection? DbConnection { get; set; }
    protected override DbParameterCollection DbParameterCollection { get; } = new FakeDbParameterCollection();
    protected override DbTransaction? DbTransaction { get; set; }
    public override bool DesignTimeVisible { get; set; }

    public override void Cancel() { }

    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
    {
        var dataTable = new DataTable();
        dataTable.Columns.Add("Id", typeof(int));
        dataTable.Columns.Add("CustomerName", typeof(string));
        
        return dataTable.CreateDataReader();
    }

    public override int ExecuteNonQuery() => 0;
    public override object? ExecuteScalar() => null;
    public override void Prepare() { }

    protected override DbParameter CreateDbParameter() => new FakeDbParameter();
}

public class FakeDbConnection : DbConnection
{
    private ConnectionState _state = ConnectionState.Closed;
    
    protected override DbCommand CreateDbCommand() => new FakeDbCommand();

    [AllowNull]
    public override string ConnectionString { get; set; } = "Server=FakeServer;Database=FakeDB;";
    public override string Database => "FakeDB";
    public override ConnectionState State => _state;
    public override string DataSource => "FakeServer";
    public override string ServerVersion => "11.00.0000";

    public override void Open() => _state = ConnectionState.Open;
    public override void Close() => _state = ConnectionState.Closed;
    public override void ChangeDatabase(string databaseName) { }
    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => null!;
}

// Parametreler için boş sınıflar (EF Core hata vermesin diye)
public class FakeDbParameter : DbParameter
{
    public override DbType DbType { get; set; }
    public override ParameterDirection Direction { get; set; }
    public override bool IsNullable { get; set; }
    [AllowNull]
    public override string ParameterName { get; set; } = string.Empty;
    public override int Size { get; set; }
    [AllowNull]
    public override string SourceColumn { get; set; } = string.Empty;
    public override bool SourceColumnNullMapping { get; set; }
    public override object? Value { get; set; }
    public override void ResetDbType() { }
}

public class FakeDbParameterCollection : DbParameterCollection
{
    private readonly List<DbParameter> _parameters = new();
    public override int Add(object value) { _parameters.Add((DbParameter)value); return _parameters.Count - 1; }
    public override void Clear() => _parameters.Clear();
    public override bool Contains(object value) => _parameters.Contains((DbParameter)value);
    public override bool Contains(string value) => true;
    public override int IndexOf(string parameterName)=> 0;
    public override void RemoveAt(string parameterName){} 
    protected override DbParameter GetParameter(string parameterName) => null!;

    public override int IndexOf(object value) => _parameters.IndexOf((DbParameter)value);
    public override void Insert(int index, object value) => _parameters.Insert(index, (DbParameter)value);
    public override void Remove(object value) => _parameters.Remove((DbParameter)value);
    public override void RemoveAt(int index) => _parameters.RemoveAt(index);
    public override void CopyTo(Array array, int index) => _parameters.CopyTo((DbParameter[])array, index);
    protected override void SetParameter(string parameterName, DbParameter value) {}

    public override int Count => _parameters.Count;
    public override object SyncRoot => new();
    public override bool IsSynchronized => false;
    public override IEnumerator GetEnumerator() => _parameters.GetEnumerator();
    protected override DbParameter GetParameter(int index) => _parameters[index];
    protected override void SetParameter(int index, DbParameter value) => _parameters[index] = value;
    public override void AddRange(Array values) => _parameters.AddRange((DbParameter[])values);
}