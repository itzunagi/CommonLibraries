using System.Linq;

namespace Common.DAL.DynamoDB.Models;

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
