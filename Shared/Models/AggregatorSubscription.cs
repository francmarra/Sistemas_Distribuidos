using System.Text.Json.Serialization;

namespace Shared.Models
{
    public class AggregatorSubscription
    {
        [JsonPropertyName("aggregator_id")]
        public string AggregatorId { get; set; } = "";
        
        [JsonPropertyName("region")]
        public string Region { get; set; } = "";
        
        [JsonPropertyName("ocean")]
        public string Ocean { get; set; } = "";
        
        [JsonPropertyName("area_type")]
        public string AreaType { get; set; } = "";
        
        [JsonPropertyName("subscribed_data_types")]
        public List<string> SubscribedDataTypes { get; set; } = new();
        
        [JsonPropertyName("is_active")]
        public bool IsActive { get; set; } = true;
        
        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        [JsonPropertyName("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
