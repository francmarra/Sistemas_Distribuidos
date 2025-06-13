const { app, BrowserWindow, ipcMain, Menu } = require('electron');
const { spawn } = require('child_process');
const path = require('path');
const fs = require('fs');

let mainWindow;
let processes = new Map();

function createWindow() {
    // Remove default menu bar
    Menu.setApplicationMenu(null);
    
    mainWindow = new BrowserWindow({
        width: 1200,
        height: 800,
        webPreferences: {
            nodeIntegration: true,
            contextIsolation: false
        },
        show: false,
        backgroundColor: '#000000',
        titleBarStyle: 'default',
        autoHideMenuBar: true
    });

    mainWindow.loadFile('index.html');
    
    mainWindow.once('ready-to-show', () => {
        mainWindow.show();
        
        // Auto-start server after window is shown
        setTimeout(() => {
            const result = startProcess('server', 'Servidor', ['run']);
            if (result.success && mainWindow) {
                mainWindow.webContents.send('server-auto-started');
            }
        }, 1500); // Give UI time to initialize
    });

    mainWindow.on('closed', () => {
        // Terminate all running processes when main window is closed
        processes.forEach((process, id) => {
            if (process && !process.killed) {
                process.kill('SIGTERM');
            }
        });
        processes.clear();
        mainWindow = null;
    });
}

app.whenReady().then(() => {
    createWindow();

    app.on('activate', () => {
        if (BrowserWindow.getAllWindows().length === 0) {
            createWindow();
        }
    });
});

app.on('window-all-closed', () => {
    if (process.platform !== 'darwin') {
        app.quit();
    }
});

// IPC handlers for starting processes
ipcMain.handle('start-server', () => {
    return startProcess('server', 'Servidor', ['run']);
});

ipcMain.handle('start-server-instance', (event, serverId) => {
    return startProcess(`server-${serverId}`, 'Servidor', ['run'], serverId);
});

ipcMain.handle('start-aggregator', (event, aggregatorId) => {
    return startProcess(`aggregator-${aggregatorId}`, 'Agregador', ['run'], aggregatorId);
});

ipcMain.handle('start-wavy', (event, wavyId) => {
    return startProcess(`wavy-${wavyId}`, 'Wavy', ['run'], wavyId);
});

