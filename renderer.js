const { ipcRenderer } = require('electron');

let currentComponent = null;
let componentOutputs = new Map();
let runningProcesses = new Set();

// Initialize
document.addEventListener('DOMContentLoaded', () => {
    updateProcessList();
    setupEventListeners();
    initializeAccordions();
    setInterval(updateProcessList, 2000); // Update every 2 seconds
});

function setupEventListeners() {
    // Terminal input
    const commandInput = document.getElementById('command-input');
    commandInput.addEventListener('keypress', (e) => {
        if (e.key === 'Enter') {
            sendCommand();
        }
    });

    // Server ID input - add event listener after DOM is ready
    const serverIdInput = document.getElementById('server-id');
    if (serverIdInput) {
        serverIdInput.addEventListener('keypress', (e) => {
            if (e.key === 'Enter') {
                startServerWithId();
            }
        });
    }// Keyboard shortcuts
    document.addEventListener('keydown', (e) => {
        // Ctrl+` to focus terminal input
        if (e.ctrlKey && e.key === '`') {
            e.preventDefault();
            if (currentComponent && runningProcesses.has(currentComponent)) {
                commandInput.focus();
            }
        }

        // Ctrl+Shift+S to start server
        if (e.ctrlKey && e.shiftKey && e.key === 'S') {
            e.preventDefault();
            startServer();
        }

        // Ctrl+Shift+R to focus server ID input for regional servers
        if (e.ctrlKey && e.shiftKey && e.key === 'R') {
            e.preventDefault();
            const serverIdInput = document.getElementById('server-id');
            if (serverIdInput) {
                serverIdInput.focus();
            }
        }

        // Ctrl+Shift+A to start all
        if (e.ctrlKey && e.shiftKey && e.key === 'A') {
            e.preventDefault();
            startAllComponents();
        }

        // Ctrl+Shift+X to stop all
        if (e.ctrlKey && e.shiftKey && e.key === 'X') {
            e.preventDefault();
            stopAllProcesses();
        }

        // Ctrl+Shift+G to create new aggregator
        if (e.ctrlKey && e.shiftKey && e.key === 'G') {
            e.preventDefault();
            showCreateAggregatorForm();
        }

        // Ctrl+Shift+W to create new wavy
        if (e.ctrlKey && e.shiftKey && e.key === 'W') {
            e.preventDefault();
            showCreateWavyForm();
        }

        // Escape to close modals
        if (e.key === 'Escape') {
            hideCreateAggregatorForm();
            hideCreateWavyForm();
        }
    });// IPC listeners
    ipcRenderer.on('process-output', (event, processId, output) => {
        if (!componentOutputs.has(processId)) {
            componentOutputs.set(processId, '');
        }

        // Clean up output and add timestamp for manager messages
        let cleanOutput = output;
        if (cleanOutput.includes('[MANAGER]')) {
            const timestamp = new Date().toLocaleTimeString();
            cleanOutput = cleanOutput.replace('[MANAGER]', `[MANAGER ${timestamp}]`);
        }

        componentOutputs.set(processId, componentOutputs.get(processId) + cleanOutput);

        if (currentComponent === processId) {
            updateTerminalContent();
        }

        // Auto-scroll notification for background processes
        if (currentComponent !== processId && runningProcesses.has(processId)) {
            showNotification(processId, 'New output available');
        }
    });

    ipcRenderer.on('process-closed', (event, processId) => {
        runningProcesses.delete(processId);
        updateComponentStatus(processId, false);

        // Add closure message to output
        addToOutput(processId, `\n[MANAGER] Process ${processId} has stopped.\n`);

        // Show notification
        showNotification(processId, 'Process stopped');
    });

    // Handle auto-started server
    ipcRenderer.on('server-auto-started', () => {
        runningProcesses.add('server');
        updateComponentStatus('server', true);
        addToOutput('server', '[MANAGER] Server auto-started on application launch.\n');
        selectComponent('server');        // Show welcome message with auto-start info
        const terminalContent = document.getElementById('terminal-content');
        terminalContent.innerHTML = `
            <div class="welcome-message">
                🚀 <strong>Server Auto-Started!</strong> 🚀<br><br>
                <span class="success-text">✅ Servidor is now running automatically</span><br><br>
                <span class="info-text">Next Steps:</span><br>
                • Use <strong>Regional Quick Start</strong> to deploy complete regional systems<br>
                • Use <strong>+ Create New</strong> buttons to create custom Aggregators and Wavy sensors<br>
                • Or manually start existing Aggregators and Wavy components<br><br>
                <span class="info-text">Creation Shortcuts:</span><br>
                • <strong>Ctrl+Shift+G</strong> - Create New Aggregator<br>
                • <strong>Ctrl+Shift+W</strong> - Create New Wavy Sensor<br><br><span class="info-text">Regional Quick Start Options:</span><br>
                • <strong>Europe (EU)</strong> - 4 aggregators, 4 wavy sensors (Atlantic/Arctic coverage)<br>
                • <strong>North America (NA)</strong> - 5 aggregators, 7 wavy sensors (Atlantic/Pacific/Arctic coverage)<br>
                • <strong>South America (SA)</strong> - 4 aggregators, 6 wavy sensors (Atlantic/Pacific coverage)<br>
                • <strong>Africa (AF)</strong> - 4 aggregators, 6 wavy sensors (Atlantic/Indian coverage)<br>
                • <strong>Asia (AS)</strong> - 5 aggregators, 8 wavy sensors (Pacific/Indian/Arctic coverage)<br>
                • <strong>Oceania (OC)</strong> - 5 aggregators, 6 wavy sensors (Pacific/Indian/Southern coverage)<br>
                • <strong>Antarctica (AQ)</strong> - 1 aggregator, 4 wavy sensors (Southern Ocean coverage)<br>
                • <strong>🌍 Start All Regions</strong> - Deploy globally (All 32 wavy sensors)<br><br>
                <span style="color: #ffaa00;">Click on "server" in the sidebar to view server output.</span>
            </div>
        `;
    });
}

