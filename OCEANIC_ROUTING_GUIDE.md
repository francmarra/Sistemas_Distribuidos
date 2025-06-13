# Oceanic/Regional Routing Implementation

## Problem Solved
Previously, all Wavys connected to all Aggregators regardless of their oceanic regions, and Aggregators connected to any Server. This caused data from Indian Ocean Wavys to reach European Servers, violating the geographical organization principle.

## New Routing Logic

### 1. Wavy → Aggregator Connection
**Old behavior:** Wavys published to `ocean.data.*` and ALL aggregators received ALL data.

**New behavior:** Wavys now use oceanic routing keys based on their Ocean and AreaType:
```
Routing Key Pattern: ocean.data.{ocean}.{area_type}
Examples:
- Atlantic + Coastal → ocean.data.atlantic.coastal
- Pacific + Open → ocean.data.pacific.open
- Indian + Coastal-Open → ocean.data.indian.coastal_open
```

### 2. Aggregator Subscription
Aggregators now subscribe only to their matching oceanic regions:
```csharp
string oceanPattern = aggregatorConfig.Ocean.ToLower();
string areaPattern = aggregatorConfig.AreaType.ToLower().Replace("-", "_");
string routingPattern = $"ocean.data.{oceanPattern}.{areaPattern}";
```

### 3. Aggregator → Server Connection  
**Old behavior:** Used generic data exchange.

**New behavior:** Region-specific routing based on continent:
```
Routing Key Pattern: server.data.{continent_code}
Examples:
- EU-Agr01 → server.data.eu
- NA-Agr02 → server.data.na
- AS-Agr01 → server.data.as
```

### 4. Server Subscription
Servers now listen only to their continental data:
```csharp
string continentRoutingKey = $"server.data.{continentCode.ToLower()}";
subscriber.SubscribeToTopic("server_data_exchange", continentRoutingKey, OnRegionalDataReceived);
```

## Data Flow Example

### Atlantic Ocean Coastal Wavy (e.g., Wavy05)
1. **Wavy05** (Atlantic, Coastal) → Publishes to `ocean.data.atlantic.coastal`
2. **Aggregators** with Ocean="Atlantic" and AreaType="Coastal" receive the data:
   - NA-Agr01 (North America, Atlantic, Coastal)
   - EU-Agr01 (Europe, Atlantic, Coastal) 
3. **NA-Agr01** → Sends aggregated data to `server.data.na`
4. **EU-Agr01** → Sends aggregated data to `server.data.eu`
5. **NA-S Server** receives data via `server.data.na`
6. **EU-S Server** receives data via `server.data.eu`

### Pacific Ocean Open Water Wavy (e.g., Wavy09)
1. **Wavy09** (Pacific, Open) → Publishes to `ocean.data.pacific.open`
2. **Aggregators** with Ocean="Pacific" and AreaType="Open" receive the data:
   - NA-Agr04 (North America, Pacific, Open)
   - AS-Agr02 (Asia, Pacific, Open)
3. **NA-Agr04** → Sends to `server.data.na` → **NA-S Server**
4. **AS-Agr02** → Sends to `server.data.as` → **AS-S Server**

## Configuration Requirements

### Wavy Configuration (MongoDB: ConfigWavy)
Must have these fields populated:
```json
{
  "WAVY_ID": "Wavy01",
  "ocean": "Atlantic",
  "area_type": "Open",
  "latitude": 59.5,
  "longitude": -30.0
}
```

### Aggregator Configuration (MongoDB: ConfigAgr)
Must have these fields populated:
```json
{
  "AggregatorId": "NA-Agr01", 
  "Region": "North America",
  "Ocean": "Atlantic",
  "AreaType": "Coastal",
  "latitude": 41.525,
  "longitude": -70.672
}
```

## Benefits

1. **Geographical Accuracy**: Data flows follow real oceanic and continental boundaries
2. **Reduced Network Traffic**: Only relevant aggregators receive Wavy data  
3. **Improved Debugging**: Clear routing keys show data flow path
4. **Scalability**: Easy to add new oceanic regions or area types
5. **Fault Isolation**: Issues in one region don't affect others

## Testing the Solution

1. Start a Wavy from Atlantic Ocean → Should only reach Atlantic aggregators
2. Start a Wavy from Indian Ocean → Should only reach Indian Ocean aggregators  
3. Start aggregators from different continents → Should route to their respective servers
4. Check console logs for routing key verification

## Monitoring

Look for these log messages to verify correct routing:

**Wavy logs:**
```
[Wavy01] Publishing to routing key: ocean.data.atlantic.open
```

**Aggregator logs:**
```
🌊 [NA-Agr01] Routing: ocean.data.atlantic.coastal → Ocean: Atlantic, AreaType: Coastal
🌍 [NA-Agr01] Routing: North America (NA) → Server: NA-S
```

**Server logs:**
```
🌍 [NA-S] Routing verification: server.data.na → Region: North America (NA)
```
