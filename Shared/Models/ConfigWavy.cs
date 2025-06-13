using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Shared.Models
{
    public class ConfigWavy
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }        [BsonElement("WAVY_ID")]
        public string WavyId { get; set; } = string.Empty;

        [BsonElement("status")]
        public int Status { get; set; }

        [BsonElement("last_sync")]
        public DateTime LastSync { get; set; }

        [BsonElement("data_interval")]
        public int DataInterval { get; set; } = 5000; // Default 5 seconds

        [BsonElement("is_active")]
        public bool IsActive { get; set; } = true;

        [BsonElement("latitude")]
        public double Latitude { get; set; }

        [BsonElement("longitude")]
        public double Longitude { get; set; }        [BsonElement("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("ocean")]
        public string Ocean { get; set; } = string.Empty;

        [BsonElement("area_type")]
        public string AreaType { get; set; } = string.Empty;

        [BsonElement("region_coverage")]
        public string RegionCoverage { get; set; } = string.Empty;
    }
}
