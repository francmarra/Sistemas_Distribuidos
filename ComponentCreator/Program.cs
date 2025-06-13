using System.Text.Json;
using Shared.Models;
using Shared.MongoDB;

namespace ComponentCreator
{
    class Program
    {        static async Task<int> Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.WriteLine("Usage: ComponentCreator.exe <type> <json_data>");
                Console.WriteLine("Types: aggregator, wavy");
                return 1;
            }

            string componentType = args[0].ToLower();
            string jsonData = args[1];

            try
            {
                var configService = new ConfigService();

                switch (componentType)
                {
                    case "aggregator":
                        await CreateAggregator(configService, jsonData);
                        break;
                    case "wavy":
                        await CreateWavy(configService, jsonData);
                        break;
                    default:
                        Console.WriteLine($"Unknown component type: {componentType}");
                        return 1;
                }

                Console.WriteLine($"SUCCESS: {componentType} created in MongoDB");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex.Message}");
                return 1;
            }
        }        static async Task CreateAggregator(ConfigService configService, string jsonData)
        {
            var formData = JsonSerializer.Deserialize<AggregatorFormData>(jsonData);
            if (formData == null)
            {
                throw new ArgumentException("Invalid JSON data for aggregator");
            }

            // Map region to continent code
            string continentCode = MapRegionToContinentCode(formData.region);

            // Generate automatic ID if not provided or empty
            string aggregatorId = formData.id;
            if (string.IsNullOrEmpty(aggregatorId))
            {
                aggregatorId = await GenerateNextAggregatorId(configService, continentCode);
            }

            var configAgr = new ConfigAgr
            {
                AgrId = aggregatorId,
                Continent = formData.region,
                ContinentCode = continentCode,
                ServerId = $"{continentCode}-S",
                QueueName = $"{aggregatorId}_queue",
                IsActive = true,
                Latitude = formData.latitude,
                Longitude = formData.longitude,
                Ocean = formData.ocean,
                AreaType = formData.areaType,
                SubscribedDataTypes = formData.dataTypes,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await configService.InsertAgrConfigAsync(configAgr);
            
            // Output the generated ID for the frontend
            Console.WriteLine($"GENERATED_ID:{aggregatorId}");
        }        static async Task CreateWavy(ConfigService configService, string jsonData)
        {
            var formData = JsonSerializer.Deserialize<WavyFormData>(jsonData);
            if (formData == null)
            {
                throw new ArgumentException("Invalid JSON data for wavy");
            }

            // Generate automatic ID if not provided or empty
            string wavyId = formData.id;
            if (string.IsNullOrEmpty(wavyId))
            {
                wavyId = await GenerateNextWavyId(configService);
            }

            var configWavy = new ConfigWavy
            {
                WavyId = wavyId,
                Status = formData.status,
                LastSync = DateTime.UtcNow,
                DataInterval = formData.dataInterval,
                IsActive = formData.status == 1,
                Latitude = formData.latitude,
                Longitude = formData.longitude,
                Ocean = formData.ocean,
                AreaType = formData.areaType,
                RegionCoverage = formData.regionCoverage,
                CreatedAt = DateTime.UtcNow
            };

            await configService.InsertWavyConfigAsync(configWavy);
            
            // Output the generated ID for the frontend
            Console.WriteLine($"GENERATED_ID:{wavyId}");        }

        static async Task<string> GenerateNextAggregatorId(ConfigService configService, string continentCode)
        {
            // Get all existing aggregator configurations for the continent
            var allConfigs = await configService.GetAllAgrConfigsAsync();
            var continentConfigs = allConfigs.Where(c => c.ContinentCode == continentCode).ToList();
            
            // Find the highest number for this continent
            int maxNumber = 0;
            string prefix = $"{continentCode}-Agr";
            
            foreach (var config in continentConfigs)
            {
                if (config.AgrId.StartsWith(prefix))
                {
                    string numberPart = config.AgrId.Substring(prefix.Length);
                    if (int.TryParse(numberPart, out int number))
                    {
                        maxNumber = Math.Max(maxNumber, number);
                    }
                }
            }
            
            // Generate next ID
            int nextNumber = maxNumber + 1;
            return $"{prefix}{nextNumber:D2}"; // Format with 2 digits (01, 02, etc.)
        }

        static async Task<string> GenerateNextWavyId(ConfigService configService)
        {
            // Get all existing wavy configurations
            var allConfigs = await configService.GetAllWavyConfigsAsync();
            
            // Find the highest number
            int maxNumber = 0;
            string prefix = "Wavy";
            
            foreach (var config in allConfigs)
            {
                if (config.WavyId.StartsWith(prefix))
                {
                    string numberPart = config.WavyId.Substring(prefix.Length);
                    if (int.TryParse(numberPart, out int number))
                    {
                        maxNumber = Math.Max(maxNumber, number);
                    }
                }
            }
            
            // Generate next ID
            int nextNumber = maxNumber + 1;
            return $"{prefix}{nextNumber:D2}"; // Format with 2 digits (01, 02, etc.)
        }

        static string MapRegionToContinentCode(string region)
        {
            return region switch
            {
                "North America" => "NA",
                "South America" => "SA",
                "Europe" => "EU",
                "Africa" => "AF",
                "Asia" => "AS",
                "Oceania" => "OC",
                "Antarctica" => "AQ",
                _ => "XX" // Unknown
            };
        }
    }

    // Data models for JSON deserialization
    public class AggregatorFormData
    {
        public string id { get; set; } = "";
        public string region { get; set; } = "";
        public string ocean { get; set; } = "";
        public string areaType { get; set; } = "";
        public double latitude { get; set; }
        public double longitude { get; set; }
        public List<string> dataTypes { get; set; } = new();
    }

    public class WavyFormData
    {
        public string id { get; set; } = "";
        public double latitude { get; set; }
        public double longitude { get; set; }
        public string ocean { get; set; } = "";
        public string areaType { get; set; } = "";
        public string regionCoverage { get; set; } = "";
        public int dataInterval { get; set; }
        public int status { get; set; }
    }
}
