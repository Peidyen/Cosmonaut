﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Cosmonaut.Extensions;
using Cosmonaut.Response;
using Cosmonaut.Storage;
using Microsoft.Azure.Cosmos;

namespace Cosmonaut
{
    public sealed class CosmosStore<TEntity> /*: ICosmosStore<TEntity>*/ where TEntity : class
    {
        public int CollectionThrouput { get; internal set; } = CosmosConstants.MinimumCosmosThroughput;

        public bool IsUpscaled { get; internal set; }

        public bool IsShared { get; internal set; }

        public string CollectionName { get; private set; }
        
        public string DatabaseName { get; }

        public CosmosStoreSettings Settings { get; }
        
        public CosmosClient CosmosClient { get; }

        public CosmosDatabase CosmosDatabase { get; set; }

        public CosmosContainer CosmosContainer { get; set; }
        
        private readonly IDatabaseCreator _databaseCreator;
        private readonly ICollectionCreator _collectionCreator;

        public CosmosStore(CosmosStoreSettings settings) : this(settings, string.Empty)
        {
        }

        public CosmosStore(CosmosStoreSettings settings, string overriddenCollectionName)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            DatabaseName = settings.DatabaseName;
            CosmosClient = new CosmosClient(Settings.EndpointUrl.ToString(), Settings.AuthKey);
            if (string.IsNullOrEmpty(Settings.DatabaseName)) throw new ArgumentNullException(nameof(Settings.DatabaseName));
            _collectionCreator = new CosmosCollectionCreator(CosmosClient);
            _databaseCreator = new CosmosDatabaseCreator(CosmosClient);
            InitialiseCosmosStore(overriddenCollectionName);
            CosmosDatabase = CosmosClient.Databases[DatabaseName].ReadAsync().GetAwaiter().GetResult();
            CosmosContainer = CosmosDatabase.Containers[CollectionName].ReadAsync().GetAwaiter().GetResult();
        }

        //public CosmosStore(CosmosClient cosmosClient,
        //    string databaseName) : this(cosmosClient, databaseName, string.Empty,
        //    new CosmosDatabaseCreator(cosmosClient),
        //    new CosmosCollectionCreator(cosmosClient))
        //{
        //}

        //public CosmosStore(CosmosClient cosmosClient,
        //    string databaseName,
        //    string overriddenCollectionName) : this(cosmosClient,
        //    databaseName,
        //    overriddenCollectionName,
        //    new CosmosDatabaseCreator(cosmosClient),
        //    new CosmosCollectionCreator(cosmosClient))
        //{
        //}

        //internal CosmosStore(CosmosClient cosmosClient,
        //    string databaseName,
        //    string overriddenCollectionName,
        //    IDatabaseCreator databaseCreator = null,
        //    ICollectionCreator collectionCreator = null,
        //    bool scaleable = false)
        //{
        //    DatabaseName = databaseName;
        //    CosmosClient = cosmosClient ?? throw new ArgumentNullException(nameof(cosmosClient));
        //    Settings = new CosmosStoreSettings(databaseName, cosmosClient.ServiceEndpoint.ToString(), string.Empty, cosmonautClient.DocumentClient.ConnectionPolicy, 
        //        scaleCollectionRUsAutomatically: scaleable);
        //    if (Settings.InfiniteRetries)
        //        CosmonautClient.DocumentClient.SetupInfiniteRetries();
        //    if (string.IsNullOrEmpty(Settings.DatabaseName)) throw new ArgumentNullException(nameof(Settings.DatabaseName));
        //    _collectionCreator = collectionCreator ?? new CosmosCollectionCreator(CosmonautClient);
        //    _databaseCreator = databaseCreator ?? new CosmosDatabaseCreator(CosmonautClient);
        //    _cosmosScaler = new CosmosScaler<TEntity>(this);
        //    InitialiseCosmosStore(overriddenCollectionName);
        //}

        //public IQueryable<TEntity> Query(FeedOptions feedOptions = null)
        //{
        //    var queryable =
        //        CosmonautClient.Query<TEntity>(DatabaseName, CollectionName, GetFeedOptionsForQuery(feedOptions));

        //    return IsShared ? queryable.Where(ExpressionExtensions.SharedCollectionExpression<TEntity>()) : queryable;
        //}

        //public IQueryable<TEntity> Query(string sql, object parameters = null, CosmosQueryRequestOptions requestOptions = null,
        //    CancellationToken cancellationToken = default)
        //{
        //    var collectionSharingFriendlySql = sql.EnsureQueryIsCollectionSharingFriendly<TEntity>();
        //    var iterator = CosmosContainer.Items.CreateItemQuery<TEntity>(new CosmosSqlQueryDefinition(collectionSharingFriendlySql), null, requestOptions:requestOptions);
            
        //}

        public async Task<TEntity> QuerySingleAsync(string sql, object parameters = null, CosmosQueryRequestOptions requestOptions = null, CancellationToken cancellationToken = default)
        {
            var collectionSharingFriendlySql = sql.EnsureQueryIsCollectionSharingFriendly<TEntity>();
            CosmosResultSetIterator<TEntity> queryable = CosmosContainer.Items.CreateItemQuery<TEntity>(new CosmosSqlQueryDefinition(collectionSharingFriendlySql).AddQueryParametersFromObject(parameters), null, requestOptions: requestOptions);
            //TODO Change this
            return (await queryable.ToListAsync(cancellationToken)).Single();
        }

