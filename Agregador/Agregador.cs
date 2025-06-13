using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Tasks;
using Shared.Models;
using Shared.RabbitMQ;
using Shared.MongoDB;

class Program
{
    static readonly ConcurrentQueue<WavyMessage> dataQueue = new();
    static RabbitMQPublisher? publisher;
    static ConfigService? configService;
    static volatile bool encerrarExecucao = false;

    static string aggregatorID = "";
    static string continentCode = "";
    static string continentName = "";
    static string serverId = "";
    static string rpcQueueName = "";
    static ConfigAgr? aggregatorConfig;

    static async Task Main(string[] args)
    {
        // Support command line argument for aggregator ID
        if (args.Length > 0)
        {
            aggregatorID = args[0].Trim();
        }
        else
        {
            Console.Write("🌍 Aggregator ID (e.g., EU-Agr01, NA-Agr01): ");
            aggregatorID = Console.ReadLine()?.Trim() ?? "";
        }

        if (string.IsNullOrEmpty(aggregatorID))
        {
            Console.WriteLine("❌ ID cannot be empty.");
            return;
        }

        // Initialize configuration service
        configService = new ConfigService(); // Moved up

        // Validate aggregator ID format (continent-based)
        // and load configuration from MongoDB
        aggregatorConfig = await configService.GetAgrConfigAsync(aggregatorID);

        if (aggregatorConfig == null)
        {
            Console.WriteLine($"❌ Aggregator {aggregatorID} not found in MongoDB configuration or invalid format.");
            Console.WriteLine("💡 Ensure the aggregator ID is correct and ConfigImporter has been run.");
            return;
        }        // Extract continent code from aggregator ID
        continentCode = aggregatorConfig.DerivedContinentCode;
        continentName = aggregatorConfig.Continent; // Use region from config

        Console.WriteLine($"🚀 Starting {aggregatorID} for {continentName} ({continentCode})...");

        // Configuration is already loaded, assign values with defaults for missing fields
        serverId = !string.IsNullOrEmpty(aggregatorConfig.ServerId) ? aggregatorConfig.ServerId : aggregatorConfig.DerivedServerId;
        rpcQueueName = !string.IsNullOrEmpty(aggregatorConfig.QueueName) ? aggregatorConfig.QueueName : aggregatorConfig.DerivedQueueName;        Console.WriteLine($"📡 Configuration loaded from MongoDB:");
        Console.WriteLine($"   • Continent: {aggregatorConfig.Continent} ({continentCode})");
        Console.WriteLine($"   • Ocean: {aggregatorConfig.Ocean}");
        Console.WriteLine($"   • Area Type: {aggregatorConfig.AreaType}");
        Console.WriteLine($"   • Location: {aggregatorConfig.Latitude:F2}°, {aggregatorConfig.Longitude:F2}°");
        Console.WriteLine($"   • Server: {serverId}");
        Console.WriteLine($"   • Port: {aggregatorConfig.Port}");
        Console.WriteLine($"   • Queue: {rpcQueueName}");
        Console.WriteLine($"   • Subscribed Data Types: {string.Join(", ", aggregatorConfig.SubscribedDataTypes)}");Console.WriteLine($"🔧 Initializing RabbitMQ components...");        try
        {
            // Initialize RabbitMQ Publisher for sending data to Server
            publisher = new RabbitMQPublisher();
            Console.WriteLine($"✅ RabbitMQ Publisher configured successfully.");            // Subscribe to ocean data exchange with ocean and area type specific routing
            var subscriber = new RabbitMQSubscriber($"{aggregatorID}_ocean_queue");
            
            // Create routing key pattern based on aggregator's ocean and area type
            string oceanPattern = aggregatorConfig.Ocean.ToLower();
            string areaPattern = aggregatorConfig.AreaType.ToLower().Replace("-", "_");
            string routingPattern = $"ocean.data.{oceanPattern}.{areaPattern}";
            
            subscriber.SubscribeToTopic("ocean_data_exchange", routingPattern, HandleWavyData);
            Console.WriteLine($"✅ Subscribed to ocean data exchange with pattern: {routingPattern}");
            Console.WriteLine($"   • Listening for data from Ocean: {aggregatorConfig.Ocean}, AreaType: {aggregatorConfig.AreaType}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error configuring RabbitMQ: {ex.Message}");
            return;
        }Console.WriteLine($"🎯 {aggregatorID} ready and waiting for Wavy data...\n");

        // Process data and send to Server every 10 seconds
        var dataTask = Task.Run(async () => await ProcessAndSendData());

        // Monitor shutdown command
        var shutdownTask = Task.Run(async () => await MonitorarComandoDesligar());

        // Wait until one of the tasks completes
        await Task.WhenAny(dataTask, shutdownTask);

        Console.WriteLine($"🔄 {aggregatorID} shutting down...");
        
        // Cleanup resources
        publisher?.Dispose();
        Console.WriteLine($"✅ {aggregatorID} RabbitMQ resources cleaned up.");
    }    static void HandleWavyData(string routingKey, string message)
    {
        try
        {
            var wavyMessage = JsonSerializer.Deserialize<WavyMessage>(message);
            if (wavyMessage != null)
            {
                // Log the data reception with geographic verification
                Console.WriteLine($"📊 [{aggregatorID}] Received data from {wavyMessage.WavyId}: " +
                    $"SST={wavyMessage.SeaSurfaceTemperatureCelsius:F1}°C, " +
                    $"Lat={wavyMessage.Latitude:F4}, Lon={wavyMessage.Longitude:F4}");
                
                Console.WriteLine($"🌊 [{aggregatorID}] Routing: {routingKey} → Ocean: {aggregatorConfig?.Ocean}, AreaType: {aggregatorConfig?.AreaType}");
                
                // Add to processing queue
                dataQueue.Enqueue(wavyMessage);
                
                // Log subscription match (for debugging)
                if (aggregatorConfig != null && aggregatorConfig.SubscribedDataTypes.Any())
                {
                    Console.WriteLine($"🎯 [{aggregatorID}] Processing for: {string.Join(", ", aggregatorConfig.SubscribedDataTypes)}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [{aggregatorID}] Error processing Wavy data: {ex.Message}");
        }
    }

    static async Task ProcessAndSendData()
    {
        while (!encerrarExecucao)
        {
            await Task.Delay(5000);

            if (!dataQueue.IsEmpty)
            {
                var messages = new List<WavyMessage>();

                // Collect all pending messages
                while (dataQueue.TryDequeue(out var message))
                {
                    messages.Add(message);
                }

                if (messages.Count > 0)
                {
                    // Create aggregated data
                    var aggregatedData = new AggregatedData
                    {
                        AgregadorId = aggregatorID,
                        Messages = messages,
                        Timestamp = DateTime.Now.ToString("o")
                    };

                    // Send to server via RabbitMQ
                    SendDataToServer(aggregatedData);
                }
            }
        }
    }    static void SendDataToServer(AggregatedData aggregatedData)
    {
        if (publisher == null)
        {
            Console.WriteLine($"[{aggregatorID}] RabbitMQ Publisher não está disponível.");
            return;
        }

        try
        {
            // Determine the correct server based on the aggregator's region/continent
            // This ensures data goes to the geographically appropriate server
            string serverRoutingKey = DetermineServerRoutingKey();
            
            Console.WriteLine($"[{aggregatorID}] Enviando {aggregatedData.Messages.Count} mensagens para Servidor {serverId}...");
            Console.WriteLine($"🌍 [{aggregatorID}] Routing: {continentName} ({continentCode}) → Server: {serverId}");
            
            // Use region-specific routing instead of generic routing
            publisher.PublishMessage("server_data_exchange", serverRoutingKey, JsonSerializer.Serialize(aggregatedData));
            Console.WriteLine($"[{aggregatorID}] Dados enviados com sucesso para o Servidor via routing key: {serverRoutingKey}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{aggregatorID}] Erro ao enviar dados para o Servidor: {ex.Message}");
        }
    }

    static string DetermineServerRoutingKey()
    {
        // Create routing key based on continent code to ensure data goes to correct regional server
        return $"server.data.{continentCode.ToLower()}";
    }

    static async Task MonitorarComandoDesligar()
    {
        while (!encerrarExecucao)
        {
            var comando = Console.ReadLine();
            if (comando != null && comando.Trim().Equals("DLG", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"[{aggregatorID}] Comando de desligamento recebido...");
                
                // Send shutdown notification to server
                if (publisher != null)
                {
                    try
                    {
                        publisher.PublishShutdown(aggregatorID);
                        Console.WriteLine($"[{aggregatorID}] Notificação de shutdown enviada ao Servidor.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[{aggregatorID}] Erro ao enviar notificação de shutdown: {ex.Message}");
                    }
                }

                encerrarExecucao = true;
                break;
            }

            await Task.Delay(100);
        }
    }    static bool IsValidAggregatorId(string id)
    {
        if (string.IsNullOrEmpty(id) || !id.Contains('-'))
            return false;

        var parts = id.Split('-');
        if (parts.Length != 2)
            return false;

        var continentCode = parts[0];
        var agrPart = parts[1];

        // Validate continent code
        if (!ContinentConfig.IsValidContinentCode(continentCode))
            return false;

        // Validate aggregator part (should be Agr followed by number)
        if (!agrPart.StartsWith("Agr") || agrPart.Length < 4)
            return false;

        // Check if the number part is valid
        var numberPart = agrPart.Substring(3);
        return int.TryParse(numberPart, out _);
    }
}