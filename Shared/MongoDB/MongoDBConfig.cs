namespace Shared.MongoDB
{
    public static class MongoDBConfig
    {
        public const string CONNECTION_STRING = "mongodb+srv://sdmongo25:w7KPjneQrqV7aOdH@sistemasdistribuidos.tybz613.mongodb.net/";
        public const string DATABASE_NAME = "RabbitMQ-Communication";
        
        // Collection names
        public const string WAVY_MESSAGES_COLLECTION = "WavyMessages";
        public const string AGGREGATED_DATA_COLLECTION = "AggregatedData";
        public const string SYSTEM_LOGS_COLLECTION = "SystemLogs";
        public const string CONFIG_AGR_COLLECTION = "ConfigAgr";
        public const string CONFIG_WAVY_COLLECTION = "ConfigWavy";
        public const string CONFIG_SERVER_COLLECTION = "ConfigServer";
        
        // Readings collections (per continent for better scaling)
        public const string READINGS_COLLECTION = "readings";
        public const string AGGREGATED_COLLECTION = "aggregated";
    }
}
