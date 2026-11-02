#Requires -RunAsAdministrator
# Installs (or updates) Network Profiles without the MSI: copies both
# executables to Program Files and registers the background service.
# Run from an administrator PowerShell in the folder with the two .exe files:
#   powershell -ExecutionPolicy Bypass -File .\install-service.ps1
param([string]$Source = $PSScriptRoot)
$ErrorActionPreference = 'Stop'

$name = 'NetworkProfiles'
$target = Join-Path $env:ProgramFiles 'Network Profiles'

foreach ($file in 'NetworkProfiles.exe', 'NetworkProfiles.Service.exe') {
    if (-not (Test-Path (Join-Path $Source $file))) { throw "$file was not found in $Source." }
}

if (Get-Service $name -ErrorAction SilentlyContinue) {
    Stop-Service $name -Force
    sc.exe delete $name | Out-Null
    Start-Sleep -Seconds 2
}

New-Item -ItemType Directory -Force $target | Out-Null
Copy-Item (Join-Path $Source 'NetworkProfiles.exe'), (Join-Path $Source 'NetworkProfiles.Service.exe') $target -Force

New-Service -Name $name `
    -BinaryPathName "`"$(Join-Path $target 'NetworkProfiles.Service.exe')`"" `
    -DisplayName 'Network Profiles Service' `
    -Description 'Applies IP address, gateway and DNS settings for the Network Profiles app.' `
    -StartupType Automatic | Out-Null
Start-Service $name

Write-Host "Installed to $target and started the service. Start the app from $target\NetworkProfiles.exe."
