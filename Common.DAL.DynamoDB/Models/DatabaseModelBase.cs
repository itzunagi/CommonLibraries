using System;
using System.Linq;

namespace Common.DAL.DynamoDB.Models;

public abstract class DatabaseModelBase<TId> where TId : struct
{
    public TId Id { get; set; }
}

[AttributeUsage(validOn: AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class TableNameAttribute : Attribute
{
    public string Name { get; }

    public TableNameAttribute(string tableName)
    {
        Name = tableName;
    }
}

public abstract class DatabaseModelBase<TDatabaseModel, TId> : DatabaseModelBase<TId>
    where TDatabaseModel : DatabaseModelBase<TDatabaseModel, TId>
    where TId : struct
{
    public static string GetTableName()
    {
        var tableNameAttribute = typeof(TDatabaseModel)
            .GetCustomAttributes(typeof(TableNameAttribute), false)
            .OfType<TableNameAttribute>()
            .SingleOrDefault();

        return tableNameAttribute?.Name;
    }
}
