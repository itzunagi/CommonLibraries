using System;

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
