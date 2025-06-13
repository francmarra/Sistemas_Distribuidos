using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Shared.Models;
using Shared.RabbitMQ;
using Shared.MongoDB;

class Program
{
    static RabbitMQPublisher? publisher;
    static ConfigService? configService;    
    static string wavyID = "";
    static double latitude = 0;
    static double longitude = 0;
    static volatile bool encerrarExecucao = false;

    static async Task Main(string[] args)
    {
        // Initialize configuration service
        configService = new ConfigService();

        // Check if Wavy ID was provided as command line argument
        if (args.Length > 0)
        {
            wavyID = args[0].Trim();
        }

        ConfigWavy? wavyConfig = null; 

        // Loop até que um ID válido seja fornecido
        while (true)
        {
            // If no command line argument provided, ask for input
            if (string.IsNullOrEmpty(wavyID))
            {
                Console.Write("ID da Wavy: ");
                wavyID = Console.ReadLine()?.Trim() ?? "";
            }

            if (string.IsNullOrEmpty(wavyID))
            {
                Console.WriteLine("ID inválido. O ID não pode ser vazio.");
                wavyID = ""; // Reset to ask again
                continue;
            }

            // Load wavy configuration from MongoDB
            wavyConfig = await configService.GetWavyConfigAsync(wavyID);
            if (wavyConfig == null)
            {
                Console.WriteLine($"Wavy {wavyID} não encontrada na configuração do MongoDB ou ID inválido.");
                Console.WriteLine("💡 Verifique o ID e se o ConfigImporter foi executado.");
                wavyID = ""; // Reset to ask again
                continue;            }
            
            // Assign loaded configuration to static fields
            latitude = wavyConfig.Latitude;
            longitude = wavyConfig.Longitude;
            Console.WriteLine($"[{wavyID}] Configuração carregada do MongoDB: Lat: {latitude:F4}, Lon: {longitude:F4}");

            // Status check and update
            if (wavyConfig.Status == 0) 
            {
                Console.Write($"{wavyID} Offline! Deseja voltar a ligá-la? (y/n) [y]: ");
                string input = (Console.ReadLine() ?? "").Trim().ToLower();
                if (string.IsNullOrEmpty(input) || input == "y")
                {
                    // Update status in MongoDB
                    await configService.UpdateWavyStatusAsync(wavyID, 1, DateTime.UtcNow);
                    Console.WriteLine($"{wavyID} foi atualizado para Online no MongoDB.");
                }
                else
                {
                    wavyID = ""; 
                    continue; 
                }
            }
            break; 
        }

        Console.WriteLine($"[{wavyID}] Inicializando RabbitMQ Publisher...");

        try
        {
            publisher = new RabbitMQPublisher();
            Console.WriteLine($"[{wavyID}] RabbitMQ configurado com sucesso.");
            Console.WriteLine($"[{wavyID}] Publicando dados para o tópico de dados oceânicos...");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{wavyID}] Erro ao configurar RabbitMQ: {ex.Message}");
            return;        }

        // Inicia o envio de dados
        var dataTask = Task.Run(async () => await PublicarDadosPeriodicamente());

        // Monitorar comando de desligamento
        var shutdownTask = Task.Run(async () => await MonitorarComandoDesligar());

        // Aguarda até que uma das tarefas complete
        await Task.WhenAny(dataTask, shutdownTask);