ipcMain.handle('start-regional-components', async (event, regionCode) => {
    const results = [];    // Regional mapping based on the actual config files
    // Wavys can connect to multiple aggregators based on ocean/region coverage
    const regionMappings = {
        'EU': {
            serverId: 'EU-S',
            aggregators: ['EU-Agr01', 'EU-Agr02', 'EU-Agr03', 'EU-Agr04'],
            wavys: [
                // Atlantic coastal/open (EU coverage)
                'Wavy01', 'Wavy02', 'Wavy06',
                // Arctic (EU coverage) 
                'Wavy24'
            ]
        },
        'NA': {
            serverId: 'NA-S',
            aggregators: ['NA-Agr01', 'NA-Agr02', 'NA-Agr03', 'NA-Agr04', 'NA-Agr05'],
            wavys: [
                // Atlantic coastal (North America East Coast)
                'Wavy05',
                // Pacific coastal (North America West Coast)
                'Wavy13',
                // Arctic (North America Arctic)
                'Wavy23',
                // Additional Atlantic/Pacific coverage
                'Wavy01', 'Wavy09', 'Wavy30'
            ]
        },
        'SA': {
            serverId: 'SA-S',
            aggregators: ['SA-Agr01', 'SA-Agr02', 'SA-Agr03', 'SA-Agr04'],
            wavys: [
                // Atlantic coastal (South America East Coast)
                'Wavy08',
                // Pacific coastal (South America West Coast)  
                'Wavy14',
                // Atlantic/Pacific open waters near SA
                'Wavy03', 'Wavy04', 'Wavy11', 'Wavy12'
            ]
        },
        'AF': {
            serverId: 'AF-S',
            aggregators: ['AF-Agr01', 'AF-Agr02', 'AF-Agr03', 'AF-Agr04'],
            wavys: [
                // Atlantic coastal (Africa West Coast)
                'Wavy07',
                // Indian coastal (Africa East Coast)
                'Wavy21',
                // Atlantic/Indian open waters near Africa
                'Wavy03', 'Wavy04', 'Wavy17', 'Wavy18'
            ]
        },
        'AS': {
            serverId: 'AS-S',
            aggregators: ['AS-Agr01', 'AS-Agr02', 'AS-Agr03', 'AS-Agr04', 'AS-Agr05'],
            wavys: [
                // Pacific coastal (Asia East Coast)
                'Wavy15',
                // Indian coastal (Asia Southwest Coast)
                'Wavy20',
                // Arctic (Asia Arctic)
                'Wavy25',
                // Pacific/Indian open waters near Asia
                'Wavy10', 'Wavy17', 'Wavy19', 'Wavy31', 'Wavy32'
            ]
        },
        'OC': {
            serverId: 'OC-S',
            aggregators: ['OC-Agr01', 'OC-Agr02', 'OC-Agr03', 'OC-Agr04', 'OC-Agr05'],
            wavys: [
                // Pacific coastal (Oceania East Coast)
                'Wavy16',
                // Indian coastal (Oceania West Coast)
                'Wavy22',
                // Pacific/Indian/Southern open waters near Oceania
                'Wavy12', 'Wavy19', 'Wavy27', 'Wavy28'
            ]
        },
        'AQ': {
            serverId: 'AQ-S',
            aggregators: ['AQ-Agr01'],
            wavys: [
                // Southern Ocean and Antarctica
                'Wavy26', 'Wavy27', 'Wavy28', 'Wavy29'
            ]
        }
    };

    const regionConfig = regionMappings[regionCode];
    if (!regionConfig) {
        return { success: false, message: `Invalid region code: ${regionCode}` };
    }    try {
        // Start a dedicated server instance for this region
        const serverProcessId = `server-${regionConfig.serverId}`;
        const serverResult = startProcess(serverProcessId, 'Servidor', ['run'], regionConfig.serverId);
        results.push({ type: 'server', id: regionConfig.serverId, processId: serverProcessId, ...serverResult });
        
        // Wait for server to initialize before starting other components
        await new Promise(resolve => setTimeout(resolve, 3000));

        // Start aggregators
        for (const aggregatorId of regionConfig.aggregators) {
            const result = startProcess(`aggregator-${aggregatorId}`, 'Agregador', ['run'], aggregatorId);
            results.push({ type: 'aggregator', id: aggregatorId, ...result });
            
            // Small delay between starts
            await new Promise(resolve => setTimeout(resolve, 1500));
        }

        // Start wavy sensors
        for (const wavyId of regionConfig.wavys) {
            const result = startProcess(`wavy-${wavyId}`, 'Wavy', ['run'], wavyId);
            results.push({ type: 'wavy', id: wavyId, ...result });
            
            // Small delay between starts
            await new Promise(resolve => setTimeout(resolve, 1000));
        }

        return { success: true, results, regionCode };
    } catch (error) {
        return { success: false, message: `Failed to start regional components: ${error.message}` };
    }
});

ipcMain.handle('stop-process', (event, processId) => {
    return stopProcess(processId);
});

ipcMain.handle('get-processes', () => {
    const processArray = [];
    processes.forEach((process, id) => {
        processArray.push({
            id: id,
            alive: process && !process.killed
        });
    });
    return processArray;
});

ipcMain.handle('send-input', (event, processId, command) => {
    const process = processes.get(processId);
    if (process && !process.killed && process.stdin && !process.stdin.destroyed) {
        try {
            process.stdin.write(command + '\n');
            return true;
        } catch (error) {
            console.error(`Error sending input to ${processId}:`, error);
            return false;
        }
    }
    return false;
});

// Component creation handlers
ipcMain.handle('create-aggregator', async (event, formData) => {
    try {
        const configPath = path.join(__dirname, 'configs', 'config_aggregator_subscriptions.csv');
        
        // Insert into MongoDB first to get the generated ID
        let finalId = formData.id;
        let mongoResult = null;
        try {
            mongoResult = await insertIntoMongoDB('aggregator', formData);
            if (mongoResult.generatedId) {
                finalId = mongoResult.generatedId;
                console.log(`Generated aggregator ID: ${finalId}`);
            }
        } catch (mongoError) {
            console.error('MongoDB insertion failed:', mongoError);
            return { success: false, message: `Failed to create aggregator: ${mongoError.message}` };
        }
        
        // Read existing config
        let configContent = '';
        if (fs.existsSync(configPath)) {
            configContent = fs.readFileSync(configPath, 'utf8');
        } else {
            // Create file with header if it doesn't exist
            configContent = 'AggregatorId,Region,Ocean,AreaType,Latitude,Longitude,SubscribedDataTypes\n';
        }
        
        // Check if aggregator ID already exists in CSV
        const lines = configContent.split('\n');
        const existingIds = lines.slice(1).map(line => line.split(',')[0]).filter(id => id.trim());
        
        if (existingIds.includes(finalId)) {
            return { success: false, message: `Aggregator ID ${finalId} already exists in CSV` };
        }
        
        // Create new aggregator entry
        const dataTypesString = `"${formData.dataTypes.join(',')}"`;
        const newEntry = `${finalId},${formData.region},${formData.ocean},${formData.areaType},${formData.latitude},${formData.longitude},${dataTypesString}\n`;
          // Append to config file
        fs.appendFileSync(configPath, newEntry);
        
        return { 
            success: true, 
            message: `Aggregator ${finalId} created successfully`,
            generatedId: finalId
        };
    } catch (error) {
        console.error('Error creating aggregator:', error);
        return { success: false, message: `Failed to create aggregator: ${error.message}` };
    }
});