        public async Task<T> QuerySingleAsync<T>(string sql, object parameters = null, CosmosQueryRequestOptions requestOptions = null, CancellationToken cancellationToken = default)
        {
            var collectionSharingFriendlySql = sql.EnsureQueryIsCollectionSharingFriendly<TEntity>();
            CosmosResultSetIterator<T> queryable = CosmosContainer.Items.CreateItemQuery<T>(new CosmosSqlQueryDefinition(collectionSharingFriendlySql).AddQueryParametersFromObject(parameters), null, requestOptions: requestOptions);
            //TODO Change this
            return (await queryable.ToListAsync(cancellationToken)).SingleOrDefault();
        }
        
        public async Task<IEnumerable<TEntity>> QueryMultipleAsync(string sql, object parameters = null, CosmosQueryRequestOptions feedOptions = null, CancellationToken cancellationToken = default)
        {
            var collectionSharingFriendlySql = sql.EnsureQueryIsCollectionSharingFriendly<TEntity>();
            CosmosResultSetIterator<TEntity> queryable = CosmosContainer.Items.CreateItemQuery<TEntity>(new CosmosSqlQueryDefinition(collectionSharingFriendlySql).AddQueryParametersFromObject(parameters), null, requestOptions: feedOptions);
            //TODO Change this
            return (await queryable.ToListAsync(cancellationToken));
        }

        public async Task<IEnumerable<T>> QueryMultipleAsync<T>(string sql, object parameters = null, CosmosQueryRequestOptions feedOptions = null, CancellationToken cancellationToken = default)
        {
            var collectionSharingFriendlySql = sql.EnsureQueryIsCollectionSharingFriendly<TEntity>();
            CosmosResultSetIterator<T> queryable = CosmosContainer.Items.CreateItemQuery<T>(new CosmosSqlQueryDefinition(collectionSharingFriendlySql).AddQueryParametersFromObject(parameters), null, requestOptions: feedOptions);
            //TODO Change this
            return (await queryable.ToListAsync(cancellationToken));
        }

        public async Task<CosmosItemResponse<TEntity>> AddAsync(TEntity entity, object partitionKey = null, CosmosItemRequestOptions cosmosItemRequestOptions = null, CancellationToken cancellationToken = default)
        {
            return await CosmosContainer.Items.CreateItemAsync(partitionKey ?? entity.GetPartitionKeyValueForEntity(), entity, cosmosItemRequestOptions, cancellationToken);
        }
        
        public async Task<CosmosMultipleResponse<TEntity>> AddRangeAsync(IEnumerable<TEntity> entities, Func<TEntity, CosmosItemRequestOptions> CosmosItemRequestOptions = null, CancellationToken cancellationToken = default)
        {
            return await ExecuteMultiOperationAsync(entities, x => AddAsync(x, null, CosmosItemRequestOptions?.Invoke(x), cancellationToken));
        }
        
        //public async Task<CosmosMultipleResponse<TEntity>> RemoveAsync(
        //    Expression<Func<TEntity, bool>> predicate, 
        //    CosmosQueryRequestOptions feedOptions = null,
        //    Func<TEntity, CosmosItemRequestOptions> CosmosItemRequestOptions = null,
        //    CancellationToken cancellationToken = default)
        //{
        //    var entitiesToRemove = await Query(GetFeedOptionsForQuery(feedOptions)).Where(predicate).ToListAsync(cancellationToken);
        //    return await RemoveRangeAsync(entitiesToRemove, CosmosItemRequestOptions, cancellationToken);
        //}

        public async Task<CosmosItemResponse<TEntity>> RemoveAsync(TEntity entity, CosmosItemRequestOptions CosmosItemRequestOptions = null, CancellationToken cancellationToken = default)
        {
            entity.ValidateEntityForCosmosDb();
            var documentId = entity.GetDocumentId();
            //GetPartitionKeyValue(entity.GetDocumentId())
            return await CosmosContainer.Items.DeleteItemAsync<TEntity>(null, documentId, CosmosItemRequestOptions, cancellationToken);
        }
        
        public async Task<CosmosMultipleResponse<TEntity>> RemoveRangeAsync(IEnumerable<TEntity> entities, Func<TEntity, CosmosItemRequestOptions> CosmosItemRequestOptions = null, CancellationToken cancellationToken = default)
        {
            return await ExecuteMultiOperationAsync(entities, x => RemoveAsync(x, CosmosItemRequestOptions?.Invoke(x), cancellationToken));
        }

        public async Task<CosmosItemResponse<TEntity>> UpdateAsync(TEntity entity, CosmosItemRequestOptions CosmosItemRequestOptions = null, CancellationToken cancellationToken = default)
        {
            entity.ValidateEntityForCosmosDb();
            //var document = entity.ToCosmonautDocument();

            return await CosmosContainer.Items.UpsertItemAsync(null, entity, CosmosItemRequestOptions, cancellationToken);
        }
        
