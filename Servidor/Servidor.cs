using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using Shared.Models;
using Shared.RabbitMQ;
using Shared.MongoDB;

class Program
{
    static RabbitMQSubscriber? subscriber;
    static MongoDBService? mongoService;
    static ConfigService? configService;
    static string serverId = "";
    static string continentCode = "";
    static string continentName = "";
    static volatile bool encerrarExecucao = false;    static async Task Main()
    {
        // Initialize configuration service
        configService = new ConfigService();

        // Get server ID from user
        while (true)
        {
            Console.Write("ID do Servidor: ");
            serverId = Console.ReadLine()?.Trim() ?? "";
            if (string.IsNullOrEmpty(serverId))
            {
                 Console.WriteLine("ID inválido. O ID não pode ser vazio.");
                 continue;
            }

            // Load server configuration from MongoDB
            var serverConfig = await configService.GetServerConfigAsync(serverId);
            if (serverConfig == null)
            {
                Console.WriteLine($"Servidor {serverId} não encontrado na configuração do MongoDB ou ID inválido.");
                Console.WriteLine("Continentes suportados podem ser inferidos dos IDs de servidor configurados (ex: EU-S, NA-S).");
                Console.WriteLine("💡 Verifique o ID e se o ConfigImporter foi executado.");
                continue;
            }
            
            continentCode = serverConfig.ContinentCode;
            continentName = serverConfig.Continent;
            Console.WriteLine($"[{serverId}] Configuração carregada do MongoDB: {continentName} ({continentCode})");
            break;
        }

        // Initialize MongoDB
        Console.WriteLine($"[{serverId}] Inicializando MongoDB...");
        try
        {
            mongoService = new MongoDBService();
            var connectionTest = await mongoService.TestConnectionAsync();
            if (!connectionTest)
            {
                Console.WriteLine($"[{serverId}] Falha na conexão com MongoDB. Continuando apenas com arquivos locais.");
                mongoService = null;
            }
            else
            {
                Console.WriteLine($"[{serverId}] MongoDB conectado com sucesso!");
                await mongoService.LogSystemEventAsync(serverId, "STARTUP", $"Servidor {serverId} iniciado com sucesso");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{serverId}] Erro ao conectar com MongoDB: {ex.Message}. Continuando apenas com arquivos locais.");
            mongoService = null;
        }
          // Setup continent-specific server queue
        string serverQueue = ContinentConfig.GetQueueName(continentCode, "server");
        Console.WriteLine($"[{serverId}] Inicializando RabbitMQ Subscriber...");

        try
        {
            subscriber = new RabbitMQSubscriber(serverQueue);
            
            // Subscribe to continent-specific data routing instead of generic data
            string continentRoutingKey = $"server.data.{continentCode.ToLower()}";
            subscriber.SubscribeToTopic("server_data_exchange", continentRoutingKey, OnRegionalDataReceived);
            
            // Subscribe to shutdown messages
            subscriber.SubscribeToShutdown(OnShutdownReceived);
            
            Console.WriteLine($"✅ RabbitMQ configurado com sucesso.");
            Console.WriteLine($"   • Queue: {serverQueue}");
            Console.WriteLine($"   • Region: {continentName} ({continentCode})");
            Console.WriteLine($"   • Routing Pattern: {continentRoutingKey}");
            Console.WriteLine($"[{serverId}] A ouvir mensagens de dados regionais e shutdown via RabbitMQ...\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{serverId}] Erro ao configurar RabbitMQ: {ex.Message}");
            return;
        }        // Monitorar comando de desligamento do console
        _ = Task.Run(() => MonitorarComandoDesligar());

        // Mantém a aplicação em execução
        while (!encerrarExecucao)
        {
            await Task.Delay(200);
        }        Console.WriteLine($"[{serverId}] Encerrando execução...");
        
        // Log shutdown event
        if (mongoService != null)
        {
            try
            {
                await mongoService.LogSystemEventAsync(serverId, "SHUTDOWN", $"Servidor {serverId} encerrando");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{serverId}] Erro ao logar shutdown no MongoDB: {ex.Message}");
            }
        }
        
        subscriber?.Dispose();
        Console.WriteLine($"[{serverId}] RabbitMQ resources cleaned up.");
    }    static async void OnRegionalDataReceived(string routingKey, string message)
    {
        try
        {
            var aggregatedData = JsonSerializer.Deserialize<AggregatedData>(message);
            if (aggregatedData == null) return;

            Console.WriteLine($"[{serverId}] Dados regionais recebidos de [{aggregatedData.AgregadorId}] via {routingKey} - {aggregatedData.Messages.Count} mensagens");
            Console.WriteLine($"🌍 [{serverId}] Routing verification: {routingKey} → Region: {continentName} ({continentCode})");

            // Save to MongoDB if available
            if (mongoService != null)
            {
                try
                {
                    await mongoService.InsertAggregatedDataAsync(aggregatedData);
                    
                    // Also save individual wavy messages
                    foreach (var wavyMessage in aggregatedData.Messages)
                    {
                        await mongoService.InsertWavyMessageAsync(wavyMessage);
                    }
                    
                    Console.WriteLine($"[{serverId}] Dados salvos no MongoDB com sucesso");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{serverId}] Erro ao salvar no MongoDB: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine($"[{serverId}] MongoDB não disponível - dados não foram salvos");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{serverId}] Erro ao processar dados regionais: {ex.Message}");
        }
    }

    static async void OnDataReceived(string message)
    {
        try
        {
            var aggregatedData = JsonSerializer.Deserialize<AggregatedData>(message);
            if (aggregatedData == null) return;            Console.WriteLine($"[{serverId}] Dados recebidos de [{aggregatedData.AgregadorId}] - {aggregatedData.Messages.Count} mensagens");

            // Save to MongoDB if available
            if (mongoService != null)
            {
                try
                {
                    await mongoService.InsertAggregatedDataAsync(aggregatedData);
                    
                    // Also save individual wavy messages
                    foreach (var wavyMessage in aggregatedData.Messages)
                    {
                        await mongoService.InsertWavyMessageAsync(wavyMessage);
                    }
                    
                    Console.WriteLine($"[{serverId}] Dados salvos no MongoDB com sucesso");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{serverId}] Erro ao salvar no MongoDB: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine($"[{serverId}] MongoDB não disponível - dados não foram salvos");
            }        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{serverId}] Erro ao processar dados recebidos: {ex.Message}");
        }
    }

    static void OnShutdownReceived(string message)
    {
        try
        {
            using var jsonDoc = JsonDocument.Parse(message);
            var aggregatorId = jsonDoc.RootElement.GetProperty("AggregatorId").GetString();
            var timestamp = jsonDoc.RootElement.GetProperty("Timestamp").GetString();
            
            Console.WriteLine($"[{serverId}] Notificação de shutdown recebida de {aggregatorId} em {timestamp}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{serverId}] Erro ao processar shutdown: {ex.Message}");
        }
    }

    static void MonitorarComandoDesligar()
    {
        while (!encerrarExecucao)
        {
            var comando = Console.ReadLine();
            if (comando != null && comando.Trim().Equals("DLG", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"[{serverId}] Comando de desligamento recebido...");
                encerrarExecucao = true;
                break;
            }
        }
    }
}