ipcMain.handle('create-wavy', async (event, formData) => {
    try {
        const configPath = path.join(__dirname, 'configs', 'config_wavy_oceanographic.csv');
        
        // Insert into MongoDB first to get the generated ID
        let finalId = formData.id;
        let mongoResult = null;
        try {
            mongoResult = await insertIntoMongoDB('wavy', formData);
            if (mongoResult.generatedId) {
                finalId = mongoResult.generatedId;
                console.log(`Generated wavy ID: ${finalId}`);
            }
        } catch (mongoError) {
            console.error('MongoDB insertion failed:', mongoError);
            return { success: false, message: `Failed to create wavy: ${mongoError.message}` };
        }
        
        // Read existing config
        let configContent = '';
        if (fs.existsSync(configPath)) {
            configContent = fs.readFileSync(configPath, 'utf8');
        } else {
            // Create file with header if it doesn't exist
            configContent = 'WAVY_ID,status,last_sync,data_interval,is_active,latitude,longitude,ocean,area_type,region_coverage\n';
        }
        
        // Check if wavy ID already exists in CSV
        const lines = configContent.split('\n');
        const existingIds = lines.slice(1).map(line => line.split(',')[0]).filter(id => id.trim());
        
        if (existingIds.includes(finalId)) {
            return { success: false, message: `Wavy ID ${finalId} already exists in CSV` };
        }
        
        // Create new wavy entry
        const timestamp = new Date().toISOString();
        const isActive = formData.status === 1 ? 'true' : 'false';
        const newEntry = `${finalId},${formData.status},${timestamp},${formData.dataInterval},${isActive},${formData.latitude},${formData.longitude},${formData.ocean},${formData.areaType},"${formData.regionCoverage}"\n`;
          // Append to config file
        fs.appendFileSync(configPath, newEntry);
        
        return { 
            success: true, 
            message: `Wavy ${finalId} created successfully`,
            generatedId: finalId
        };
    } catch (error) {
        console.error('Error creating wavy:', error);
        return { success: false, message: `Failed to create wavy: ${error.message}` };
    }
});

// Helper function to insert into MongoDB using ComponentCreator
async function insertIntoMongoDB(componentType, formData) {
    return new Promise((resolve, reject) => {
        const componentCreatorPath = path.join(__dirname, 'ComponentCreator', 'bin', 'Debug', 'net9.0', 'ComponentCreator.exe');
        const jsonData = JSON.stringify(formData);
        
        console.log(`Inserting ${componentType} into MongoDB:`, jsonData);
        
        const process = spawn(componentCreatorPath, [componentType, jsonData], {
            stdio: ['pipe', 'pipe', 'pipe']
        });
        
        let stdout = '';
        let stderr = '';
        
        process.stdout.on('data', (data) => {
            stdout += data.toString();
        });
        
        process.stderr.on('data', (data) => {
            stderr += data.toString();
        });
        
        process.on('close', (code) => {
            if (code === 0) {
                console.log(`MongoDB insertion successful: ${stdout.trim()}`);
                
                // Extract generated ID if present
                let generatedId = null;
                const lines = stdout.trim().split('\n');
                for (const line of lines) {
                    if (line.startsWith('GENERATED_ID:')) {
                        generatedId = line.substring('GENERATED_ID:'.length);
                        break;
                    }
                }
                
                resolve({ 
                    success: true, 
                    message: stdout.trim(),
                    generatedId: generatedId
                });
            } else {
                console.error(`MongoDB insertion failed (code ${code}): ${stderr}`);
                reject(new Error(`ComponentCreator failed: ${stderr || stdout}`));
            }
        });
        
        process.on('error', (error) => {
            console.error('Failed to start ComponentCreator:', error);
            reject(error);
        });
    });
}