async function startServer() {
    const result = await ipcRenderer.invoke('start-server');
    if (result.success) {
        runningProcesses.add('server');
        updateComponentStatus('server', true);
        addToOutput('server', '[MANAGER] Starting Servidor...\n');
        selectComponent('server');
        // Clear the server ID input field
        document.getElementById('server-id').value = '';
    } else {
        alert(`Failed to start server: ${result.message}`);
    }
}

async function startServerWithId() {
    const serverId = document.getElementById('server-id').value.trim();
    
    if (!serverId) {
        // If no ID provided, start basic server
        return startServer();
    }

    const processId = `server-${serverId}`;
    const result = await ipcRenderer.invoke('start-server-instance', serverId);

    if (result.success) {
        runningProcesses.add(processId);
        addServerToList(serverId, processId);
        addToOutput(processId, `[MANAGER] Starting Regional Server ${serverId}...\n`);
        selectComponent(processId);
        document.getElementById('server-id').value = '';
    } else {
        alert(`Failed to start server instance: ${result.message}`);
    }
}

async function startAggregator() {
    const aggregatorId = document.getElementById('aggregator-id').value.trim();
    if (!aggregatorId) {
        alert('Please enter an Aggregator ID (e.g., N_Agr)');
        return;
    }

    const processId = `aggregator-${aggregatorId}`;
    const result = await ipcRenderer.invoke('start-aggregator', aggregatorId);

    if (result.success) {
        runningProcesses.add(processId);
        addAggregatorToList(aggregatorId);
        addToOutput(processId, `[MANAGER] Starting Agregador ${aggregatorId}...\n`);
        selectComponent(processId);
        document.getElementById('aggregator-id').value = '';
    } else {
        alert(`Failed to start aggregator: ${result.message}`);
    }
}

async function startWavy() {
    const wavyId = document.getElementById('wavy-id').value.trim();
    if (!wavyId) {
        alert('Please enter a Wavy ID (e.g., Wavy01, Wavy02, etc.)');
        return;
    }

    const processId = `wavy-${wavyId}`;
    const result = await ipcRenderer.invoke('start-wavy', wavyId);

    if (result.success) {
        runningProcesses.add(processId);
        addWavyToList(wavyId);
        addToOutput(processId, `[MANAGER] Starting Wavy ${wavyId}...\n`);
        selectComponent(processId);
        document.getElementById('wavy-id').value = '';
    } else {
        alert(`Failed to start wavy: ${result.message}`);
    }
}

async function stopProcess(processId) {
    const result = await ipcRenderer.invoke('stop-process', processId);
    if (result.success) {
        addToOutput(processId, '[MANAGER] Stopping process...\n');
    } else {
        alert(`Failed to stop process: ${result.message}`);
    }
}

async function stopAllProcesses() {
    const processes = await ipcRenderer.invoke('get-processes');
    for (const process of processes) {
        if (process.alive) {
            await stopProcess(process.id);
        }
    }

    // Clear UI
    runningProcesses.clear();
    document.getElementById('aggregator-list').innerHTML = '';
    document.getElementById('wavy-list').innerHTML = '';
    updateComponentStatus('server', false);
}

async function startAllComponents() {
    // Use the new regional approach - start all regions
    await startAllRegions();
}

async function quickStartRegion(regionCode) {
    const regionNames = {
        'EU': 'Europe',
        'NA': 'North America', 
        'SA': 'South America',
        'AF': 'Africa',
        'AS': 'Asia',
        'OC': 'Oceania',
        'AQ': 'Antarctica'
    };

    const regionName = regionNames[regionCode] || regionCode;
    
    addToOutput('server', `\n[MANAGER] Starting regional deployment for ${regionName} (${regionCode})...\n`);
    
    try {
        const result = await ipcRenderer.invoke('start-regional-components', regionCode);
          if (result.success) {
            // Update UI for started components
            result.results.forEach(componentResult => {
                if (componentResult.success) {
                    const processId = componentResult.type === 'server' ? componentResult.processId : 
                                    componentResult.type === 'aggregator' ? `aggregator-${componentResult.id}` :
                                    `wavy-${componentResult.id}`;
                    
                    runningProcesses.add(processId);
                    
                    if (componentResult.type === 'server') {
                        addServerToList(componentResult.id, componentResult.processId);
                    } else if (componentResult.type === 'aggregator') {
                        addAggregatorToList(componentResult.id);
                    } else if (componentResult.type === 'wavy') {
                        addWavyToList(componentResult.id);
                    }
                    
                    updateComponentStatus(processId, true);
                }
            });
              // Show summary
            const successCount = result.results.filter(r => r.success).length;
            const totalCount = result.results.length;
            
            const serverResult = result.results.find(r => r.type === 'server');
            const serverProcessId = serverResult ? serverResult.processId : 'server';
            
            addToOutput(serverProcessId, 
                `[MANAGER] Regional deployment completed for ${regionName}!\n` +
                `[MANAGER] Successfully started ${successCount}/${totalCount} components\n` +
                `[MANAGER] • Server: ${regionCode}-S\n` +
                `[MANAGER] • Aggregators: ${result.results.filter(r => r.type === 'aggregator' && r.success).map(r => r.id).join(', ')}\n` +
                `[MANAGER] • Wavy Sensors: ${result.results.filter(r => r.type === 'wavy' && r.success).map(r => r.id).join(', ')}\n\n`);
                
            // Auto-select the new server to show output
            selectComponent(serverProcessId);        } else {
            addToOutput('server', `[ERROR] Failed to start regional components: ${result.message}\n`);
            alert(`Failed to start regional components for ${regionName}: ${result.message}`);
        }
    } catch (error) {
        addToOutput('server', `[ERROR] Regional deployment failed: ${error.message}\n`);
        alert(`Regional deployment failed: ${error.message}`);
    }
}

