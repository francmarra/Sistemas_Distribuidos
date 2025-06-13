using System.Text.Json.Serialization;

namespace Shared.Models
{
    public class WavyMessage
    {        [JsonPropertyName("wavy_id")]
        public string WavyId { get; set; } = "";
        
        [JsonPropertyName("latitude")]
        public double Latitude { get; set; }
        
        [JsonPropertyName("longitude")]
        public double Longitude { get; set; }
        
        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; } = "";
          // Ocean Sensor Data - Comprehensive Marine Monitoring
        
        // 1. Sea Surface Temperature (SST)
        [JsonPropertyName("sea_surface_temperature_celsius")]
        public double SeaSurfaceTemperatureCelsius { get; set; }
        
        // 2. Wind Speed and Direction
        [JsonPropertyName("wind_speed_ms")]
        public double WindSpeedMs { get; set; }
        
        [JsonPropertyName("wind_direction_degrees")]
        public double WindDirectionDegrees { get; set; }
        
        // 3. Sea Level / Tide Height
        [JsonPropertyName("sea_level_meters")]
        public double SeaLevelMeters { get; set; }
        
        // 4. Ocean Surface Currents
        [JsonPropertyName("current_speed_ms")]
        public double CurrentSpeedMs { get; set; }
        
        [JsonPropertyName("current_direction_degrees")]
        public double CurrentDirectionDegrees { get; set; }
        
        // 5. Salinity
        [JsonPropertyName("salinity_psu")]
        public double SalinityPsu { get; set; }
        
        // 6. Chlorophyll Concentration
        [JsonPropertyName("chlorophyll_mg_m3")]
        public double ChlorophyllMgM3 { get; set; }
        
        // 7. Wave Height and Direction
        [JsonPropertyName("wave_height_meters")]
        public double WaveHeightMeters { get; set; }
        
        [JsonPropertyName("wave_direction_degrees")]
        public double WaveDirectionDegrees { get; set; }
        
        // 8. Acoustic Activity
        [JsonPropertyName("acoustic_level_db")]
        public double AcousticLevelDb { get; set; }
        
        // 9. Turbidity / Water Clarity
        [JsonPropertyName("turbidity_ntu")]
        public double TurbidityNtu { get; set; }
        
        // 10. Rainfall / Precipitation Rate
        [JsonPropertyName("precipitation_rate_mm_h")]
        public double PrecipitationRateMmH { get; set; }
        
        // 11. Pressure at Sea Surface
        [JsonPropertyName("surface_pressure_hpa")]
        public double SurfacePressureHpa { get; set; }
          // 12. Temperature Gradient (horizontal surface)
        [JsonPropertyName("temperature_gradient_c_km")]        public double TemperatureGradientCKm { get; set; }
    }
}