function startProcess(processId, component, args, componentId = null) {
    if (processes.has(processId)) {
        return { success: false, message: `Process ${processId} is already running` };
    }

    try {
        const cwd = path.join(__dirname, component);
        
        // Check if directory exists
        if (!fs.existsSync(cwd)) {
            return { success: false, message: `Directory ${cwd} does not exist` };
        }

        const dotnetProcess = spawn('dotnet', args, {
            cwd: cwd,
            stdio: ['pipe', 'pipe', 'pipe'],
            env: { ...process.env, FORCE_COLOR: '0' } // Disable colors for cleaner output
        });

        processes.set(processId, dotnetProcess);

        // Send startup message
        if (mainWindow) {
            mainWindow.webContents.send('process-output', processId, 
                `[MANAGER] Starting ${component}${componentId ? ` with ID: ${componentId}` : ''}...\n`);
        }

        // Send output to renderer
        dotnetProcess.stdout.on('data', (data) => {
            if (mainWindow) {
                const output = data.toString().replace(/\r\n/g, '\n');
                mainWindow.webContents.send('process-output', processId, output);
            }
        });

        dotnetProcess.stderr.on('data', (data) => {
            if (mainWindow) {
                const output = data.toString().replace(/\r\n/g, '\n');
                mainWindow.webContents.send('process-output', processId, `[ERROR] ${output}`);
            }
        });

        dotnetProcess.on('error', (error) => {
            if (mainWindow) {
                mainWindow.webContents.send('process-output', processId, 
                    `[ERROR] Failed to start process: ${error.message}\n`);
                mainWindow.webContents.send('process-closed', processId);
            }
            processes.delete(processId);
        });

        dotnetProcess.on('close', (code) => {
            if (mainWindow) {
                const exitMessage = code === 0 ? 
                    `[MANAGER] Process exited normally\n` : 
                    `[MANAGER] Process exited with code ${code}\n`;
                mainWindow.webContents.send('process-output', processId, exitMessage);
                mainWindow.webContents.send('process-closed', processId);
            }
            processes.delete(processId);
        });

        // If component requires an ID, send it automatically after a delay
        if (componentId) {
            setTimeout(() => {
                if (dotnetProcess.stdin && !dotnetProcess.killed) {
                    try {
                        dotnetProcess.stdin.write(componentId + '\n');
                        if (mainWindow) {
                            mainWindow.webContents.send('process-output', processId, 
                                `[MANAGER] Sent ID: ${componentId}\n`);
                        }
                    } catch (error) {
                        console.error('Error sending component ID:', error);
                    }
                }
            }, 2000); // Increased delay to ensure process is ready
        }

        return { success: true, message: `Started ${component} with ID ${processId}` };
    } catch (error) {
        return { success: false, message: `Failed to start ${component}: ${error.message}` };
    }
}

function stopProcess(processId) {
    const process = processes.get(processId);
    if (process && !process.killed) {
        try {
            if (mainWindow) {
                mainWindow.webContents.send('process-output', processId, 
                    '[MANAGER] Attempting graceful shutdown...\n');
            }

            // Send DLG command first for graceful shutdown (for Wavy and Agregador)
            if (process.stdin && !process.stdin.destroyed) {
                try {
                    process.stdin.write('DLG\n');
                } catch (error) {
                    console.log('Error sending DLG command:', error.message);
                }
            }
            
            // Force kill after 5 seconds if still running
            const forceKillTimer = setTimeout(() => {
                if (!process.killed) {
                    if (mainWindow) {
                        mainWindow.webContents.send('process-output', processId, 
                            '[MANAGER] Force terminating process...\n');
                    }
                    try {
                        process.kill('SIGTERM');
                        // If SIGTERM doesn't work on Windows, try SIGKILL
                        setTimeout(() => {
                            if (!process.killed) {
                                process.kill('SIGKILL');
                            }
                        }, 2000);
                    } catch (error) {
                        console.error('Error force killing process:', error);
                    }
                }
            }, 5000);

            // Clear the timer if process exits gracefully
            process.on('close', () => {
                clearTimeout(forceKillTimer);
            });
            
            return { success: true, message: `Stopping process ${processId}` };
        } catch (error) {
            return { success: false, message: `Failed to stop process ${processId}: ${error.message}` };
        }
    }
    return { success: false, message: `Process ${processId} not found or already stopped` };
}
