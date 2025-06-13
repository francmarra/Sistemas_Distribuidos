# Distributed System Validation Script
# This script validates that all components are properly configured and ready

Write-Host "DISTRIBUTED SYSTEM VALIDATION" -ForegroundColor Cyan
Write-Host "====================================" -ForegroundColor Cyan

$ErrorCount = 0

# Check Node.js installation
Write-Host "`nChecking Node.js..." -ForegroundColor Yellow
try {
    $nodeVersion = node --version 2>$null
    if ($nodeVersion) {
        Write-Host "   [OK] Node.js: $nodeVersion" -ForegroundColor Green
    } else {
        Write-Host "   [ERROR] Node.js not found!" -ForegroundColor Red
        $ErrorCount++
    }
} catch {
    Write-Host "   [ERROR] Node.js not found!" -ForegroundColor Red
    $ErrorCount++
}

# Check .NET installation
Write-Host "`nChecking .NET Runtime..." -ForegroundColor Yellow
try {
    $dotnetVersion = dotnet --version 2>$null
    if ($dotnetVersion) {
        Write-Host "   [OK] .NET: $dotnetVersion" -ForegroundColor Green
    } else {
        Write-Host "   [ERROR] .NET not found!" -ForegroundColor Red
        $ErrorCount++
    }
} catch {
    Write-Host "   [ERROR] .NET not found!" -ForegroundColor Red
    $ErrorCount++
}

# Check if npm dependencies are installed
Write-Host "`nChecking NPM Dependencies..." -ForegroundColor Yellow
if (Test-Path "node_modules") {
    Write-Host "   [OK] Node modules installed" -ForegroundColor Green
} else {
    Write-Host "   [WARN] Node modules not found - run 'npm install'" -ForegroundColor Yellow
    $ErrorCount++
}

# Check C# project builds
Write-Host "`nChecking C# Projects..." -ForegroundColor Yellow

$projects = @("Shared", "Servidor", "Agregador", "Wavy")
foreach ($project in $projects) {
    $binPath = "$project\bin\Debug\net9.0\$project.dll"
    if (Test-Path $binPath) {
        Write-Host "   [OK] ${project}: Built" -ForegroundColor Green
    } else {
        Write-Host "   [ERROR] ${project}: Not built" -ForegroundColor Red
        $ErrorCount++
    }
}

# Check Electron files
Write-Host "`nChecking Electron Manager..." -ForegroundColor Yellow
$electronFiles = @("main.js", "index.html", "renderer.js", "package.json")
foreach ($file in $electronFiles) {
    if (Test-Path $file) {
        Write-Host "   [OK] ${file}: Found" -ForegroundColor Green
    } else {
        Write-Host "   [ERROR] ${file}: Missing" -ForegroundColor Red
        $ErrorCount++
    }
}

# Check MongoDB connection (basic check)
Write-Host "`nChecking MongoDB Configuration..." -ForegroundColor Yellow
$mongoConfigPath = "Shared\MongoDB\MongoDBConfig.cs"
if (Test-Path $mongoConfigPath) {
    $mongoConfig = Get-Content $mongoConfigPath -Raw
    if ($mongoConfig -match 'CONNECTION_STRING.*=.*"([^"]+)"') {
        $connectionString = $matches[1]
        if ($connectionString -ne "YOUR_MONGO_CONNECTION_STRING") {
            Write-Host "   [OK] MongoDB: Configuration set" -ForegroundColor Green
        } else {
            Write-Host "   [WARN] MongoDB: Default configuration (update CONNECTION_STRING)" -ForegroundColor Yellow
        }
    } else {
        Write-Host "   [ERROR] MongoDB: Configuration error" -ForegroundColor Red
        $ErrorCount++
    }
} else {
    Write-Host "   [ERROR] MongoDB: Config file missing" -ForegroundColor Red
    $ErrorCount++
}

# Check RabbitMQ configuration
Write-Host "`nChecking RabbitMQ Configuration..." -ForegroundColor Yellow
$rabbitConfigPath = "Shared\RabbitMQ\RabbitMQConfig.cs"
if (Test-Path $rabbitConfigPath) {
    Write-Host "   [OK] RabbitMQ: Configuration found" -ForegroundColor Green
} else {
    Write-Host "   [ERROR] RabbitMQ: Configuration missing" -ForegroundColor Red
    $ErrorCount++
}

# Summary
Write-Host "`nVALIDATION SUMMARY" -ForegroundColor Cyan
Write-Host "====================================" -ForegroundColor Cyan

if ($ErrorCount -eq 0) {
    Write-Host "SUCCESS: All checks passed! System is ready." -ForegroundColor Green
    Write-Host "`nTo start the manager:" -ForegroundColor White
    Write-Host "   • Double-click: start-manager.bat" -ForegroundColor Gray
    Write-Host "   • Command line: npm start" -ForegroundColor Gray
    Write-Host "   • Development: npm run dev" -ForegroundColor Gray
} else {
    Write-Host "WARNING: Found $ErrorCount issue(s) that need attention." -ForegroundColor Yellow
    Write-Host "`nTo fix common issues:" -ForegroundColor White
    Write-Host "   • Install dependencies: npm install" -ForegroundColor Gray
    Write-Host "   • Build C# projects: dotnet build SD_TRABALHO_2.sln" -ForegroundColor Gray
    Write-Host "   • Update MongoDB config: edit Shared\MongoDB\MongoDBConfig.cs" -ForegroundColor Gray
}

Write-Host "`nDocumentation: MANAGER_README.md" -ForegroundColor Magenta
