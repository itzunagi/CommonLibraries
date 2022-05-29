using Common.DAL.Couchbase;
using MongoDB.Bson.Serialization.Attributes;

namespace Common.DAL.MongoDb.Documents;

public abstract class ServiceDocumentBase<TId>
{
    public abstract string Service { get; }
    public abstract string Type { get; }
    [BsonId]
    [IdGenerationStrategy(IdGenerationStrategyType.Auto)]
    public abstract TId Id { get; set; }
}
