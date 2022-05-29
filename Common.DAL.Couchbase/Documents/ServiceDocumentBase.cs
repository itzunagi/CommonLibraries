namespace Common.DAL.Couchbase.Documents;

public abstract class ServiceDocumentBase<TId>
{
    public abstract string Service { get; }
    public abstract string Type { get; }
    public virtual string DocumentKey => GetDocumentKey(Service, Type, Id);
    public abstract TId Id { get; set; }

    protected internal static string GetDocumentKeyPrefix(string service, string type)
        => $"{service}-{type}-";

    internal static string GetDocumentKey(string service, string type, TId id)
        => $"{GetDocumentKeyPrefix(service, type)}{id}";
}
