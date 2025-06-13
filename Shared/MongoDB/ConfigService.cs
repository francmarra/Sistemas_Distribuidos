using MongoDB.Driver;
using Shared.Models;
using Shared.MongoDB;

namespace Shared.MongoDB
{
    public class ConfigService
    {
        private readonly IMongoDatabase _database;
        private readonly IMongoCollection<ConfigAgr> _configAgrCollection;
        private readonly IMongoCollection<ConfigWavy> _configWavyCollection;
        private readonly IMongoCollection<ConfigServer> _configServerCollection;

        public ConfigService()
        {
            var client = new MongoClient(MongoDBConfig.CONNECTION_STRING);
            _database = client.GetDatabase(MongoDBConfig.DATABASE_NAME);
            _configAgrCollection = _database.GetCollection<ConfigAgr>(MongoDBConfig.CONFIG_AGR_COLLECTION);
            _configWavyCollection = _database.GetCollection<ConfigWavy>(MongoDBConfig.CONFIG_WAVY_COLLECTION);
            _configServerCollection = _database.GetCollection<ConfigServer>(MongoDBConfig.CONFIG_SERVER_COLLECTION);
        }

        // ========== AGGREGATOR CONFIGURATION METHODS ==========
        
        // Get aggregator configuration by ID
        public async Task<ConfigAgr?> GetAgrConfigAsync(string agrId)
        {
            return await _configAgrCollection.Find(x => x.AgrId == agrId).FirstOrDefaultAsync();
        }

        // Get all aggregator configurations
        public async Task<List<ConfigAgr>> GetAllAgrConfigsAsync()
        {
            return await _configAgrCollection.Find(_ => true).ToListAsync();
        }

        // Get aggregators by continent code
        public async Task<List<ConfigAgr>> GetAgrConfigsByContinentAsync(string continentCode)
        {
            return await _configAgrCollection.Find(x => x.ContinentCode == continentCode).ToListAsync();
        }

        // Insert aggregator config
        public async Task InsertAgrConfigAsync(ConfigAgr config)
        {
            await _configAgrCollection.InsertOneAsync(config);
        }

        // Clear and insert all aggregator configs
        public async Task ReplaceAllAgrConfigsAsync(List<ConfigAgr> configs)
        {
            await _configAgrCollection.DeleteManyAsync(_ => true);
            if (configs.Any())
            {
                await _configAgrCollection.InsertManyAsync(configs);
            }
        }

        // ========== WAVY SENSOR CONFIGURATION METHODS ==========

        // Get wavy configuration by ID
        public async Task<ConfigWavy?> GetWavyConfigAsync(string wavyId)
        {
            return await _configWavyCollection.Find(x => x.WavyId == wavyId).FirstOrDefaultAsync();
        }        // Get all wavy configurations
        public async Task<List<ConfigWavy>> GetAllWavyConfigsAsync()
        {
            return await _configWavyCollection.Find(_ => true).ToListAsync();
        }

        // Get active wavy configurations
        public async Task<List<ConfigWavy>> GetActiveWavyConfigsAsync()
        {
            return await _configWavyCollection.Find(x => x.IsActive && x.Status == 1).ToListAsync();        }

        // Update wavy status
        public async Task UpdateWavyStatusAsync(string wavyId, int status, DateTime lastSync)
        {
            var filter = Builders<ConfigWavy>.Filter.Eq(x => x.WavyId, wavyId);
            var update = Builders<ConfigWavy>.Update
                .Set(x => x.Status, status)
                .Set(x => x.LastSync, lastSync);
            
            await _configWavyCollection.UpdateOneAsync(filter, update);
        }        // Insert wavy config
        public async Task InsertWavyConfigAsync(ConfigWavy config)
        {
            await _configWavyCollection.InsertOneAsync(config);
        }

        // Clear all wavy configs
        public async Task ClearAllWavyConfigsAsync()
        {
            await _configWavyCollection.DeleteManyAsync(_ => true);
        }

        // Clear and insert all wavy configs
        public async Task ReplaceAllWavyConfigsAsync(List<ConfigWavy> configs)
        {
            await _configWavyCollection.DeleteManyAsync(_ => true);
            if (configs.Any())
            {
                await _configWavyCollection.InsertManyAsync(configs);
            }
        }

        // ========== SERVER CONFIGURATION METHODS ==========

        // Get server configuration by ID
        public async Task<ConfigServer?> GetServerConfigAsync(string serverId)
        {
            return await _configServerCollection.Find(x => x.ServerId == serverId).FirstOrDefaultAsync();
        }

        // Get server configuration by continent code
        public async Task<ConfigServer?> GetServerConfigByContinentAsync(string continentCode)
        {
            return await _configServerCollection.Find(x => x.ContinentCode == continentCode).FirstOrDefaultAsync();
        }

        // Get all server configurations
        public async Task<List<ConfigServer>> GetAllServerConfigsAsync()
        {
            return await _configServerCollection.Find(_ => true).ToListAsync();
        }

        // Insert server config
        public async Task InsertServerConfigAsync(ConfigServer config)
        {
            await _configServerCollection.InsertOneAsync(config);
        }

        // Clear and insert all server configs
        public async Task ReplaceAllServerConfigsAsync(List<ConfigServer> configs)
        {
            await _configServerCollection.DeleteManyAsync(_ => true);
            if (configs.Any())
            {
                await _configServerCollection.InsertManyAsync(configs);
            }
        }        // ========== CONTINENT-SPECIFIC UTILITY METHODS ==========

        // Get all active continent codes (for servers/aggregators only)
        public async Task<List<string>> GetActiveContinentCodesAsync()
        {
            var servers = await _configServerCollection.Find(x => x.IsActive).ToListAsync();
            return servers.Select(s => s.ContinentCode).Distinct().ToList();
        }
    }
}
