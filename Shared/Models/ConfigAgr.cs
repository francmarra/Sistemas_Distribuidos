using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Shared.Models
{
    public class ConfigAgr
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }        [BsonElement("AggregatorId")]
        public string AgrId { get; set; } = string.Empty;        [BsonElement("Region")]
        public string Continent { get; set; } = string.Empty;        [BsonElement("continent_code")]
        public string ContinentCode { get; set; } = string.Empty;

        [BsonElement("server_id")]
        public string ServerId { get; set; } = "DefaultServer";

        [BsonElement("port")]
        public int Port { get; set; } = 5672;

        [BsonElement("queue_name")]
        public string QueueName { get; set; } = string.Empty;        [BsonElement("IsActive")]
        public bool IsActive { get; set; } = true;        [BsonElement("Latitude")]
        public double Latitude { get; set; } = 0.0;

        [BsonElement("Longitude")]
        public double Longitude { get; set; } = 0.0;

        [BsonElement("CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("UpdatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // New fields from aggregator subscription data        [BsonElement("Ocean")]
        public string Ocean { get; set; } = string.Empty;

        [BsonElement("AreaType")]
        public string AreaType { get; set; } = string.Empty;        [BsonElement("SubscribedDataTypes")]
        public List<string> SubscribedDataTypes { get; set; } = new List<string>();

        // Derived properties (computed from AggregatorId)
        [BsonIgnore]
        public string DerivedContinentCode => AgrId.Split('-')[0];
        
        [BsonIgnore] 
        public string DerivedQueueName => $"{AgrId}_queue";
        
        [BsonIgnore]
        public string DerivedServerId => $"{DerivedContinentCode}_Server";
    }
}
