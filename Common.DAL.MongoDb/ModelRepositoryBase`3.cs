using System.Net;
using Common.BusinessLogic.Models;
using Common.DAL.MongoDb.Documents;
using Common.DAL.Shared.NoSql;
using Common.Results;
using Common.Results.Models;
using MongoDB.Driver;

namespace Common.DAL.MongoDb;

public abstract class ModelRepositoryBase<TDatabaseModel, TBusinessModel, TId>
    : IModelRepository<TBusinessModel, TId>
    where TDatabaseModel : ServiceDocumentBase<TId>
    where TBusinessModel : ModelBase<TId>
{
    protected readonly IMongoClient Client;
    protected readonly string Collection;
    protected readonly string Database;
    // ReSharper disable once StaticMemberInGenericType
    // intended to allow this to be shared across instances of each instance of the same generic type,
    // but not across instances of different types
    protected static readonly IdGenerationStrategyType IdGenerationStrategy;
    protected readonly IResultFactory ResultFactory;

    static ModelRepositoryBase()
    {
        IdGenerationStrategy = typeof(TDatabaseModel)
            .GetProperty(nameof(ServiceDocumentBase<TId>.Id), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)?
            .GetCustomAttributes(typeof(IdGenerationStrategyAttribute), true)
            .OfType<IdGenerationStrategyAttribute>()
            .SingleOrDefault()?.Strategy ?? IdGenerationStrategyType.Auto;
    }

    protected ModelRepositoryBase(IResultFactory resultFactory,
        IMongoClient mongoClient)
    {
        Client = mongoClient ?? throw new ArgumentNullException(nameof(mongoClient));
        var dbModelInstance = Activator.CreateInstance(typeof(TDatabaseModel)) as TDatabaseModel;
        Collection = dbModelInstance?.Type ?? throw new InvalidOperationException($"{typeof(TDatabaseModel)} missing type setting.");
        Database = dbModelInstance.Service ?? throw new InvalidOperationException($"{typeof(TDatabaseModel)} missing service setting.");
        ResultFactory = resultFactory ?? throw new ArgumentNullException(nameof(resultFactory));
    }

    public virtual async Task<IResult<TBusinessModel>> CreateAsync(TBusinessModel model)
    {
        var dbModel = ToDatabaseModel(model);
        var database = Client.GetDatabase(Database);
        var collection = database.GetCollection<TDatabaseModel>(Collection);

        try
        {
            dbModel.Id = IdGenerationStrategy == IdGenerationStrategyType.Auto ? await GetNextIdAsync() : model.Id;
            await collection.InsertOneAsync(dbModel).ConfigureAwait(false);
            return ResultFactory.Success(model);
        }
        catch (Exception ex)
        {
            return ResultFactory.Error<TBusinessModel>(HttpStatusCode.InternalServerError, ex.Message);
        }
    }

    public virtual async Task<IResult<TBusinessModel>> GetAsync(TId id)
    {
        var database = Client.GetDatabase(Database);
        var collection = database.GetCollection<TDatabaseModel>(Collection);
        var filter = Builders<TDatabaseModel>.Filter.Eq("_id", id);
        var documentList = await collection.FindAsync(filter)
            .ConfigureAwait(false);
        var document = documentList.SingleOrDefault();
        return document == null
            ? ResultFactory.NotFound<TBusinessModel>()
            : ResultFactory.Success(ToBusinessModel(document));
    }

    public virtual async Task<IResult> DeleteAsync(TId id)
    {
        var database = Client.GetDatabase(Database);
        var collection = database.GetCollection<TDatabaseModel>(Collection);
        var filter = Builders<TDatabaseModel>.Filter.Eq("_id", id);
        await collection.FindOneAndDeleteAsync(filter);
        return ResultFactory.Success();
    }

    public async Task<IResult<IEnumerable<TBusinessModel>>> ListAsync()
    {
        var database = Client.GetDatabase(Database);
        var collection = database.GetCollection<TDatabaseModel>(Collection);
        var documents = await collection.FindAsync(Builders<TDatabaseModel>.Filter.Empty,
            new FindOptions<TDatabaseModel>
            {
                Sort = Builders<TDatabaseModel>.Sort.Ascending(m => m.Id)
            }).ConfigureAwait(false);
        return ResultFactory.Success(documents.ToList(CancellationToken.None).Select(ToBusinessModel));
    }

    public async Task<IResult> SaveAsync(TId id, TBusinessModel model)
    {
        var database = Client.GetDatabase(Database);
        var collection = database.GetCollection<TDatabaseModel>(Collection);
        var filter = Builders<TDatabaseModel>.Filter.Eq("_id", id);
        model.Id = id;
        var findResult = await collection.FindOneAndReplaceAsync(filter, ToDatabaseModel(model));
        if (findResult == null)
        {
            return ResultFactory.NotFound<TBusinessModel>();
        }
        return ResultFactory.Success(model);
    }

    protected abstract TBusinessModel ToBusinessModel(TDatabaseModel databaseModel);

    protected abstract TDatabaseModel ToDatabaseModel(TBusinessModel businessModel);

    protected virtual Task<TId> GetNextIdAsync()
    {
        throw new NotImplementedException();
    }
}
