param(
    [string] $Account = "LocalSystem"
)

$serviceName = "flaas"
$binaryPath = "$PSScriptRoot\flaas.exe"
$description = "Fan Light as a Service"

if (-not (Test-Path $binaryPath)) {
    Write-Host "Binary not found: $binaryPath"
    exit 1
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
