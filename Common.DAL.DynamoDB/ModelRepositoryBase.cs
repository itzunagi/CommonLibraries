using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Common.BusinessLogic.Models;
using Common.DAL.DynamoDB.Models;
using Common.DAL.Shared.NoSql;
using Common.Results;
using Common.Results.Models;

namespace Common.DAL.DynamoDB;

public abstract class ModelRepositoryBase<TDatabaseModel, TBusinessModel>
    : IModelRepository<TBusinessModel, long>
    where TDatabaseModel : DatabaseModelBase<TDatabaseModel, long>
    where TBusinessModel : ModelBase<long>
{
    protected readonly IAmazonDynamoDB AmazonDynamoDb;

    protected ModelRepositoryBase(
        IAmazonDynamoDB amazonDynamoDb)
    {
        AmazonDynamoDb = amazonDynamoDb ?? throw new ArgumentNullException(nameof(amazonDynamoDb));
    }

    public virtual async Task<IResult<TBusinessModel>> CreateAsync(TBusinessModel model)
    {
        var tableResponse = await AmazonDynamoDb.ListTablesAsync();
        if (!tableResponse.TableNames.Contains(DatabaseModelBase<TDatabaseModel, long>.GetTableName()))
        {
            // Create our table if it doesn't exist
            var createTableResponse = await AmazonDynamoDb.CreateTableAsync(new CreateTableRequest
            {
                TableName = DatabaseModelBase<TDatabaseModel, long>.GetTableName(),
                AttributeDefinitions = new List<AttributeDefinition>
                {
                    new()
                    {
                        AttributeName = "Id",
                        AttributeType = ScalarAttributeType.N
                    }
                },
                KeySchema = new List<KeySchemaElement>
                {
                    new()
                    {
                        AttributeName = "Key",
                        KeyType = KeyType.HASH
                    }
                },
                BillingMode = BillingMode.PAY_PER_REQUEST
            });

            if (createTableResponse.HttpStatusCode != HttpStatusCode.OK)
            {
                return ResultFactory.Error<TBusinessModel>(createTableResponse.HttpStatusCode, createTableResponse.ToString());
            }
        }

        // table exists now

        throw new NotImplementedException();
    }

    public Task<IResult<TBusinessModel>> GetAsync(long id)
    {
        throw new NotImplementedException();
    }

    public Task<IResult> DeleteAsync(long id)
    {
        throw new NotImplementedException();
    }

    public Task<IResult<IEnumerable<TBusinessModel>>> ListAsync()
    {
        throw new NotImplementedException();
    }

    public Task<IResult> SaveAsync(long id, TBusinessModel model)
    {
        throw new NotImplementedException();
    }
}
