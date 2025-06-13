using MongoDB.Driver;
using Shared.Models;

namespace Shared.MongoDB
{
    public class SubscriptionService
    {
        private readonly IMongoCollection<AggregatorSubscription> _subscriptions;

        public SubscriptionService()
        {
            var client = new MongoClient(MongoDBConfig.CONNECTION_STRING);
            var database = client.GetDatabase(MongoDBConfig.DATABASE_NAME);
            _subscriptions = database.GetCollection<AggregatorSubscription>("ConfigAgr");
        }

        public async Task<AggregatorSubscription?> GetSubscriptionAsync(string aggregatorId)
        {
            return await _subscriptions.Find(s => s.AggregatorId == aggregatorId && s.IsActive)
                                     .FirstOrDefaultAsync();
        }

        public async Task<List<AggregatorSubscription>> GetAllSubscriptionsAsync()
        {
            return await _subscriptions.Find(s => s.IsActive).ToListAsync();
        }

        public async Task<List<AggregatorSubscription>> GetSubscriptionsByRegionAsync(string region)
        {
            return await _subscriptions.Find(s => s.Region == region && s.IsActive).ToListAsync();
        }

        public async Task<List<AggregatorSubscription>> GetSubscriptionsByOceanAsync(string ocean)
        {
            return await _subscriptions.Find(s => s.Ocean == ocean && s.IsActive).ToListAsync();
        }

        public async Task CreateSubscriptionAsync(AggregatorSubscription subscription)
        {
            subscription.CreatedAt = DateTime.UtcNow;
            subscription.UpdatedAt = DateTime.UtcNow;
            await _subscriptions.InsertOneAsync(subscription);
        }

        public async Task UpdateSubscriptionAsync(AggregatorSubscription subscription)
        {
            subscription.UpdatedAt = DateTime.UtcNow;
            await _subscriptions.ReplaceOneAsync(s => s.AggregatorId == subscription.AggregatorId, subscription);
        }

        public async Task DeleteSubscriptionAsync(string aggregatorId)
        {
            await _subscriptions.UpdateOneAsync(
                s => s.AggregatorId == aggregatorId,
                Builders<AggregatorSubscription>.Update.Set(s => s.IsActive, false).Set(s => s.UpdatedAt, DateTime.UtcNow)
            );
        }

        public async Task ClearAllSubscriptionsAsync()
        {
            await _subscriptions.DeleteManyAsync(FilterDefinition<AggregatorSubscription>.Empty);
        }

        public async Task<bool> IsDataTypeSubscribedAsync(string aggregatorId, string dataType)
        {
            var subscription = await GetSubscriptionAsync(aggregatorId);
            return subscription != null && subscription.SubscribedDataTypes.Contains(dataType);
        }
    }
}