async function startAllRegions() {
    const regions = ['EU', 'NA', 'SA', 'AF', 'AS', 'OC', 'AQ'];
    
    // Create a general output location for global deployment messages
    if (!componentOutputs.has('global-deployment')) {
        componentOutputs.set('global-deployment', '');
    }
    
    addToOutput('global-deployment', '\n[MANAGER] ========================================\n');
    addToOutput('global-deployment', '[MANAGER] 🌍 GLOBAL DEPLOYMENT INITIATED 🌍\n');
    addToOutput('global-deployment', '[MANAGER] ========================================\n\n');
    
    for (let i = 0; i < regions.length; i++) {
        const region = regions[i];
        addToOutput('global-deployment', `[MANAGER] Starting region ${i + 1}/7: ${region}...\n`);
        
        await quickStartRegion(region);
        
        // Add delay between regions to avoid overwhelming the system
        if (i < regions.length - 1) {
            addToOutput('global-deployment', '[MANAGER] Waiting before starting next region...\n\n');
            await new Promise(resolve => setTimeout(resolve, 3000));
        }
    }
    
    addToOutput('global-deployment', '\n[MANAGER] ========================================\n');
    addToOutput('global-deployment', '[MANAGER] 🎉 GLOBAL DEPLOYMENT COMPLETED! 🎉\n');
    addToOutput('global-deployment', '[MANAGER] All regional systems are now active.\n');
    addToOutput('global-deployment', '[MANAGER] ========================================\n\n');
    
    // Select the global deployment view
    selectComponent('global-deployment');
}

function addAggregatorToList(aggregatorId) {
    const list = document.getElementById('aggregator-list');
    const processId = `aggregator-${aggregatorId}`;

    const item = document.createElement('div');
    item.className = 'component-item';
    item.setAttribute('data-component', processId);
    item.onclick = () => selectComponent(processId);

    item.innerHTML = `
        <div class="component-name">${aggregatorId}</div>
        <div class="component-status running" id="status-${processId}">Running</div>
        <button class="btn stop" onclick="event.stopPropagation(); stopProcess('${processId}')" style="margin-top: 5px; font-size: 10px;">Stop</button>
    `;

    list.appendChild(item);
}

function addWavyToList(wavyId) {
    const list = document.getElementById('wavy-list');
    const processId = `wavy-${wavyId}`;

    const item = document.createElement('div');
    item.className = 'component-item';
    item.setAttribute('data-component', processId);
    item.onclick = () => selectComponent(processId);

    item.innerHTML = `
        <div class="component-name">${wavyId}</div>
        <div class="component-status running" id="status-${processId}">Running</div>
        <button class="btn stop" onclick="event.stopPropagation(); stopProcess('${processId}')" style="margin-top: 5px; font-size: 10px;">Stop</button>
    `;

    list.appendChild(item);
}

function addServerToList(serverId, processId) {
    // Check if we have a servers section, if not create one
    let serversSection = document.querySelector('.servers-section');
    if (!serversSection) {
        // Create servers section in the sidebar
        const sidebar = document.querySelector('.sidebar');
        const serverSection = document.querySelector('.section'); // Get the existing server section
          serversSection = document.createElement('div');
        serversSection.className = 'section servers-section';
        serversSection.innerHTML = `
            <div class="section-title accordion-header" onclick="toggleAccordion('regional-servers')">
                <span>🌍 Regional Servers</span>
                <span class="accordion-arrow" id="regional-servers-arrow">▼</span>
            </div>
            <div class="accordion-content" id="regional-servers-content">
                <div id="server-list"></div>
            </div>
        `;
          // Insert after the main server section
        serverSection.parentNode.insertBefore(serversSection, serverSection.nextSibling);
        
        // Initialize the accordion state for the newly created section
        const content = document.getElementById('regional-servers-content');
        const arrow = document.getElementById('regional-servers-arrow');
        if (content && arrow) {
            // Keep it open by default since it's actively being used
            content.classList.remove('collapsed');
            arrow.classList.remove('rotated');
            arrow.textContent = '▼';
        }
    }
    
    const list = document.getElementById('server-list');
    
    // Check if server already exists in list
    const existingItem = list.querySelector(`[data-component="${processId}"]`);
    if (existingItem) return; // Don't add duplicates

    const item = document.createElement('div');
    item.className = 'component-item';
    item.setAttribute('data-component', processId);
    item.onclick = () => selectComponent(processId);

    item.innerHTML = `
        <div class="component-name">${serverId}</div>
        <div class="component-status running" id="status-${processId}">Running</div>
        <button class="btn stop" onclick="event.stopPropagation(); stopProcess('${processId}')" style="margin-top: 5px; font-size: 10px;">Stop</button>
    `;

    list.appendChild(item);
}

