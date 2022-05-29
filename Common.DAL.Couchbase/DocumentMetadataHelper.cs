using Common.DAL.Couchbase.Documents;
using System;
using System.Collections.Concurrent;

namespace Common.DAL.Couchbase
{
    internal class DocumentMetadataHelper<TDatabaseModel, TId>
        where TDatabaseModel : ServiceDocumentBase<TId>
    {
        private static readonly ConcurrentDictionary<Type, Tuple<string, string>> _documentServiceTypeCache = new();

        public static string GetDocumentKey(TId id)
        {
            var serviceType = _documentServiceTypeCache.GetOrAdd(typeof(TDatabaseModel), modelType =>
            {
                var modelInstance = (TDatabaseModel)Activator.CreateInstance(modelType);
                var service = modelInstance.Service;
                var type = modelInstance.Type;
                return new Tuple<string, string>(service, type);
            });

            return ServiceDocumentBase<TId>.GetDocumentKey(serviceType.Item1, serviceType.Item2, id);
        }
    }
}