        public async Task<CosmosMultipleResponse<TEntity>> UpdateRangeAsync(IEnumerable<TEntity> entities, Func<TEntity, CosmosItemRequestOptions> CosmosItemRequestOptions = null, CancellationToken cancellationToken = default)
        {
            return await ExecuteMultiOperationAsync(entities, x => UpdateAsync(x, CosmosItemRequestOptions?.Invoke(x), cancellationToken));
        }

        public async Task<CosmosItemResponse<TEntity>> UpsertAsync(TEntity entity, CosmosItemRequestOptions CosmosItemRequestOptions = null, CancellationToken cancellationToken = default)
        {
            //var document = entity.ToCosmonautDocument();

            return await CosmosContainer.Items.UpsertItemAsync(null, entity, CosmosItemRequestOptions, cancellationToken);
        }

        public async Task<CosmosMultipleResponse<TEntity>> UpsertRangeAsync(IEnumerable<TEntity> entities, Func<TEntity, CosmosItemRequestOptions> CosmosItemRequestOptions = null, CancellationToken cancellationToken = default)
        {
            return await ExecuteMultiOperationAsync(entities, x => UpsertAsync(x, CosmosItemRequestOptions?.Invoke(x), cancellationToken));
        }
        
        public async Task<CosmosItemResponse<TEntity>> RemoveByIdAsync(string id, CosmosItemRequestOptions CosmosItemRequestOptions = null, CancellationToken cancellationToken = default)
        {
            return await CosmosContainer.Items.DeleteItemAsync<TEntity>(null, id, CosmosItemRequestOptions, cancellationToken);
        }

        public async Task<TEntity> FindAsync(string id, object partitionKeyValue, CosmosItemRequestOptions CosmosItemRequestOptions = null, CancellationToken cancellationToken = default)
        {
            return await CosmosContainer.Items.ReadItemAsync<TEntity>(partitionKeyValue, id, CosmosItemRequestOptions,
                cancellationToken);
        }

        public async Task<TEntity> FindAsync(string id, object partitionKeyValue, CancellationToken cancellationToken = default)
        {
            return await FindAsync(id, partitionKeyValue, new CosmosItemRequestOptions(), cancellationToken);
        }
        
        private void InitialiseCosmosStore(string overridenCollectionName)
        {
            IsShared = typeof(TEntity).UsesSharedCollection();
            CollectionName = GetCosmosStoreCollectionName(overridenCollectionName);

            Settings.DefaultCollectionThroughput = CollectionThrouput = CosmosClient.Databases[DatabaseName].Containers[CollectionName].ReadProvisionedThroughputAsync().ConfigureAwait(false).GetAwaiter()
                .GetResult() ?? typeof(TEntity).GetCollectionThroughputForEntity(Settings.DefaultCollectionThroughput);

            _databaseCreator.EnsureCreatedAsync(DatabaseName).ConfigureAwait(false).GetAwaiter().GetResult();
            _collectionCreator.EnsureCreatedAsync<TEntity>(DatabaseName, CollectionName, CollectionThrouput, Settings.IndexingPolicy)
                .ConfigureAwait(false).GetAwaiter().GetResult();
        }

        private string GetCosmosStoreCollectionName(string overridenCollectionName)
        {
            var hasOverridenName = !string.IsNullOrEmpty(overridenCollectionName);
            return IsShared
                ? $"{Settings.CollectionPrefix ?? string.Empty}{(hasOverridenName ? overridenCollectionName : typeof(TEntity).GetSharedCollectionName())}"
                : $"{Settings.CollectionPrefix ?? string.Empty}{(hasOverridenName ? overridenCollectionName : typeof(TEntity).GetCollectionName())}";
        }

        private async Task<CosmosMultipleResponse<TEntity>> ExecuteMultiOperationAsync(IEnumerable<TEntity> entities,
            Func<TEntity, Task<CosmosItemResponse<TEntity>>> operationFunc)
        {
            var entitiesList = entities.ToList();
            if (!entitiesList.Any())
                return new CosmosMultipleResponse<TEntity>();
            
            var multipleResponse = new CosmosMultipleResponse<TEntity>();
            var results = (await entitiesList.Select(operationFunc).WhenAllTasksAsync()).ToList();
            multipleResponse.SuccessfulEntities.AddRange(results.Where(x => ((int)x.StatusCode >= 200) && ((int)x.StatusCode <= 299)));
            multipleResponse.FailedEntities.AddRange(results.Where(x => !((int)x.StatusCode >= 200) && ((int)x.StatusCode <= 299)));
            return multipleResponse;
        }
        
        private object GetPartitionKeyValue(string id)
        {
            var partitionKeyDefinition = typeof(TEntity).GetPartitionKeyDefinitionForEntity();
            var partitionKeyIsId = partitionKeyDefinition?.Paths?.SingleOrDefault()?.Equals($"/{CosmosConstants.CosmosId}") ?? false;
            return partitionKeyIsId ? id : null;
        }
    }
}