function selectComponent(componentId) {
    // Remove active class from all components
    document.querySelectorAll('.component-item').forEach(item => {
        item.classList.remove('active');
    });

    // Add virtual component for global deployment if it doesn't exist
    if (componentId === 'global-deployment' && !document.querySelector(`[data-component="${componentId}"]`)) {
        // Create a virtual component item for global deployment
        const sidebar = document.querySelector('.sidebar');
        const virtualSection = document.createElement('div');
        virtualSection.className = 'section';
        virtualSection.innerHTML = `
            <div class="section-title">Global Deployment</div>
            <div class="component-item" data-component="global-deployment" onclick="selectComponent('global-deployment')">
                <div class="component-name">Global Status</div>
                <div class="component-status running">Active</div>
            </div>
        `;
        sidebar.appendChild(virtualSection);
    }

    // Add active class to selected component
    const selectedElement = document.querySelector(`[data-component="${componentId}"]`);
    if (selectedElement) {
        selectedElement.classList.add('active');
    }

    currentComponent = componentId;
    updateTerminalContent();    // Show terminal input for this component
    const terminalInput = document.getElementById('terminal-input');
    const terminal = document.querySelector('.terminal');
    if (runningProcesses.has(componentId)) {
        terminalInput.style.display = 'block';
        terminal.classList.add('input-visible');
    } else {
        terminalInput.style.display = 'none';
        terminal.classList.remove('input-visible');
    }
}

// Terminal control features
let tailMode = true;
const MAX_OUTPUT_LENGTH = 5000;
const TAIL_MODE_LINES = 100;

function clearTerminal() {
    if (currentComponent && componentOutputs.has(currentComponent)) {
        componentOutputs.set(currentComponent, '');
        updateTerminalContent();
    }
}

function toggleTailMode() {
    tailMode = !tailMode;
    document.getElementById('tail-mode-status').textContent = tailMode ? 'ON' : 'OFF';
    updateTerminalContent();
}

function addToOutput(componentId, text) {
    if (!componentOutputs.has(componentId)) {
        componentOutputs.set(componentId, '');
    }

    let output = componentOutputs.get(componentId) + text;

    // Limit overall output size to prevent memory issues
    if (output.length > MAX_OUTPUT_LENGTH) {
        output = output.slice(-MAX_OUTPUT_LENGTH);
    }

    componentOutputs.set(componentId, output);

    if (currentComponent === componentId) {
        updateTerminalContent();
    }

    // Update message count
    updateMessageCount(componentId);
}

function updateMessageCount(componentId) {
    if (currentComponent === componentId) {
        const output = componentOutputs.get(componentId) || '';
        const messageCount = (output.match(/Data received successfully/g) || []).length;
        document.getElementById('message-count').textContent = `${messageCount} messages`;
    }
}

function updateTerminalContent() {
    const terminalContent = document.getElementById('terminal-content');

    if (currentComponent && componentOutputs.has(currentComponent)) {
        let output = componentOutputs.get(currentComponent);
        const isRunning = runningProcesses.has(currentComponent);
        const status = isRunning ? 'RUNNING' : 'STOPPED';
        const statusColor = isRunning ? '#00ff00' : '#ff6666';

        // In tail mode, only show the last portion of the output
        if (tailMode && output.length > 0) {
            const lines = output.split('\n');
            if (lines.length > TAIL_MODE_LINES) {
                output = lines.slice(-TAIL_MODE_LINES).join('\n');
                output = `[...${lines.length - TAIL_MODE_LINES} earlier messages hidden...]\n\n` + output;
            }
        }

        terminalContent.innerHTML = `
            <div style="color: #00aaaa; margin-bottom: 10px; border-bottom: 1px solid #333; padding-bottom: 5px;">
                [${currentComponent.toUpperCase()}] - <span style="color: ${statusColor}">${status}</span>
            </div>` + 
            formatOutput(output);

        // Update message count
        updateMessageCount(currentComponent);
    } else if (currentComponent) {
        terminalContent.innerHTML = `
            <div style="color: #00aaaa; margin-bottom: 10px;">
                [${currentComponent.toUpperCase()}] Waiting for output...
            </div>`;
    }

    // Auto-scroll to bottom
    terminalContent.scrollTop = terminalContent.scrollHeight;
}

function formatOutput(text) {
    // Escape HTML and format special messages
    const escaped = escapeHtml(text);

    return escaped
        .replace(/\[MANAGER[^\]]*\]/g, '<span style="color: #00aaaa; font-weight: bold;">$&</span>')
        .replace(/\[ERROR\]/g, '<span style="color: #ff6666; font-weight: bold;">[ERROR]</span>')
        .replace(/\[.*?\]/g, '<span style="color: #ffaa00;">$&</span>') // Other brackets in orange
        .replace(/ID da Wavy:|ID do Agregador:/g, '<span style="color: #00ff00; font-weight: bold;">$&</span>')
        .replace(/Handshake estabelecido|conexão estabelecida|sucesso/gi, '<span style="color: #66ff66;">$&</span>')
        .replace(/erro|falha|failed|error/gi, '<span style="color: #ff6666;">$&</span>');
}

function showNotification(processId, message) {
    // Simple visual notification - could be expanded
    const componentElement = document.querySelector(`[data-component="${processId}"]`);
    if (componentElement) {
        componentElement.style.border = '2px solid #ffaa00';
        setTimeout(() => {
            componentElement.style.border = '';
        }, 2000);
    }
}

function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