        Console.WriteLine($"[{wavyID}] Encerrando execução...");
        publisher?.Dispose();
        Console.WriteLine($"[{wavyID}] RabbitMQ resources cleaned up.");
    }    static async Task PublicarDadosPeriodicamente()
    {
        var rnd = new Random();

        while (!encerrarExecucao && publisher != null)
        {            
            try
            {
                // Generate comprehensive ocean sensor data
                var wavyMessage = GenerateOceanSensorData(rnd);
                
                // Set metadata fields
                wavyMessage.WavyId = wavyID;
                wavyMessage.Latitude = latitude;
                wavyMessage.Longitude = longitude;
                wavyMessage.Timestamp = DateTime.UtcNow.ToString("o");

                // Get Wavy configuration for ocean and area type
                var wavyConfig = await configService!.GetWavyConfigAsync(wavyID);
                if (wavyConfig != null)
                {
                    // Create routing key based on ocean and area type for targeted aggregator matching
                    string routingKey = $"ocean.data.{wavyConfig.Ocean.ToLower()}.{wavyConfig.AreaType.ToLower().Replace("-", "_")}";
                    
                    Console.WriteLine($"[{wavyID}] Publishing to routing key: {routingKey}");
                    publisher.PublishMessage("ocean_data_exchange", routingKey, JsonSerializer.Serialize(wavyMessage));
                    
                    Console.WriteLine($"[{wavyID}] Dados oceânicos publicados: SST={wavyMessage.SeaSurfaceTemperatureCelsius:F1}°C, Ocean={wavyConfig.Ocean}, AreaType={wavyConfig.AreaType}, Lat={latitude:F4}, Lon={longitude:F4}");
                }
                else
                {
                    // Fallback to generic routing if config not found
                    string routingKey = $"ocean.data.{wavyID}";
                    publisher.PublishMessage("ocean_data_exchange", routingKey, JsonSerializer.Serialize(wavyMessage));
                    Console.WriteLine($"[{wavyID}] Dados oceânicos publicados (generic routing): SST={wavyMessage.SeaSurfaceTemperatureCelsius:F1}°C, Lat={latitude:F4}, Lon={longitude:F4}");
                }
                
                // Update LastSync status in MongoDB
                await configService!.UpdateWavyStatusAsync(wavyID, 1, DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{wavyID}] Erro ao publicar dados: {ex.Message}");
            }

            await Task.Delay(1000);
        }
    }

    static async Task MonitorarComandoDesligar()
    {
        while (!encerrarExecucao)
        {
            var comando = Console.ReadLine();
            if (comando != null && comando.Trim().Equals("DLG", StringComparison.OrdinalIgnoreCase))
            {                Console.WriteLine($"[{wavyID}] Terminando execução...");
                
                // Update Wavy status to Offline (0) in MongoDB upon clean shutdown
                await configService!.UpdateWavyStatusAsync(wavyID, 0, DateTime.UtcNow);
                Console.WriteLine($"[{wavyID}] Status atualizado para Offline no MongoDB.");

                encerrarExecucao = true;
                break;
            }
        }
    }

    /// <summary>
    /// Generates comprehensive ocean sensor data simulating real-world marine monitoring buoy readings.
    /// This method creates realistic data for 12 different types of ocean sensors commonly used in
    /// oceanographic research and marine monitoring systems.
    /// </summary>
    /// <param name="rnd">Random number generator for data variation</param>
    /// <returns>WavyMessage containing comprehensive ocean sensor data</returns>
    static WavyMessage GenerateOceanSensorData(Random rnd)
    {
        var message = new WavyMessage();
        
        // 1. Sea Surface Temperature (SST) - Realistic oceanic range
        // Tropical: 26-30°C, Temperate: 15-25°C, Polar: -2-10°C
        message.SeaSurfaceTemperatureCelsius = Math.Round(GetRandomInRange(rnd, -2.0, 30.0), 2);
        
        // 2. Wind Speed and Direction
        // Surface winds typically 0-40 m/s (0-144 km/h)
        message.WindSpeedMs = Math.Round(GetRandomInRange(rnd, 0.0, 40.0), 1);
        message.WindDirectionDegrees = Math.Round(GetRandomInRange(rnd, 0.0, 360.0), 0);
        
        // 3. Sea Level / Tide Height
        // Typical tidal range: -2m to +2m relative to mean sea level
        message.SeaLevelMeters = Math.Round(GetRandomInRange(rnd, -2.0, 2.0), 3);
        
        // 4. Ocean Surface Currents
        // Surface currents typically 0-2 m/s
        message.CurrentSpeedMs = Math.Round(GetRandomInRange(rnd, 0.0, 2.0), 2);
        message.CurrentDirectionDegrees = Math.Round(GetRandomInRange(rnd, 0.0, 360.0), 0);
        
        // 5. Salinity
        // Ocean salinity typically 32-37 PSU, with 35 PSU being average
        message.SalinityPsu = Math.Round(GetRandomInRange(rnd, 32.0, 37.0), 1);
        
        // 6. Chlorophyll Concentration
        // Open ocean: 0.1-1 mg/m³, Coastal/upwelling: 1-10 mg/m³
        message.ChlorophyllMgM3 = Math.Round(GetRandomInRange(rnd, 0.1, 10.0), 2);
        
        // 7. Wave Height and Direction
        // Significant wave height typically 0-15m in extreme conditions
        message.WaveHeightMeters = Math.Round(GetRandomInRange(rnd, 0.1, 8.0), 2);
        message.WaveDirectionDegrees = Math.Round(GetRandomInRange(rnd, 0.0, 360.0), 0);
        
        // 8. Acoustic Activity
        // Underwater sound levels: 50-180 dB re 1 μPa
        message.AcousticLevelDb = Math.Round(GetRandomInRange(rnd, 50.0, 180.0), 1);
        
        // 9. Turbidity / Water Clarity
        // Clear ocean: 0.1-1 NTU, Coastal/turbid: 1-100+ NTU
        message.TurbidityNtu = Math.Round(GetRandomInRange(rnd, 0.1, 50.0), 1);
        
        // 10. Rainfall / Precipitation Rate
        // 0-100 mm/h (extreme rainfall can exceed this)
        message.PrecipitationRateMmH = Math.Round(GetRandomInRange(rnd, 0.0, 25.0), 1);
        
        // 11. Pressure at Sea Surface
        // Sea level pressure: 980-1040 hPa typically
        message.SurfacePressureHpa = Math.Round(GetRandomInRange(rnd, 980.0, 1040.0), 1);
          // 12. Temperature Gradient (horizontal surface)
        // Oceanic fronts can have gradients 0.1-5°C/km
        message.TemperatureGradientCKm = Math.Round(GetRandomInRange(rnd, 0.0, 5.0), 2);
        
        return message;
    }
    
    static double GetRandomInRange(Random rnd, double min, double max)
    {
        return min + (rnd.NextDouble() * (max - min));
    }
    
    // Removed local helper methods: IsWavyConfiguredAsync, GetWavyStatusAsync, UpdateWavyStatusAsync, UpdateWavyLastSyncAsync.
    // Configuration and status are now handled via ConfigService and direct MongoDB interactions.
}