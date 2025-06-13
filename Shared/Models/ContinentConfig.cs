namespace Shared.Models
{
    public static class ContinentConfig
    {
        public static class Continents
        {
            public const string Europe = "Europe";
            public const string NorthAmerica = "North America";
            public const string SouthAmerica = "South America";
            public const string Africa = "Africa";
            public const string Asia = "Asia";
            public const string Oceania = "Oceania";
            public const string Antarctica = "Antarctica";
        }

        public static class ContinentCodes
        {
            public const string Europe = "EU";
            public const string NorthAmerica = "NA";
            public const string SouthAmerica = "SA";
            public const string Africa = "AF";
            public const string Asia = "AS";
            public const string Oceania = "OC";
            public const string Antarctica = "AQ";
        }

        public static readonly Dictionary<string, string> ContinentCodeToName = new()
        {
            { ContinentCodes.Europe, Continents.Europe },
            { ContinentCodes.NorthAmerica, Continents.NorthAmerica },
            { ContinentCodes.SouthAmerica, Continents.SouthAmerica },
            { ContinentCodes.Africa, Continents.Africa },
            { ContinentCodes.Asia, Continents.Asia },
            { ContinentCodes.Oceania, Continents.Oceania },
            { ContinentCodes.Antarctica, Continents.Antarctica }
        };

        public static readonly Dictionary<string, string> ContinentNameToCode = new()
        {
            { Continents.Europe, ContinentCodes.Europe },
            { Continents.NorthAmerica, ContinentCodes.NorthAmerica },
            { Continents.SouthAmerica, ContinentCodes.SouthAmerica },
            { Continents.Africa, ContinentCodes.Africa },
            { Continents.Asia, ContinentCodes.Asia },
            { Continents.Oceania, ContinentCodes.Oceania },
            { Continents.Antarctica, ContinentCodes.Antarctica }
        };

        public static readonly Dictionary<string, int> BasePortsByContinentCode = new()
        {
            { ContinentCodes.Europe, 11000 },
            { ContinentCodes.NorthAmerica, 12000 },
            { ContinentCodes.SouthAmerica, 13000 },
            { ContinentCodes.Africa, 14000 },
            { ContinentCodes.Asia, 15000 },
            { ContinentCodes.Oceania, 16000 },
            { ContinentCodes.Antarctica, 17000 }
        };

        public static string GetServerId(string continentCode) => $"{continentCode}-S";
        
        public static string GetAggregatorId(string continentCode, int number = 1) => $"{continentCode}-Agr{number:00}";
        
        public static string GetWavyId(string continentCode, int number) => $"{continentCode}-Wavy{number:00}";
        
        public static string GetQueueName(string continentCode, string componentType) => $"{continentCode.ToLower()}_{componentType}_queue";

        public static int GetServerPort(string continentCode) => BasePortsByContinentCode[continentCode];
        
        public static int GetAggregatorPort(string continentCode, int number = 1) => BasePortsByContinentCode[continentCode] + 100 + number;        public static bool IsValidContinentCode(string code) => ContinentCodeToName.ContainsKey(code);
        
        public static string GetContinentName(string code) => ContinentCodeToName.GetValueOrDefault(code, "Unknown");
          public static bool IsValidWavyId(string wavyId)
        {
            if (string.IsNullOrEmpty(wavyId) || !wavyId.Contains('-'))
                return false;
                
            var parts = wavyId.Split('-');
            if (parts.Length != 2)
                return false;
                
            var continentCode = parts[0];
            var wavyPart = parts[1];
            
            // Check if continent code is valid
            if (!IsValidContinentCode(continentCode))
                return false;
                
            // Check if wavy part follows pattern WavyXX (like Wavy01, Wavy02, etc.)
            if (!wavyPart.StartsWith("Wavy") || wavyPart.Length != 6)
                return false;
                
            // Check if the last two characters are digits
            var numberPart = wavyPart.Substring(4);
            return int.TryParse(numberPart, out _);
        }
        
        public static bool IsValidServerId(string serverId)
        {
            if (string.IsNullOrEmpty(serverId) || !serverId.Contains('-'))
                return false;
                
            var parts = serverId.Split('-');
            if (parts.Length != 2)
                return false;
                
            var continentCode = parts[0];
            var serverPart = parts[1];
            
            // Check if continent code is valid
            if (!IsValidContinentCode(continentCode))
                return false;
                
            // Check if server part is "S"
            return serverPart == "S";
        }
    }
}