function updateSystemStatus() {
    const indicator = document.getElementById('status-indicator');
    const statusText = document.getElementById('status-text');
    const serverRunning = runningProcesses.has('server');
    const totalProcesses = runningProcesses.size;

    if (serverRunning && totalProcesses > 1) {
        indicator.className = 'status-indicator';
        indicator.style.color = '#00ff00';
        statusText.textContent = `System Active (${totalProcesses} processes)`;
    } else if (serverRunning) {
        indicator.className = 'status-indicator warning';
        indicator.style.color = '#ffaa00';
        statusText.textContent = 'Server Ready';
    } else if (totalProcesses > 0) {
        indicator.className = 'status-indicator warning';
        indicator.style.color = '#ffaa00';
        statusText.textContent = `${totalProcesses} processes (no server)`;
    } else {
        indicator.className = 'status-indicator error';
        indicator.style.color = '#ff6666';
        statusText.textContent = 'System Idle';
    }
}

async function sendCommand() {
    const input = document.getElementById('command-input');
    const command = input.value.trim();

    if (command && currentComponent) {
        const success = await ipcRenderer.invoke('send-input', currentComponent, command);
        if (success) {
            addToOutput(currentComponent, `> ${command}\n`);
        }
    }

    input.value = '';
}

async function updateProcessList() {
    const processes = await ipcRenderer.invoke('get-processes');

    // Update running processes set
    runningProcesses.clear();
    processes.forEach(proc => {
        if (proc.alive) {
            runningProcesses.add(proc.id);
        }
    });

    // Update UI status indicators
    processes.forEach(proc => {
        updateComponentStatus(proc.id, proc.alive);
    });

    // Update server button
    const serverBtn = document.getElementById('btn-server');
    if (runningProcesses.has('server')) {
        serverBtn.textContent = 'Stop Server';
        serverBtn.onclick = () => stopProcess('server');
        serverBtn.classList.add('stop');
    } else {
        serverBtn.textContent = 'Start Server';
        serverBtn.onclick = startServer;
        serverBtn.classList.remove('stop');
    }

    // Update system status
    updateSystemStatus();

    // Update map markers if map is open
    if (map) {
        updateMapMarkers();
    }
}

function updateComponentStatus(processId, isRunning) {
    const statusElement = document.getElementById(`status-${processId}`);
    if (statusElement) {
        statusElement.textContent = isRunning ? 'Running' : 'Stopped';
        statusElement.className = `component-status ${isRunning ? 'running' : 'stopped'}`;
    }
}

function highlightErrors() {
    if (currentComponent && componentOutputs.has(currentComponent)) {
        const terminalContent = document.getElementById('terminal-content');
        const errorElements = terminalContent.querySelectorAll('span[style*="color: #ff6666"]');

        if (errorElements.length > 0) {
            // Flash all error elements
            errorElements.forEach(el => {
                el.style.backgroundColor = '#440000';

                // Scroll to the first error
                if (el === errorElements[0]) {
                    el.scrollIntoView({ behavior: 'smooth', block: 'center' });
                }
            });

            // Remove highlighting after a few seconds
            setTimeout(() => {
                errorElements.forEach(el => {
                    el.style.backgroundColor = 'transparent';
                });
            }, 3000);
        } else {
            // If no errors found, add a temporary message
            const message = document.createElement('div');
            message.textContent = 'No errors found in current output';
            message.style.color = '#66ff66';
            message.style.textAlign = 'center';
            message.style.padding = '10px';
            message.style.margin = '10px 0';
            message.style.backgroundColor = '#003300';
            message.style.borderRadius = '5px';

            terminalContent.insertBefore(message, terminalContent.firstChild);

            setTimeout(() => {
                message.remove();
            }, 3000);
        }
    }
}

// Accordion functionality
function initializeAccordions() {
    // Initialize accordion states - keep Aggregators open by default, others collapsed
    const accordions = [
        { id: 'aggregators', defaultOpen: true }, 
        { id: 'wavys', defaultOpen: false },
        { id: 'regional-quick-start', defaultOpen: false },
        { id: 'regional-servers', defaultOpen: true } // This will be created dynamically when servers are added
    ];
    
    accordions.forEach(accordion => {
        const content = document.getElementById(`${accordion.id}-content`);
        const arrow = document.getElementById(`${accordion.id}-arrow`);
        
        if (content && arrow) {
            if (!accordion.defaultOpen) {
                content.classList.add('collapsed');
                arrow.classList.add('rotated');
                arrow.textContent = '▶';
            } else {
                content.classList.remove('collapsed');
                arrow.classList.remove('rotated');
                arrow.textContent = '▼';
            }
        }
    });
}

function toggleAccordion(id) {
    const content = document.getElementById(`${id}-content`);
    const arrow = document.getElementById(`${id}-arrow`);

    if (content.classList.contains('collapsed')) {
        content.classList.remove('collapsed');
        arrow.classList.remove('rotated');
        arrow.textContent = '▼';
    } else {
        content.classList.add('collapsed');
        arrow.classList.add('rotated');
        arrow.textContent = '▶';
    }
}

// Map functionality
let map = null;
let mapMarkers = [];
let componentConfigs = new Map(); // Store component configurations with coordinates

async function openMapTab() {
    const modal = document.getElementById('map-modal');
    modal.style.display = 'flex';

    // Load configuration data if not already loaded
    if (componentConfigs.size === 0) {
        console.log('Loading component configurations from MongoDB...');
        await loadComponentConfigurations();
        console.log(`Loaded ${componentConfigs.size} configurations`);
    }

    // Initialize map if not already done
    if (!map) {
        setTimeout(() => {
            initializeMap();
        }, 100); // Small delay to ensure the modal is visible
    } else {
        // Refresh map size in case of container changes
        setTimeout(() => {
            map.invalidateSize();
            updateMapMarkers();
        }, 100);
    }
}

