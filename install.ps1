param(
    [string] $Account = "LocalSystem"
)

$serviceName = "flaas"
$binaryPath = "$PSScriptRoot\flaas.exe"
$configPath = "$PSScriptRoot\appsettings.json"
$description = "Fan Light as a Service"

if (-not (Test-Path $binaryPath)) {
    Write-Host "Binary not found: $binaryPath"
    exit 1
}

# Ensure appsettings.json exists
if (-not (Test-Path $configPath)) {
    @{
        SensorName = ""
        Logging = @{ LogLevel = @{ Default = "Information"; "Microsoft.AspNetCore" = "Warning" } }
        AllowedHosts = "*"
    } | ConvertTo-Json -Depth 3 | Set-Content $configPath
    Write-Host "Created default config: $configPath"
}

# Check SensorName configuration
$config = Get-Content $configPath -Raw | ConvertFrom-Json
if ([string]::IsNullOrWhiteSpace($config.SensorName)) {
    Write-Host "SensorName is not configured. Scanning for available sensors..."
    $output = & $binaryPath --list-sensors 2>&1
    $sensors = @($output | Where-Object { $_ -match '^\s+\S' } | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne 'CPU Fan' })

    if ($sensors.Count -eq 0) {
        Write-Host "No control sensors found. Is PawnIO installed?"
        exit 1
    }

    Write-Host ""
    Write-Host "Available control sensors:"
    for ($i = 0; $i -lt $sensors.Count; $i++) {
        Write-Host "  [$($i + 1)] $($sensors[$i])"
    }
    Write-Host ""

    do {
        $selection = Read-Host "Select a sensor (1-$($sensors.Count))"
    } while ($selection -lt 1 -or $selection -gt $sensors.Count)

    $config.SensorName = $sensors[$selection - 1]
    $config | ConvertTo-Json -Depth 3 | Set-Content $configPath
    Write-Host "Set SensorName to: $($config.SensorName)"
}

# Remove existing service
if (Get-Service $serviceName -ErrorAction SilentlyContinue) {
    Write-Host "Stopping and removing existing service..."
    Stop-Service $serviceName -ErrorAction SilentlyContinue
    Remove-Service -Name $serviceName
    Write-Host "Service removed: $serviceName"
}

# Install service
Write-Host "Installing service: $serviceName (account: $Account)"

$serviceParams = @{
    Name           = $serviceName
    BinaryPathName = $binaryPath
    Description    = $description
    DisplayName    = $serviceName
    StartupType    = "Automatic"
}

# LocalSystem doesn't use -Credential; other accounts do
if ($Account -ne "LocalSystem") {
    $serviceParams["Credential"] = $Account
}

New-Service @serviceParams

# Start service
Write-Host "Starting service: $serviceName"
Start-Service $serviceName

# Smoke test
Start-Sleep -s 5
$svc = Get-Service -Name $serviceName
if ($svc.Status -ne "Running") {
    Write-Host "Smoke test: FAILED (service not running)"
    exit 1
}

Write-Host "Smoke test: OK"
