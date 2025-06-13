using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Shared.Models
{
    public class ConfigServer
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("server_id")]
        public string ServerId { get; set; } = string.Empty;

        [BsonElement("continent")]
        public string Continent { get; set; } = string.Empty;

        [BsonElement("continent_code")]
        public string ContinentCode { get; set; } = string.Empty;

        [BsonElement("port")]
        public int Port { get; set; }

        [BsonElement("queue_name")]
        public string QueueName { get; set; } = string.Empty;

        [BsonElement("database_name")]
        public string DatabaseName { get; set; } = string.Empty;

        [BsonElement("is_active")]
        public bool IsActive { get; set; } = true;        [BsonElement("max_connections")]
        public int MaxConnections { get; set; } = 100;

        [BsonElement("latitude")]
        public double Latitude { get; set; }

        [BsonElement("longitude")]
        public double Longitude { get; set; }

        [BsonElement("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