function closeMapTab() {
    const modal = document.getElementById('map-modal');
    modal.style.display = 'none';
}

// Close modal when clicking outside of it
document.addEventListener('click', (event) => {
    const modal = document.getElementById('map-modal');
    if (event.target === modal) {
        closeMapTab();
    }
});

function initializeMap() {
    map = L.map('map').setView([20, 0], 2); // Center on world view

    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
        maxZoom: 18
    }).addTo(map);

    updateMapMarkers();
}

// Load component configurations from MongoDB
async function loadComponentConfigurations() {
    try {
        const { MongoClient } = require('mongodb');
        const client = new MongoClient('mongodb+srv://sdmongo25:w7KPjneQrqV7aOdH@sistemasdistribuidos.tybz613.mongodb.net/');
        
        await client.connect();
        const db = client.db('RabbitMQ-Communication');

        // Load server configurations from MongoDB
        const serverConfigs = await db.collection('ConfigServer').find({}).toArray();
        serverConfigs.forEach(config => {
            if (config.latitude && config.longitude) {
                componentConfigs.set(`server-${config.server_id}`, {
                    type: 'server',
                    id: config.server_id,
                    continent: config.continent,
                    latitude: parseFloat(config.latitude),
                    longitude: parseFloat(config.longitude)
                });
            }
        });        // Load aggregator configurations from MongoDB
        const agrConfigs = await db.collection('ConfigAgr').find({}).toArray();
        console.log(`Found ${agrConfigs.length} aggregator configs in MongoDB`);
        agrConfigs.forEach(config => {
            console.log(`Processing aggregator config:`, config.AggregatorId, config.Latitude, config.Longitude);
            if (config.Latitude && config.Longitude) {
                componentConfigs.set(`aggregator-${config.AggregatorId}`, {
                    type: 'aggregator',
                    id: config.AggregatorId,
                    continent: config.Region,
                    ocean: config.Ocean,
                    areaType: config.AreaType,
                    latitude: parseFloat(config.Latitude),
                    longitude: parseFloat(config.Longitude)
                });
                console.log(`Added aggregator ${config.AggregatorId} to map at ${config.Latitude}, ${config.Longitude}`);
            }
        });// Load wavy configurations from MongoDB
        const wavyConfigs = await db.collection('ConfigWavy').find({}).toArray();
        wavyConfigs.forEach(config => {
            if (config.latitude && config.longitude) {
                componentConfigs.set(`wavy-${config.WAVY_ID}`, {
                    type: 'wavy',
                    id: config.WAVY_ID,
                    ocean: config.ocean,
                    areaType: config.area_type,
                    latitude: parseFloat(config.latitude),
                    longitude: parseFloat(config.longitude)
                });
            }
        });

        await client.close();
        console.log(`Loaded ${componentConfigs.size} component configurations from MongoDB`);
    } catch (error) {
        console.error('Error loading configurations from MongoDB:', error);
    }
}

function updateMapMarkers() {
    if (!map) return;

    console.log(`Updating map markers. Component configs: ${componentConfigs.size}, Running processes: ${runningProcesses.size}`);
    console.log('Component configs:', Array.from(componentConfigs.keys()));
    console.log('Running processes:', Array.from(runningProcesses));

    // Clear existing markers
    mapMarkers.forEach(marker => map.removeLayer(marker));
    mapMarkers = [];

    // Get current map bounds for wrapping check
    const bounds = map.getBounds();    // Iterate over all configured components
    componentConfigs.forEach((config, processKey) => {
        if (!config.latitude || !config.longitude) return;

        // Determine running state - show all aggregators, but highlight running ones
        let isRunning = false;
        if (config.type === 'server' && runningProcesses.has('server')) {
            isRunning = true;
        } else if (config.type === 'aggregator') {
            // For aggregators, check if this specific aggregator is running
            isRunning = runningProcesses.has(processKey);
        } else if (config.type === 'wavy') {
            // For wavy sensors, check if this specific sensor is running
            isRunning = runningProcesses.has(processKey);
        }

        console.log(`Processing ${config.type} ${config.id}: running=${isRunning}, processKey=${processKey}`);

        // Choose styling based on type
        let color, fillColor, radius, label;
        switch (config.type) {
            case 'server':
                color = '#ff4444'; fillColor = '#fe4544'; radius = 10; label = 'Server';
                break;
            case 'aggregator':
                color = '#1f1f82'; fillColor = '#4445fe'; radius = 8; label = 'Aggregator';
                break;
            case 'wavy':
                color = '#1c821e'; fillColor = '#44ff44'; radius = 6; label = 'Wavy Sensor';
                break;
            default:
                return;
        }

        // Dim color if not running
        if (!isRunning) {
            color = '#888'; fillColor = '#888';
        }

        // Base position and additional positions for wrapping
        const baseLat = config.latitude;
        const baseLng = config.longitude;
        const positions = [];
        // Original position
        positions.push([baseLat, baseLng]);
        // Check for left duplicate (subtract 360°)
        const leftPos = [baseLat, baseLng - 360];
        if (bounds.contains(L.latLng(leftPos))) {
            positions.push(leftPos);
        }
        // Check for right duplicate (add 360°)
        const rightPos = [baseLat, baseLng + 360];
        if (bounds.contains(L.latLng(rightPos))) {
            positions.push(rightPos);
        }

        // Create markers for each valid position
        positions.forEach(pos => {
            const marker = L.circleMarker(pos, {
                color, fillColor, fillOpacity: 0.8, radius, weight: 2
            }).addTo(map);            // Popup with status info
            const statusText = isRunning ? 'Running' : 'Stopped';
            let locationInfo = '';
            if (config.continent) {
                locationInfo = `Continent: ${config.continent}<br>`;
            }
            if (config.ocean) {
                locationInfo += `Ocean: ${config.ocean}<br>`;
            }
            if (config.areaType) {
                locationInfo += `Area: ${config.areaType}<br>`;
            }
            
            const popupContent = `
                <div style="font-family: 'Courier New', monospace; color: #000;">
                    <b>${config.id}</b><br>
                    Type: ${label}<br>
                    ${locationInfo}Status: ${statusText}<br>
                    <small>Lat: ${baseLat.toFixed(4)}, Lng: ${baseLng.toFixed(4)}</small>
                </div>
            `;
            marker.bindPopup(popupContent);
            mapMarkers.push(marker);
        });
    });    // If no markers found, show a message
    if (mapMarkers.length === 0) {
        console.log('No markers created. Component configs available:', componentConfigs.size);
        if (componentConfigs.size === 0) {
            console.log('No configurations loaded. Try opening the map tab to load configurations.');
        }
    } else {
        console.log(`Created ${mapMarkers.length} map markers`);
    }
}

