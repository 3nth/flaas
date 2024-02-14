param(
    [string] $Account
)

function ReinstallService ($serviceName, $binaryPath, $description, $login, $password, $startUpType)
{
        Write-Host "Trying to create service: $serviceName"

        #Check Parameters
        if ((Test-Path $binaryPath)-eq $false)
        {
            Write-Host "BinaryPath to service not found: $binaryPath"
            Write-Host "Service was NOT installed."
            return
        }

        if (("Automatic", "Manual", "Disabled") -notcontains $startUpType)
        {
            Write-Host "Value for startUpType parameter should be (Automatic or Manual or Disabled) and it was $startUpType"
            Write-Host "Service was NOT installed."
            return
        }

        # Verify if the service already exists, and if yes remove it first
        if (Get-Service $serviceName -ErrorAction SilentlyContinue)
        {
            Stop-Service $serviceName -ErrorAction SilentlyContinue
            Remove-Service -Name $serviceName -ErrorAction SilentlyContinue

            Write-Host "Service removed: $serviceName"
        }

        # if password is empty, create a dummy one to allow have credentias for system accounts: 
        #NT AUTHORITY\LOCAL SERVICE
        #NT AUTHORITY\NETWORK SERVICE
        if ($password -eq "") 
        {
            #$secpassword = (new-object System.Security.SecureString)
            # Bug detected by @GaTechThomas
            $secpasswd = (new-object System.Security.SecureString)
        }
        else
        {
            $secpasswd = ConvertTo-SecureString $password -AsPlainText -Force
        }
        $mycreds = New-Object System.Management.Automation.PSCredential ($login, $secpasswd)

        # Creating Windows Service using all provided parameters
        Write-Host "Installing service: $serviceName"
        New-Service -name $serviceName -binaryPathName $binaryPath -Description $description -displayName $serviceName -startupType $startUpType -credential $login

        Write-Host "Installation completed: $serviceName"

        # Trying to start new service
        Write-Host "Trying to start new service: $serviceName"
        Start-Service $serviceName
        Write-Host "Service started: $serviceName"

        #SmokeTest
        Write-Host "Waiting 5 seconds to give time service to start..."
        Start-Sleep -s 5
        $SmokeTestService = Get-Service -Name $serviceName
        if ($SmokeTestService.Status -ne "Running")
        {
            Write-Host "Smoke test: FAILED. (SERVICE FAILED TO START)"
            Throw "Smoke test: FAILED. (SERVICE FAILED TO START)"
        }
        else
        {
            Write-Host "Smoke test: OK."
        }

}

ReinstallService -serviceName "flaas" -binaryPath "$PSScriptRoot\flaas.exe" -description "Fan Light as a Service" -login $account -password "" -startUpType "Automatic"