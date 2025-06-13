using MongoDB.Driver;
using MongoDB.Bson;
using Shared.Models;
using System.Text.Json;

namespace Shared.MongoDB
{
    public class MongoDBService
    {
        private readonly IMongoDatabase _database;
        private readonly IMongoCollection<BsonDocument> _wavyMessagesCollection;
        private readonly IMongoCollection<BsonDocument> _aggregatedDataCollection;
        private readonly IMongoCollection<BsonDocument> _systemLogsCollection;

        public MongoDBService()
        {
            try
            {
                var client = new MongoClient(MongoDBConfig.CONNECTION_STRING);
                _database = client.GetDatabase(MongoDBConfig.DATABASE_NAME);

                _wavyMessagesCollection = _database.GetCollection<BsonDocument>(MongoDBConfig.WAVY_MESSAGES_COLLECTION);
                _aggregatedDataCollection = _database.GetCollection<BsonDocument>(MongoDBConfig.AGGREGATED_DATA_COLLECTION);
                _systemLogsCollection = _database.GetCollection<BsonDocument>(MongoDBConfig.SYSTEM_LOGS_COLLECTION);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MongoDB] Error initializing MongoDB service: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                await _database.RunCommandAsync((Command<BsonDocument>)"{ping:1}");
                Console.WriteLine("[MongoDB] Connection test successful");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MongoDB] Connection test failed: {ex.Message}");
                return false;
            }
        }        public async Task InsertWavyMessageAsync(WavyMessage message)
        {
            try
            {                var document = new BsonDocument
                {
                    ["wavy_id"] = message.WavyId,
                    ["latitude"] = message.Latitude,
                    ["longitude"] = message.Longitude,
                    ["timestamp"] = message.Timestamp,
                    ["received_at"] = DateTime.UtcNow,
                    
                    // Ocean Sensor Data
                    ["sea_surface_temperature_celsius"] = message.SeaSurfaceTemperatureCelsius,
                    ["wind_speed_ms"] = message.WindSpeedMs,
                    ["wind_direction_degrees"] = message.WindDirectionDegrees,
                    ["sea_level_meters"] = message.SeaLevelMeters,
                    ["current_speed_ms"] = message.CurrentSpeedMs,
                    ["current_direction_degrees"] = message.CurrentDirectionDegrees,
                    ["salinity_psu"] = message.SalinityPsu,
                    ["chlorophyll_mg_m3"] = message.ChlorophyllMgM3,
                    ["wave_height_meters"] = message.WaveHeightMeters,
                    ["wave_direction_degrees"] = message.WaveDirectionDegrees,
                    ["acoustic_level_db"] = message.AcousticLevelDb,
                    ["turbidity_ntu"] = message.TurbidityNtu,
                    ["precipitation_rate_mm_h"] = message.PrecipitationRateMmH,
                    ["surface_pressure_hpa"] = message.SurfacePressureHpa,
                    ["temperature_gradient_c_km"] = message.TemperatureGradientCKm,
                    
                    // Create comprehensive sensors array
                    ["sensors"] = new BsonArray(new[]
                    {
                        new BsonDocument { ["type"] = "sea_surface_temperature", ["value"] = message.SeaSurfaceTemperatureCelsius, ["unit"] = "celsius" },
                        new BsonDocument { ["type"] = "wind_speed", ["value"] = message.WindSpeedMs, ["unit"] = "m/s" },
                        new BsonDocument { ["type"] = "wind_direction", ["value"] = message.WindDirectionDegrees, ["unit"] = "degrees" },
                        new BsonDocument { ["type"] = "sea_level", ["value"] = message.SeaLevelMeters, ["unit"] = "meters" },
                        new BsonDocument { ["type"] = "current_speed", ["value"] = message.CurrentSpeedMs, ["unit"] = "m/s" },
                        new BsonDocument { ["type"] = "current_direction", ["value"] = message.CurrentDirectionDegrees, ["unit"] = "degrees" },
                        new BsonDocument { ["type"] = "salinity", ["value"] = message.SalinityPsu, ["unit"] = "psu" },
                        new BsonDocument { ["type"] = "chlorophyll", ["value"] = message.ChlorophyllMgM3, ["unit"] = "mg/m3" },
                        new BsonDocument { ["type"] = "wave_height", ["value"] = message.WaveHeightMeters, ["unit"] = "meters" },
                        new BsonDocument { ["type"] = "wave_direction", ["value"] = message.WaveDirectionDegrees, ["unit"] = "degrees" },
                        new BsonDocument { ["type"] = "acoustic_level", ["value"] = message.AcousticLevelDb, ["unit"] = "db" },
                        new BsonDocument { ["type"] = "turbidity", ["value"] = message.TurbidityNtu, ["unit"] = "ntu" },
                        new BsonDocument { ["type"] = "precipitation_rate", ["value"] = message.PrecipitationRateMmH, ["unit"] = "mm/h" },
                        new BsonDocument { ["type"] = "surface_pressure", ["value"] = message.SurfacePressureHpa, ["unit"] = "hpa" },
                        new BsonDocument { ["type"] = "temperature_gradient", ["value"] = message.TemperatureGradientCKm, ["unit"] = "c/km" }
                    })
                };                await _wavyMessagesCollection.InsertOneAsync(document);
                // Console.WriteLine($"[MongoDB] Wavy message from {message.WavyId} inserted successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MongoDB] Error inserting Wavy message: {ex.Message}");
                // Don't throw to avoid breaking the main flow
            }
        }        public async Task InsertAggregatedDataAsync(AggregatedData data)
        {
            try
            {
                var document = new BsonDocument
                {
                    ["agregador_id"] = data.AgregadorId,
                    ["timestamp"] = data.Timestamp,
                    ["received_at"] = DateTime.UtcNow,
                    ["message_count"] = data.Messages.Count,                    ["messages"] = new BsonArray(data.Messages.Select(m => new BsonDocument
                    {
                        ["wavy_id"] = m.WavyId,
                        ["latitude"] = m.Latitude,
                        ["longitude"] = m.Longitude,
                        ["timestamp"] = m.Timestamp,
                        
                        // Ocean Sensor Data
                        ["sea_surface_temperature_celsius"] = m.SeaSurfaceTemperatureCelsius,
                        ["wind_speed_ms"] = m.WindSpeedMs,
                        ["wind_direction_degrees"] = m.WindDirectionDegrees,
                        ["sea_level_meters"] = m.SeaLevelMeters,
                        ["current_speed_ms"] = m.CurrentSpeedMs,
                        ["current_direction_degrees"] = m.CurrentDirectionDegrees,
                        ["salinity_psu"] = m.SalinityPsu,
                        ["chlorophyll_mg_m3"] = m.ChlorophyllMgM3,
                        ["wave_height_meters"] = m.WaveHeightMeters,
                        ["wave_direction_degrees"] = m.WaveDirectionDegrees,
                        ["acoustic_level_db"] = m.AcousticLevelDb,
                        ["turbidity_ntu"] = m.TurbidityNtu,
                        ["precipitation_rate_mm_h"] = m.PrecipitationRateMmH,
                        ["surface_pressure_hpa"] = m.SurfacePressureHpa,
                        ["temperature_gradient_c_km"] = m.TemperatureGradientCKm,
                        
                        ["sensors"] = new BsonArray(new[]
                        {
                            new BsonDocument { ["type"] = "sea_surface_temperature", ["value"] = m.SeaSurfaceTemperatureCelsius, ["unit"] = "celsius" },
                            new BsonDocument { ["type"] = "wind_speed", ["value"] = m.WindSpeedMs, ["unit"] = "m/s" },
                            new BsonDocument { ["type"] = "wind_direction", ["value"] = m.WindDirectionDegrees, ["unit"] = "degrees" },
                            new BsonDocument { ["type"] = "sea_level", ["value"] = m.SeaLevelMeters, ["unit"] = "meters" },
                            new BsonDocument { ["type"] = "current_speed", ["value"] = m.CurrentSpeedMs, ["unit"] = "m/s" },
                            new BsonDocument { ["type"] = "current_direction", ["value"] = m.CurrentDirectionDegrees, ["unit"] = "degrees" },
                            new BsonDocument { ["type"] = "salinity", ["value"] = m.SalinityPsu, ["unit"] = "psu" },
                            new BsonDocument { ["type"] = "chlorophyll", ["value"] = m.ChlorophyllMgM3, ["unit"] = "mg/m3" },
                            new BsonDocument { ["type"] = "wave_height", ["value"] = m.WaveHeightMeters, ["unit"] = "meters" },
                            new BsonDocument { ["type"] = "wave_direction", ["value"] = m.WaveDirectionDegrees, ["unit"] = "degrees" },
                            new BsonDocument { ["type"] = "acoustic_level", ["value"] = m.AcousticLevelDb, ["unit"] = "db" },
                            new BsonDocument { ["type"] = "turbidity", ["value"] = m.TurbidityNtu, ["unit"] = "ntu" },
                            new BsonDocument { ["type"] = "precipitation_rate", ["value"] = m.PrecipitationRateMmH, ["unit"] = "mm/h" },
                            new BsonDocument { ["type"] = "surface_pressure", ["value"] = m.SurfacePressureHpa, ["unit"] = "hpa" },
                            new BsonDocument { ["type"] = "temperature_gradient", ["value"] = m.TemperatureGradientCKm, ["unit"] = "c/km" }
                        })
                    }))
                };

                await _aggregatedDataCollection.InsertOneAsync(document);
                Console.WriteLine($"[MongoDB] Aggregated data from {data.AgregadorId} with {data.Messages.Count} messages inserted successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MongoDB] Error inserting aggregated data: {ex.Message}");
                // Don't throw to avoid breaking the main flow
            }
        }

        public async Task LogSystemEventAsync(string component, string eventType, string description, string? additionalData = null)
        {
            try
            {                var document = new BsonDocument
                {
                    ["component"] = component,
                    ["event_type"] = eventType,
                    ["description"] = description,
                    ["timestamp"] = DateTime.UtcNow,
                    ["additional_data"] = additionalData != null ? (BsonValue)additionalData : BsonNull.Value
                };

                await _systemLogsCollection.InsertOneAsync(document);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MongoDB] Error logging system event: {ex.Message}");
            }
        }

        public async Task<List<BsonDocument>> GetRecentWavyMessagesAsync(int limit = 100)
        {
            try
            {
                var filter = Builders<BsonDocument>.Filter.Empty;
                var sort = Builders<BsonDocument>.Sort.Descending("received_at");

                var cursor = await _wavyMessagesCollection.FindAsync(filter, new FindOptions<BsonDocument>
                {
                    Sort = sort,
                    Limit = limit
                });

                return await cursor.ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MongoDB] Error retrieving recent messages: {ex.Message}");
                return new List<BsonDocument>();
            }
        }

        public async Task<Dictionary<string, object>> GetStatisticsAsync()
        {
            try
            {
                var stats = new Dictionary<string, object>();

                // Count total messages
                stats["total_wavy_messages"] = await _wavyMessagesCollection.CountDocumentsAsync(Builders<BsonDocument>.Filter.Empty);
                stats["total_aggregated_batches"] = await _aggregatedDataCollection.CountDocumentsAsync(Builders<BsonDocument>.Filter.Empty);

                // Get unique wavy nodes
                var wavyIds = await _wavyMessagesCollection.DistinctAsync<string>("wavy_id", Builders<BsonDocument>.Filter.Empty);
                stats["unique_wavy_nodes"] = (await wavyIds.ToListAsync()).Count;

                // Get unique aggregators
                var agrIds = await _wavyMessagesCollection.DistinctAsync<string>("agregador_id", Builders<BsonDocument>.Filter.Empty);
                stats["unique_aggregators"] = (await agrIds.ToListAsync()).Count;

                return stats;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MongoDB] Error getting statistics: {ex.Message}");
                return new Dictionary<string, object>();
            }
        }
    }
}
