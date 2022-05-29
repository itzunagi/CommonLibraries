using AutoMapper;
using Common.BusinessLogic.Models;
using Common.DAL.Couchbase.Documents;
using Common.Results;
using Common.Results.Models;
using Couchbase.Core.Exceptions.KeyValue;
using System;
using System.Net;
using System.Threading.Tasks;
using System.Linq;
using Common.DAL.Shared.NoSql;
using Couchbase.Extensions.DependencyInjection;

namespace Common.DAL.Couchbase
{
    public abstract class ModelRepositoryBase<TDatabaseModel, TBusinessModel, TId>
        : IModelRepository<TBusinessModel, TId>
        where TDatabaseModel : ServiceDocumentBase<TId>
        where TBusinessModel : ModelBase<TId>
    {
        // ReSharper disable once StaticMemberInGenericType
        // intended to allow this to be shared across instances of each instance of the same generic type,
        // but not across instances of different types
        protected static readonly IdGenerationStrategyType IdGenerationStrategy;
        protected readonly IMapper Mapper;
        protected readonly INamedBucketProvider BucketProvider;
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
            IMapper mapper,
            INamedBucketProvider bucketProvider)
        {
            BucketProvider = bucketProvider ?? throw new ArgumentNullException(nameof(bucketProvider));
            Mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            ResultFactory = resultFactory ?? throw new ArgumentNullException(nameof(resultFactory));
        }

        public virtual async Task<IResult<TBusinessModel>> CreateAsync(TBusinessModel model)
        {
            var bucket = await BucketProvider.GetBucketAsync().ConfigureAwait(false);
            var collection = await bucket.DefaultCollectionAsync().ConfigureAwait(false);
            
            try
            {
                model.Id = IdGenerationStrategy == IdGenerationStrategyType.Auto ? await GetNextIdAsync() : model.Id;
                var key = DocumentMetadataHelper<TDatabaseModel, TId>.GetDocumentKey(model.Id);
                var mutationResult = await collection.InsertAsync(key, Mapper.Map<TDatabaseModel>(model)).ConfigureAwait(false);
                return ResultFactory.Success(model);
            }
            catch (Exception ex)
            {
                return ResultFactory.Error<TBusinessModel>(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        public virtual async Task<IResult<TBusinessModel>> GetAsync(TId id)
        {
            var bucket = await BucketProvider.GetBucketAsync().ConfigureAwait(false);
            var collection = await bucket.DefaultCollectionAsync().ConfigureAwait(false);
            try
            {
                var getResult = await collection.GetAsync(DocumentMetadataHelper<TDatabaseModel, TId>.GetDocumentKey(id)).ConfigureAwait(false);
                var content = getResult.ContentAs<TDatabaseModel>();
                var model = Mapper.Map<TBusinessModel>(content);
                return ResultFactory.Success(model);
            }
            catch (DocumentNotFoundException)
            {
                return ResultFactory.NotFound<TBusinessModel>();
            }
            catch (Exception ex)
            {
                return ResultFactory.Error<TBusinessModel>(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        public virtual async Task<IResult> DeleteAsync(TId id)
        {
            var bucket = await BucketProvider.GetBucketAsync().ConfigureAwait(false);
            var key = DocumentMetadataHelper<TDatabaseModel, TId>.GetDocumentKey(id);
            try
            {
                var collection = await bucket.DefaultCollectionAsync().ConfigureAwait(false);
                await collection.RemoveAsync(key).ConfigureAwait(false);
            }
            catch (DocumentNotFoundException)
            {
                return ResultFactory.Success();
            }
            catch (Exception ex)
            {
                return ResultFactory.Error(HttpStatusCode.InternalServerError, ex.Message);
            }

            return ResultFactory.Success();
        }

        public async Task<IResult> SaveAsync(TId id, TBusinessModel model)
        {
            throw new NotImplementedException();
        }

        protected virtual Task<TId> GetNextIdAsync()
        {
            throw new NotImplementedException();
        }
    }

    public abstract class ModelWithStructIdRepositoryBase<TDatabaseModel, TBusinessModel, TId> : ModelRepositoryBase<TDatabaseModel, TBusinessModel, TId>
        where TDatabaseModel : ServiceDocumentBase<TId>
        where TBusinessModel : ModelBase<TId>
        where TId : struct
    {
        public ModelWithStructIdRepositoryBase(IResultFactory resultFactory,
            IMapper mapper,
            INamedBucketProvider bucketProvider) : base(resultFactory, mapper, bucketProvider)
        {
        }

        protected override Task<TId> GetNextIdAsync()
        {
            // TODO: implement auto-incrementing ids
            throw new NotImplementedException();
        }
    }
}
