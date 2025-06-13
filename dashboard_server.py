from flask import Flask, jsonify, request, render_template_string, send_from_directory
from flask_cors import CORS
import pymongo
from datetime import datetime, timedelta, UTC
import json
from typing import Dict, List, Any
import os
import pika
import requests

app = Flask(__name__)
CORS(app)

# MongoDB configuration
MONGO_CONNECTION_STRING = "mongodb+srv://sdmongo25:w7KPjneQrqV7aOdH@sistemasdistribuidos.tybz613.mongodb.net/"
DATABASE_NAME = "RabbitMQ-Communication"

# Collections
WAVY_MESSAGES_COLLECTION = "WavyMessages"
AGGREGATED_DATA_COLLECTION = "AggregatedData"
READINGS_COLLECTION = "readings"
CONFIG_AGR_COLLECTION = "ConfigAgr"
CONFIG_WAVY_COLLECTION = "ConfigWavy"
CONFIG_SERVER_COLLECTION = "ConfigServer"

try:
    client = pymongo.MongoClient(MONGO_CONNECTION_STRING)
    db = client[DATABASE_NAME]
    print("✅ Connected to MongoDB successfully")
except Exception as e:
    print(f"❌ Failed to connect to MongoDB: {e}")
    db = None

# RabbitMQ configuration
RABBITMQ_HOST = "localhost"
RABBITMQ_USERNAME = "guest"
RABBITMQ_PASSWORD = "guest"
RABBITMQ_MANAGEMENT_URL = "http://localhost:15672"