// Test function to verify configurations (can be called from browser console)
window.testMapConfigs = function() {
    console.log('=== Map Configuration Test ===');
    console.log(`Total configurations loaded: ${componentConfigs.size}`);
    console.log(`Running processes: ${runningProcesses.size}`);
    
    componentConfigs.forEach((config, key) => {
        console.log(`${key}: ${config.type} ${config.id} at ${config.latitude}, ${config.longitude}`);
    });
    
    console.log('Running processes:', Array.from(runningProcesses));
};

// Initialize accordion state on page load
document.addEventListener('DOMContentLoaded', () => {
    // Initialize Quick Start accordion as open by default
    const quickStartContent = document.getElementById('quick-start-content');
    const quickStartArrow = document.getElementById('quick-start-arrow');

    if (quickStartContent && quickStartArrow) {
        quickStartContent.classList.remove('collapsed');
        quickStartArrow.classList.remove('rotated');
        quickStartArrow.textContent = '▼';
    }
});

// Component creation functions
function showCreateAggregatorForm() {
    document.getElementById('create-aggregator-modal').style.display = 'flex';
}

function hideCreateAggregatorForm() {
    document.getElementById('create-aggregator-modal').style.display = 'none';
    document.getElementById('create-aggregator-form').reset();
}

function showCreateWavyForm() {
    document.getElementById('create-wavy-modal').style.display = 'flex';
}

function hideCreateWavyForm() {
    document.getElementById('create-wavy-modal').style.display = 'none';
    document.getElementById('create-wavy-form').reset();
}

async function createAggregator(event) {
    event.preventDefault();
    
    const formData = {
        id: document.getElementById('aggr-id').value.trim(),
        region: document.getElementById('aggr-region').value,
        ocean: document.getElementById('aggr-ocean').value,
        areaType: document.getElementById('aggr-area-type').value,
        latitude: parseFloat(document.getElementById('aggr-latitude').value),
        longitude: parseFloat(document.getElementById('aggr-longitude').value),
        dataTypes: Array.from(document.querySelectorAll('#create-aggregator-form .checkbox-group input:checked'))
                        .map(cb => cb.value)
    };

    // Validation
    if (!formData.id || !formData.region || !formData.ocean || !formData.areaType) {
        alert('Please fill in all required fields.');
        return;
    }

    if (isNaN(formData.latitude) || isNaN(formData.longitude)) {
        alert('Please enter valid latitude and longitude values.');
        return;
    }

    if (formData.dataTypes.length === 0) {
        alert('Please select at least one data type.');
        return;
    }

    try {
        const result = await ipcRenderer.invoke('create-aggregator', formData);
        if (result.success) {
            hideCreateAggregatorForm();
            
            // Show success message in terminal
            const terminalContent = document.getElementById('terminal-content');
            terminalContent.innerHTML += `
                <div class="success-text">[MANAGER] Successfully created aggregator ${formData.id}</div>
                <div class="info-text">- Region: ${formData.region}</div>
                <div class="info-text">- Ocean: ${formData.ocean}</div>
                <div class="info-text">- Location: ${formData.latitude}, ${formData.longitude}</div>
                <div class="info-text">- Data Types: ${formData.dataTypes.join(', ')}</div>
                <div class="info-text">You can now start this aggregator using the input field above.</div><br>
            `;
            terminalContent.scrollTop = terminalContent.scrollHeight;
            
            // Pre-fill the aggregator ID input
            setTimeout(() => {
                document.getElementById('aggregator-id').value = formData.id;
            }, 500);
        } else {
            alert(`Failed to create aggregator: ${result.message}`);
        }
    } catch (error) {
        alert(`Error creating aggregator: ${error.message}`);
    }
}