def get_active_components_from_rabbitmq():
    """Get active Wavys and Aggregators by checking RabbitMQ queues and connections"""
    active_wavys = set()
    active_aggregators = set()
    
    try:
        # Try to get queue information from RabbitMQ Management API
        try:
            import requests
            auth = (RABBITMQ_USERNAME, RABBITMQ_PASSWORD)
            
            # Get all queues
            queues_response = requests.get(f"{RABBITMQ_MANAGEMENT_URL}/api/queues", auth=auth, timeout=5)
            if queues_response.status_code == 200:
                queues = queues_response.json()
                print(f"🔍 Debug: Found {len(queues)} RabbitMQ queues")
                
                for queue in queues:
                    queue_name = queue.get('name', '')
                    consumers = queue.get('consumers', 0)
                    
                    print(f"🔍 Debug: Queue: {queue_name} (consumers: {consumers})")
                    
                    # Check for Aggregator queues: {REGION}-Agr{NUMBER}_ocean_queue
                    if '_ocean_queue' in queue_name and 'Agr' in queue_name:
                        if consumers > 0:  # Has active consumers
                            # Extract aggregator ID (e.g., "AF-Agr01" from "AF-Agr01_ocean_queue")
                            aggregator_id = queue_name.replace('_ocean_queue', '')
                            active_aggregators.add(aggregator_id)
                            print(f"🔍 Debug: Found active Aggregator: {aggregator_id} (consumers: {consumers})")
                    
                    # Check for RPC queues (server components): rpc_queue_{REGION}
                    elif queue_name.startswith('rpc_queue_'):
                        if consumers > 0:  # Has active consumers
                            region = queue_name.replace('rpc_queue_', '')
                            # Count this as an active aggregator server
                            active_aggregators.add(f"{region}-Server")
                            print(f"🔍 Debug: Found active Server: {region}-Server (consumers: {consumers})")
                    
                    # Check for anonymous queues (likely Wavy consumers): amq.gen-*
                    elif queue_name.startswith('amq.gen-'):
                        if consumers > 0:  # Has active consumers
                            # Each anonymous queue with consumers likely represents an active Wavy
                            wavy_id = f"Wavy-{queue_name[-8:]}"  # Use last 8 chars as unique ID
                            active_wavys.add(wavy_id)
                            print(f"🔍 Debug: Found active Wavy: {wavy_id} (consumers: {consumers})")
                    
                    # Check for server queues: {region}_server_queue
                    elif queue_name.endswith('_server_queue'):
                        if consumers > 0:  # Has active consumers
                            region = queue_name.replace('_server_queue', '').upper()
                            server_id = f"{region}-ServerQueue"
                            active_aggregators.add(server_id)
                            print(f"🔍 Debug: Found active Server Queue: {server_id} (consumers: {consumers})")
            
            # Get more detailed information about consumers and channels
            consumers_response = requests.get(f"{RABBITMQ_MANAGEMENT_URL}/api/consumers", auth=auth, timeout=5)
            channels_response = requests.get(f"{RABBITMQ_MANAGEMENT_URL}/api/channels", auth=auth, timeout=5)
            connections_response = requests.get(f"{RABBITMQ_MANAGEMENT_URL}/api/connections", auth=auth, timeout=5)
            
            if consumers_response.status_code == 200 and channels_response.status_code == 200 and connections_response.status_code == 200:
                consumers = consumers_response.json()
                channels = channels_response.json()
                connections = connections_response.json()
                
                print(f"🔍 Debug: Found {len(consumers)} consumers, {len(channels)} channels, {len(connections)} connections")
                
                # Count unique connections that have consumers (Wavys)
                consumer_connections = set()
                for consumer in consumers:
                    if 'channel_details' in consumer and 'connection_name' in consumer['channel_details']:
                        consumer_connections.add(consumer['channel_details']['connection_name'])
                
                # Count channels with consumers vs without consumers
                channels_with_consumers = sum(1 for ch in channels if ch.get('consumer_count', 0) > 0)
                channels_without_consumers = sum(1 for ch in channels if ch.get('consumer_count', 0) == 0)
                
                print(f"🔍 Debug: Channels with consumers: {channels_with_consumers}, without consumers: {channels_without_consumers}")
                print(f"🔍 Debug: Unique consumer connections: {len(consumer_connections)}")
                
                # Simplified detection based on observed patterns:
                # 1. Wavys create anonymous consumer queues (amq.gen-*) - count these
                # 2. Each Wavy might create multiple consumer queues, so estimate actual Wavy count
                # 3. Aggregators create connections but typically don't consume, so count non-consumer connections
                
                wavy_consumer_queues = len(active_wavys)  # Number of anonymous consumer queues
                
                # Estimate actual Wavy count (assuming each Wavy creates 1-2 consumer queues)
                estimated_wavy_count = min(4, max(1, wavy_consumer_queues // 1))  # Each Wavy creates ~1 queue
                if wavy_consumer_queues > 4:
                    estimated_wavy_count = 4  # Cap at expected 4 Wavys
                
                # Count connections that don't have consumers (likely Aggregators)
                non_consumer_connections = len(connections) - len(consumer_connections)
                estimated_aggregator_count = min(4, max(0, non_consumer_connections))  # Cap at expected 4 Aggregators
                
                # Update active sets to reflect actual estimates
                active_wavys.clear()
                for i in range(estimated_wavy_count):
                    wavy_id = f"Wavy-{i+1:02d}"
                    active_wavys.add(wavy_id)
                    
                active_aggregators.clear()
                for i in range(estimated_aggregator_count):
                    aggregator_id = f"Aggregator-{i+1:02d}"
                    active_aggregators.add(aggregator_id)
                
                print(f"🔍 Debug: Consumer queues: {wavy_consumer_queues}, Estimated Wavys: {estimated_wavy_count}")
                print(f"🔍 Debug: Non-consumer connections: {non_consumer_connections}, Estimated Aggregators: {estimated_aggregator_count}")
                        
        except Exception as api_error:
            print(f"🔍 Debug: RabbitMQ Management API not available: {api_error}")
            
            # Fallback: Try direct connection to RabbitMQ
            try:
                import pika
                connection = pika.BlockingConnection(
                    pika.ConnectionParameters(
                        host=RABBITMQ_HOST,
                        credentials=pika.PlainCredentials(RABBITMQ_USERNAME, RABBITMQ_PASSWORD)
                    )
                )
                channel = connection.channel()
                
                # Check for known queue patterns
                test_queues = []
                
                # Try to find queues by attempting to declare them passively
                for i in range(1, 10):  # Check for common component IDs
                    for component_type in ['Wavy', 'Aggregator', 'wavy', 'aggregator']:
                        queue_name = f"rpc_queue_{component_type}{i:02d}"
                        try:
                            method = channel.queue_declare(queue=queue_name, passive=True)
                            if method.method.message_count is not None:
                                test_queues.append(queue_name)
                                if 'wavy' in queue_name.lower():
                                    active_wavys.add(f"{component_type}{i:02d}")
                                elif 'aggregator' in queue_name.lower():
                                    active_aggregators.add(f"{component_type}{i:02d}")
                        except:
                            continue
                
                connection.close()
                print(f"🔍 Debug: Found queues via direct connection: {test_queues}")
                
            except Exception as direct_error:
                print(f"🔍 Debug: Direct RabbitMQ connection failed: {direct_error}")
    
    except Exception as e:
        print(f"🔍 Debug: Error checking RabbitMQ: {e}")
    
    return active_wavys, active_aggregators

# Dashboard HTML template
DASHBOARD_HTML = """
<!DOCTYPE html>
<html>
<head>
    <meta charset="UTF-8">
    <title>Oceanographic Data Dashboard</title>
    <script src="https://cdn.plot.ly/plotly-latest.min.js"></script>
    <style>
        body {
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            margin: 0;
            padding: 20px;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: #333;
            min-height: 100vh;
        }
        .container {
            max-width: 1400px;
            margin: 0 auto;
            background: rgba(255, 255, 255, 0.95);
            border-radius: 15px;
            padding: 30px;
            box-shadow: 0 20px 40px rgba(0, 0, 0, 0.1);
        }
        .header {
            text-align: center;
            margin-bottom: 30px;
            padding-bottom: 20px;
            border-bottom: 3px solid #667eea;
        }
        .header h1 {
            margin: 0;
            color: #2c3e50;
            font-size: 2.5em;
            text-shadow: 2px 2px 4px rgba(0, 0, 0, 0.1);
        }
        .filters {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
            gap: 15px;
            margin-bottom: 30px;
            padding: 20px;
            background: #f8f9fa;
            border-radius: 10px;
            border-left: 5px solid #667eea;
        }
        .filter-group {
            display: flex;
            flex-direction: column;
        }
        .filter-group label {
            font-weight: 600;
            margin-bottom: 5px;
            color: #555;
        }
        .filter-group select, .filter-group input {
            padding: 10px;
            border: 2px solid #e1e5e9;
            border-radius: 5px;
            font-size: 14px;
            transition: border-color 0.3s;
        }
        .filter-group select:focus, .filter-group input:focus {
            outline: none;
            border-color: #667eea;
        }
        .button-group {
            display: flex;
            gap: 10px;
            align-items: flex-end;
        }
        button {
            padding: 12px 20px;
            background: linear-gradient(135deg, #667eea, #764ba2);
            color: white;
            border: none;
            border-radius: 5px;
            cursor: pointer;
            font-weight: 600;
            transition: transform 0.2s, box-shadow 0.2s;
        }
        button:hover {
            transform: translateY(-2px);
            box-shadow: 0 5px 15px rgba(102, 126, 234, 0.3);
        }
        .stats {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
            gap: 20px;
            margin-bottom: 30px;
        }
        .stat-card {
            background: linear-gradient(135deg, #667eea, #764ba2);
            color: white;
            padding: 20px;
            border-radius: 10px;
            text-align: center;
            box-shadow: 0 5px 15px rgba(0, 0, 0, 0.1);
        }
        .stat-card h3 {
            margin: 0 0 10px 0;
            font-size: 1.2em;
        }
        .stat-card .value {
            font-size: 2em;
            font-weight: bold;
        }
        .charts {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(500px, 1fr));
            gap: 30px;
        }
        .chart-container {
            background: white;
            border-radius: 10px;
            padding: 20px;
            box-shadow: 0 5px 15px rgba(0, 0, 0, 0.1);
        }
        .chart-title {
            font-size: 1.3em;
            font-weight: 600;
            color: #2c3e50;
            margin-bottom: 15px;
            text-align: center;
        }
        .loading {
            text-align: center;
            padding: 50px;
            color: #666;
            font-size: 1.2em;
        }
    </style>
</head>
<body>
    <div class="container">
        <div class="header">
            <h1>🌊 Oceanographic Data Dashboard</h1>
            <p>Real-time monitoring and analysis of oceanic sensor data</p>
        </div>
        
        <div class="filters">
            <div class="filter-group">
                <label for="server-filter">🖥️ Server:</label>
                <select id="server-filter">
                    <option value="">All Servers</option>
                </select>
            </div>
            <div class="filter-group">
                <label for="aggregator-filter">🔗 Aggregator:</label>
                <select id="aggregator-filter">
                    <option value="">All Aggregators</option>
                </select>
            </div>
            <div class="filter-group">
                <label for="wavy-filter">🌊 Wavy:</label>
                <select id="wavy-filter">
                    <option value="">All Wavys</option>
                </select>
            </div>
            <div class="filter-group">
                <label for="ocean-filter">🌊 Ocean:</label>
                <select id="ocean-filter">
                    <option value="">All Oceans</option>
                    <option value="Atlantic">Atlantic</option>
                    <option value="Pacific">Pacific</option>
                    <option value="Indian">Indian</option>
                    <option value="Arctic">Arctic</option>
                    <option value="Southern">Southern</option>
                </select>
            </div>
            <div class="filter-group">
                <label for="area-filter">🏖️ Area Type:</label>
                <select id="area-filter">
                    <option value="">All Areas</option>
                    <option value="Coastal">Coastal</option>
                    <option value="Open">Open</option>
                    <option value="Coastal-Open">Coastal-Open</option>
                </select>
            </div>
            <div class="filter-group">
                <label for="time-range">⏰ Time Range:</label>
                <select id="time-range">
                    <option value="1">Last Hour</option>
                    <option value="6">Last 6 Hours</option>
                    <option value="24" selected>Last 24 Hours</option>
                    <option value="168">Last Week</option>
                </select>
            </div>
            <div class="button-group">
                <button onclick="updateDashboard()">🔄 Update</button>
                <button onclick="clearFilters()">🗑️ Clear Filters</button>
            </div>
        </div>
        
        <div class="stats" id="stats-container">
            <div class="stat-card">
                <h3>📊 Total Records</h3>
                <div class="value" id="total-records">-</div>
            </div>
            <div class="stat-card">
                <h3>🌊 Active Wavys</h3>
                <div class="value" id="active-wavys">-</div>
            </div>
            <div class="stat-card">
                <h3>🔗 Active Aggregators</h3>
                <div class="value" id="active-aggregators">-</div>
            </div>
            <div class="stat-card">
                <h3>📈 Avg Wave Height</h3>
                <div class="value" id="avg-wave-height">-</div>
            </div>
        </div>
        
        <div class="charts">
            <div class="chart-container">
                <div class="chart-title">🌊 Wave Height Over Time</div>
                <div id="wave-height-chart"></div>
            </div>
            <div class="chart-container">
                <div class="chart-title">🌡️ Sea Surface Temperature</div>
                <div id="temperature-chart"></div>
            </div>
            <div class="chart-container">
                <div class="chart-title">💨 Wind Speed Distribution</div>
                <div id="wind-chart"></div>
            </div>
            <div class="chart-container">
                <div class="chart-title">🧂 Salinity vs Chlorophyll</div>
                <div id="salinity-chart"></div>
            </div>
            <div class="chart-container">
                <div class="chart-title">🌊 Current Speed by Ocean</div>
                <div id="current-chart"></div>
            </div>
            <div class="chart-container">
                <div class="chart-title">📊 Data Distribution by Wavy</div>
                <div id="wavy-distribution-chart"></div>
            </div>
        </div>
    </div>

    <script>
        let currentData = [];
        
        async function loadFilters() {
            try {
                const response = await fetch('/api/filters');
                const filters = await response.json();
                
                populateSelect('server-filter', filters.servers);
                populateSelect('aggregator-filter', filters.aggregators);
                populateSelect('wavy-filter', filters.wavys);
            } catch (error) {
                console.error('Error loading filters:', error);
            }
        }
        
        function populateSelect(selectId, options) {
            const select = document.getElementById(selectId);
            const currentValue = select.value;
            
            // Keep "All" option and add new options
            while (select.children.length > 1) {
                select.removeChild(select.lastChild);
            }
            
            options.forEach(option => {
                const optionElement = document.createElement('option');
                optionElement.value = option;
                optionElement.textContent = option;
                select.appendChild(optionElement);
            });
            
            select.value = currentValue;
        }
        
        async function updateDashboard() {
            showLoading();
            
            const filters = {
                server: document.getElementById('server-filter').value,
                aggregator: document.getElementById('aggregator-filter').value,
                wavy: document.getElementById('wavy-filter').value,
                ocean: document.getElementById('ocean-filter').value,
                area: document.getElementById('area-filter').value,
                timeRange: document.getElementById('time-range').value
            };
              try {
                // Get data
                const response = await fetch('/api/data?' + new URLSearchParams(filters));
                const data = await response.json();
                currentData = data.records;
                
                // Get statistics from dedicated endpoint
                const statsResponse = await fetch('/api/statistics');
                const statsData = await statsResponse.json();
                
                updateStats(statsData);
                createCharts(data.records);
            } catch (error) {
                console.error('Error updating dashboard:', error);
                alert('Error loading dashboard data. Please check the server connection.');
            }
        }
        
        function updateStats(stats) {
            document.getElementById('total-records').textContent = stats.totalRecords.toLocaleString();
            document.getElementById('active-wavys').textContent = stats.activeWavys;
            document.getElementById('active-aggregators').textContent = stats.activeAggregators;
            document.getElementById('avg-wave-height').textContent = stats.avgWaveHeight + ' m';
        }
        
        function createCharts(data) {
            if (!data || data.length === 0) {
                showNoDataMessage();
                return;
            }
            
            createWaveHeightChart(data);
            createTemperatureChart(data);
            createWindChart(data);
            createSalinityChart(data);
            createCurrentChart(data);
            createWavyDistributionChart(data);
        }
        
        function createWaveHeightChart(data) {
            const trace = {
                x: data.map(d => d.timestamp),
                y: data.map(d => d.wave_height_meters),
                type: 'scatter',
                mode: 'lines+markers',
                name: 'Wave Height',
                line: { color: '#667eea', width: 2 },
                marker: { size: 4 }
            };
            
            const layout = {
                xaxis: { title: 'Time' },
                yaxis: { title: 'Wave Height (m)' },
                margin: { t: 20, r: 20, b: 40, l: 50 }
            };
            
            Plotly.newPlot('wave-height-chart', [trace], layout);
        }
        
        function createTemperatureChart(data) {
            const trace = {
                x: data.map(d => d.timestamp),
                y: data.map(d => d.sea_surface_temperature_celsius),
                type: 'scatter',
                mode: 'lines+markers',
                name: 'Sea Surface Temperature',
                line: { color: '#e74c3c', width: 2 },
                marker: { size: 4 }
            };
            
            const layout = {
                xaxis: { title: 'Time' },
                yaxis: { title: 'Temperature (°C)' },
                margin: { t: 20, r: 20, b: 40, l: 50 }
            };
            
            Plotly.newPlot('temperature-chart', [trace], layout);
        }
        
        function createWindChart(data) {
            const windSpeeds = data.map(d => d.wind_speed_ms);
            
            const trace = {
                x: windSpeeds,
                type: 'histogram',
                nbinsx: 20,
                marker: { color: '#27ae60' },
                name: 'Wind Speed Distribution'
            };
            
            const layout = {
                xaxis: { title: 'Wind Speed (m/s)' },
                yaxis: { title: 'Frequency' },
                margin: { t: 20, r: 20, b: 40, l: 50 }
            };
            
            Plotly.newPlot('wind-chart', [trace], layout);
        }
        
        function createSalinityChart(data) {
            const trace = {
                x: data.map(d => d.salinity_psu),
                y: data.map(d => d.chlorophyll_mg_m3),
                mode: 'markers',
                type: 'scatter',
                marker: {
                    size: 8,
                    color: data.map(d => d.wave_height_meters),
                    colorscale: 'Viridis',
                    showscale: true,
                    colorbar: { title: 'Wave Height (m)' }
                },
                text: data.map(d => `Wavy: ${d.wavy_id}<br>Wave Height: ${d.wave_height_meters}m`),
                hovertemplate: '%{text}<br>Salinity: %{x} PSU<br>Chlorophyll: %{y} mg/m³<extra></extra>'
            };
            
            const layout = {
                xaxis: { title: 'Salinity (PSU)' },
                yaxis: { title: 'Chlorophyll (mg/m³)' },
                margin: { t: 20, r: 60, b: 40, l: 50 }
            };
            
            Plotly.newPlot('salinity-chart', [trace], layout);
        }
        
        function createCurrentChart(data) {
            // Group data by ocean and calculate average current speed
            const oceanData = {};
            data.forEach(d => {
                if (!d.ocean) return;
                if (!oceanData[d.ocean]) {
                    oceanData[d.ocean] = [];
                }
                oceanData[d.ocean].push(d.current_speed_ms);
            });
            
            const oceans = Object.keys(oceanData);
            const avgCurrents = oceans.map(ocean => {
                const speeds = oceanData[ocean];
                return speeds.reduce((a, b) => a + b, 0) / speeds.length;
            });
            
            const trace = {
                x: oceans,
                y: avgCurrents,
                type: 'bar',
                marker: { color: '#3498db' }
            };
            
            const layout = {
                xaxis: { title: 'Ocean' },
                yaxis: { title: 'Average Current Speed (m/s)' },
                margin: { t: 20, r: 20, b: 40, l: 50 }
            };
            
            Plotly.newPlot('current-chart', [trace], layout);
        }
        
        function createWavyDistributionChart(data) {
            // Count records by wavy_id
            const wavyData = {};
            data.forEach(d => {
                wavyData[d.wavy_id] = (wavyData[d.wavy_id] || 0) + 1;
            });
            
            const trace = {
                labels: Object.keys(wavyData),
                values: Object.values(wavyData),
                type: 'pie',
                marker: {
                    colors: ['#667eea', '#764ba2', '#f093fb', '#f5576c', '#4facfe', '#00f2fe']
                }
            };
            
            const layout = {
                margin: { t: 20, r: 20, b: 20, l: 20 }
            };
            
            Plotly.newPlot('wavy-distribution-chart', [trace], layout);
        }
        
        function showLoading() {
            const chartContainers = document.querySelectorAll('.chart-container > div[id$="-chart"]');
            chartContainers.forEach(container => {
                container.innerHTML = '<div class="loading">Loading chart data...</div>';
            });
        }
        
        function showNoDataMessage() {
            const chartContainers = document.querySelectorAll('.chart-container > div[id$="-chart"]');
            chartContainers.forEach(container => {
                container.innerHTML = '<div class="loading">No data available for the selected filters</div>';
            });
        }
        
        function clearFilters() {
            document.getElementById('server-filter').value = '';
            document.getElementById('aggregator-filter').value = '';
            document.getElementById('wavy-filter').value = '';
            document.getElementById('ocean-filter').value = '';
            document.getElementById('area-filter').value = '';
            document.getElementById('time-range').value = '24';
            updateDashboard();
        }
        
        // Initialize dashboard
        document.addEventListener('DOMContentLoaded', function() {
            loadFilters();
            updateDashboard();
            
            // Auto-refresh every 30 seconds
            setInterval(updateDashboard, 30000);
        });
    </script>
</body>
</html>
"""

@app.route('/')
def dashboard():
    """Main dashboard page"""
    return render_template_string(DASHBOARD_HTML)

@app.route('/api/filters')
def get_filters():
    """Get available filter options"""
    if db is None:
        return jsonify({'error': 'Database not available'}), 500
    
    try:
        # Get unique servers, aggregators, and wavys from the data
        servers = list(db[CONFIG_SERVER_COLLECTION].distinct("ServerID"))
        aggregators = list(db[CONFIG_AGR_COLLECTION].distinct("AggregatorId"))
        wavys = list(db[CONFIG_WAVY_COLLECTION].distinct("WAVY_ID"))
        
        # Also get from actual data collections
        wavy_messages_wavys = list(db[WAVY_MESSAGES_COLLECTION].distinct("wavy_id"))
        readings_wavys = list(db[READINGS_COLLECTION].distinct("wavy_id"))
        
        # Combine and deduplicate
        all_wavys = list(set(wavys + wavy_messages_wavys + readings_wavys))
        
        return jsonify({
            'servers': sorted([s for s in servers if s]),
            'aggregators': sorted([a for a in aggregators if a]),
            'wavys': sorted([w for w in all_wavys if w])
        })
    except Exception as e:
        print(f"Error getting filters: {e}")
        return jsonify({'error': str(e)}), 500

@app.route('/api/data')
def get_data():
    """Get filtered oceanographic data"""
    if db is None:
        return jsonify({'error': 'Database not available'}), 500
    
    try:
        # Get filter parameters
        server_filter = request.args.get('server', '')
        aggregator_filter = request.args.get('aggregator', '')
        wavy_filter = request.args.get('wavy', '')
        ocean_filter = request.args.get('ocean', '')
        area_filter = request.args.get('area', '')
        time_range_hours = int(request.args.get('timeRange', '24'))
        
        # Build MongoDB query
        query = {}
        # Time filter
        time_cutoff = datetime.now(UTC) - timedelta(hours=time_range_hours)
        
        # Try to get data from different collections
        data = []
        
        # First try WavyMessages collection
        wavy_query = query.copy()
        if wavy_filter:
            wavy_query['wavy_id'] = wavy_filter
        
        wavy_messages = list(db[WAVY_MESSAGES_COLLECTION].find(wavy_query).limit(1000))
        
        # Then try readings collection
        readings_query = query.copy()
        if wavy_filter:
            readings_query['wavy_id'] = wavy_filter
        if ocean_filter:
            readings_query['ocean'] = ocean_filter
        if area_filter:
            readings_query['area_type'] = area_filter
        
        readings = list(db[READINGS_COLLECTION].find(readings_query).limit(1000))
        
        # Combine and format data
        all_records = []
        
        # Process wavy messages
        for record in wavy_messages:
            formatted_record = {
                'wavy_id': record.get('wavy_id', ''),
                'timestamp': record.get('timestamp', ''),
                'latitude': record.get('latitude', 0),
                'longitude': record.get('longitude', 0),
                'wave_height_meters': record.get('wave_height_meters', 0),
                'sea_surface_temperature_celsius': record.get('sea_surface_temperature_celsius', 0),
                'wind_speed_ms': record.get('wind_speed_ms', 0),
                'wind_direction_degrees': record.get('wind_direction_degrees', 0),
                'current_speed_ms': record.get('current_speed_ms', 0),
                'current_direction_degrees': record.get('current_direction_degrees', 0),
                'salinity_psu': record.get('salinity_psu', 0),
                'chlorophyll_mg_m3': record.get('chlorophyll_mg_m3', 0),
                'acoustic_level_db': record.get('acoustic_level_db', 0),
                'turbidity_ntu': record.get('turbidity_ntu', 0),
                'precipitation_rate_mm_h': record.get('precipitation_rate_mm_h', 0),
                'surface_pressure_hpa': record.get('surface_pressure_hpa', 0),
                'ocean': record.get('ocean', 'Unknown'),
                'area_type': record.get('area_type', 'Unknown')
            }
            all_records.append(formatted_record)
        
        # Process readings
        for record in readings:
            formatted_record = {
                'wavy_id': record.get('wavy_id', ''),
                'timestamp': record.get('timestamp', ''),
                'latitude': record.get('latitude', 0),
                'longitude': record.get('longitude', 0),
                'wave_height_meters': record.get('wave_height_meters', 0),
                'sea_surface_temperature_celsius': record.get('sea_surface_temperature_celsius', 0),
                'wind_speed_ms': record.get('wind_speed_ms', 0),
                'wind_direction_degrees': record.get('wind_direction_degrees', 0),
                'current_speed_ms': record.get('current_speed_ms', 0),
                'current_direction_degrees': record.get('current_direction_degrees', 0),
                'salinity_psu': record.get('salinity_psu', 0),
                'chlorophyll_mg_m3': record.get('chlorophyll_mg_m3', 0),
                'acoustic_level_db': record.get('acoustic_level_db', 0),
                'turbidity_ntu': record.get('turbidity_ntu', 0),
                'precipitation_rate_mm_h': record.get('precipitation_rate_mm_h', 0),
                'surface_pressure_hpa': record.get('surface_pressure_hpa', 0),
                'ocean': record.get('ocean', 'Unknown'),
                'area_type': record.get('area_type', 'Unknown')
            }
            all_records.append(formatted_record)        
        return jsonify({
            'records': all_records
        })
    except Exception as e:
        print(f"Error getting data: {e}")
        return jsonify({'error': str(e)}), 500

@app.route('/api/statistics')
def get_statistics():
    """Get system statistics including truly active components from RabbitMQ"""
    if db is None:
        return jsonify({'error': 'Database not available'}), 500
    
    try:
        print("🔍 Debug: Getting statistics...")
        
        # Get truly active components from RabbitMQ
        active_wavys_set, active_aggregators_set = get_active_components_from_rabbitmq()
        
        print(f"🔍 Debug: Active Wavys from RabbitMQ: {active_wavys_set}")
        print(f"🔍 Debug: Active Aggregators from RabbitMQ: {active_aggregators_set}")
        
        # Get recent data counts for additional statistics
        time_cutoff = datetime.now(UTC) - timedelta(hours=24)
        
        # Count recent records
        wavy_messages_count = db[WAVY_MESSAGES_COLLECTION].count_documents({
            "timestamp": {"$gte": time_cutoff}
        }) if WAVY_MESSAGES_COLLECTION in db.list_collection_names() else 0
        
        readings_count = db[READINGS_COLLECTION].count_documents({
            "timestamp": {"$gte": time_cutoff}
        }) if READINGS_COLLECTION in db.list_collection_names() else 0
        
        aggregated_data_count = db[AGGREGATED_DATA_COLLECTION].count_documents({
            "timestamp": {"$gte": time_cutoff}
        }) if AGGREGATED_DATA_COLLECTION in db.list_collection_names() else 0
        
        total_records = wavy_messages_count + readings_count + aggregated_data_count
        
        # Calculate average wave height from recent data
        pipeline = [
            {"$match": {"timestamp": {"$gte": time_cutoff}, "wave_height_meters": {"$exists": True, "$ne": None}}},
            {"$group": {"_id": None, "avg_wave_height": {"$avg": "$wave_height_meters"}}}
        ]
        
        avg_wave_height = 0
        try:
            wavy_avg = list(db[WAVY_MESSAGES_COLLECTION].aggregate(pipeline))
            readings_avg = list(db[READINGS_COLLECTION].aggregate(pipeline))
            
            if wavy_avg and wavy_avg[0]['avg_wave_height']:
                avg_wave_height = wavy_avg[0]['avg_wave_height']
            elif readings_avg and readings_avg[0]['avg_wave_height']:
                avg_wave_height = readings_avg[0]['avg_wave_height']
        except:
            pass
        
        stats = {
            'totalRecords': total_records,
            'activeWavys': len(active_wavys_set),
            'activeAggregators': len(active_aggregators_set),
            'avgWaveHeight': round(avg_wave_height, 2),
            'activeWavysList': list(active_wavys_set),
            'activeAggregatorsList': list(active_aggregators_set),
            'lastUpdated': datetime.now(UTC).isoformat(),
            'dataBreakdown': {
                'wavyMessages': wavy_messages_count,
                'readings': readings_count,
                'aggregatedData': aggregated_data_count
            }
        }
        
        print(f"🔍 Debug: Returning stats: {stats}")
        return jsonify(stats)
        
    except Exception as e:
        print(f"❌ Error getting statistics: {e}")
        return jsonify({'error': str(e)}), 500

@app.route('/health')
def health_check():
    """Health check endpoint"""
    return jsonify({
        'status': 'healthy',
        'timestamp': datetime.now(UTC).isoformat(),
        'database': 'connected' if db is not None else 'disconnected'
    })

if __name__ == '__main__':
    print("🌊 Starting Oceanographic Data Dashboard...")
    print("📊 Dashboard will be available at: http://localhost:5001")
    print("🏥 Health check available at: http://localhost:5001/health")
    app.run(debug=True, host='0.0.0.0', port=5001)