async function createWavy(event) {
    event.preventDefault();
    
    const formData = {
        id: document.getElementById('wavy-id-create').value.trim(),
        latitude: parseFloat(document.getElementById('wavy-latitude').value),
        longitude: parseFloat(document.getElementById('wavy-longitude').value),
        ocean: document.getElementById('wavy-ocean').value,
        areaType: document.getElementById('wavy-area-type').value,
        regionCoverage: document.getElementById('wavy-region-coverage').value.trim(),
        dataInterval: parseInt(document.getElementById('wavy-data-interval').value),
        status: parseInt(document.getElementById('wavy-status').value)
    };

    // Validation
    if (!formData.id || !formData.ocean || !formData.areaType || !formData.regionCoverage) {
        alert('Please fill in all required fields.');
        return;
    }

    if (isNaN(formData.latitude) || isNaN(formData.longitude)) {
        alert('Please enter valid latitude and longitude values.');
        return;
    }

    if (isNaN(formData.dataInterval) || formData.dataInterval < 1000) {
        alert('Data interval must be at least 1000 milliseconds.');
        return;
    }

    try {
        const result = await ipcRenderer.invoke('create-wavy', formData);
        if (result.success) {
            hideCreateWavyForm();
            
            // Show success message in terminal
            const terminalContent = document.getElementById('terminal-content');
            terminalContent.innerHTML += `
                <div class="success-text">[MANAGER] Successfully created wavy sensor ${formData.id}</div>
                <div class="info-text">- Ocean: ${formData.ocean}</div>
                <div class="info-text">- Area Type: ${formData.areaType}</div>
                <div class="info-text">- Location: ${formData.latitude}, ${formData.longitude}</div>
                <div class="info-text">- Region Coverage: ${formData.regionCoverage}</div>
                <div class="info-text">- Data Interval: ${formData.dataInterval}ms</div>
                <div class="info-text">You can now start this wavy sensor using the input field above.</div><br>
            `;
            terminalContent.scrollTop = terminalContent.scrollHeight;
            
            // Pre-fill the wavy ID input
            setTimeout(() => {
                document.getElementById('wavy-id').value = formData.id;
            }, 500);
        } else {
            alert(`Failed to create wavy: ${result.message}`);
        }
    } catch (error) {
        alert(`Error creating wavy: ${error.message}`);
    }
}

// Close modals when clicking outside
document.addEventListener('click', (event) => {
    const aggregatorModal = document.getElementById('create-aggregator-modal');
    const wavyModal = document.getElementById('create-wavy-modal');
    
    if (event.target === aggregatorModal) {
        hideCreateAggregatorForm();
    }
    if (event.target === wavyModal) {
        hideCreateWavyForm();
    }
});

// Dashboard functionality
let dashboardServerRunning = false;
let dashboardProcess = null;

async function openDashboardTab() {
    const modal = document.getElementById('dashboard-modal');
    modal.style.display = 'flex';
    
    // Update dashboard status
    const statusElement = document.getElementById('dashboard-server-status');
    const textElement = document.getElementById('dashboard-server-text');
    
    statusElement.textContent = '⚡';
    textElement.textContent = 'Starting Dashboard Server...';
    
    try {
        // Start the dashboard server via IPC
        const result = await ipcRenderer.invoke('start-dashboard-server');
        
        if (result.success) {
            dashboardServerRunning = true;
            dashboardProcess = result.processId;
            statusElement.textContent = '✅';
            textElement.textContent = 'Dashboard Server Running';
            
            // Wait a moment for server to be fully ready
            setTimeout(() => {
                const iframe = document.getElementById('dashboard-frame');
                iframe.src = 'http://localhost:5001';
            }, 2000);
        } else {
            statusElement.textContent = '❌';
            textElement.textContent = `Error: ${result.error}`;
            
            // Show error message and instructions
            const iframe = document.getElementById('dashboard-frame');
            iframe.srcdoc = `
                <html>
                <head>
                    <style>
                        body { font-family: Arial, sans-serif; padding: 20px; background: #f5f5f5; }
                        .error { background: #ffebee; border: 1px solid #f44336; border-radius: 5px; padding: 15px; margin: 10px 0; }
                        .instructions { background: #e3f2fd; border: 1px solid #2196f3; border-radius: 5px; padding: 15px; margin: 10px 0; }
                        code { background: #eeeeee; padding: 2px 5px; border-radius: 3px; }
                    </style>
                </head>
                <body>
                    <h2>⚠️ Dashboard Server Setup Required</h2>
                    <div class="error">
                        <strong>Error:</strong> ${result.error}
                    </div>
                    <div class="instructions">
                        <h3>🔧 Quick Setup Instructions:</h3>
                        <ol>
                            <li><strong>Install Python:</strong> Download from <a href="https://python.org" target="_blank">python.org</a></li>
                            <li><strong>Run setup script:</strong> <code>npm run setup-dashboard</code></li>
                            <li><strong>Or install manually:</strong> <code>pip install -r requirements.txt</code></li>
                            <li><strong>Try again:</strong> Click "🔄 Refresh" button above</li>
                        </ol>
                        <p><strong>💡 Alternative:</strong> You can also run <code>python dashboard_server.py</code> manually in a terminal, then click "🌐 Open in Browser".</p>
                    </div>
                </body>
                </html>
            `;
        }
    } catch (error) {
        statusElement.textContent = '❌';
        textElement.textContent = `Failed to start server: ${error.message}`;
    }
}

function closeDashboardTab() {
    const modal = document.getElementById('dashboard-modal');
    modal.style.display = 'none';
}

function refreshDashboard() {
    const iframe = document.getElementById('dashboard-frame');
    if (iframe.src) {
        iframe.src = iframe.src; // Reload the iframe
    } else {
        openDashboardTab(); // Restart the server if not running
    }
}

function openDashboardExternal() {
    // Open the dashboard in the user's default browser
    require('electron').shell.openExternal('http://localhost:5001');
}

// Close dashboard modal when clicking outside of it
document.addEventListener('click', (event) => {
    const dashboardModal = document.getElementById('dashboard-modal');
    if (event.target === dashboardModal) {
        closeDashboardTab();
    }
    
    // Existing map modal logic
    const modal = document.getElementById('map-modal');
    if (event.target === modal) {
        closeMapTab();
    }
});
