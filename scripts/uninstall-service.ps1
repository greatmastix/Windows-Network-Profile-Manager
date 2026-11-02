#Requires -RunAsAdministrator
# Removes what install-service.ps1 set up. Your profiles in %APPDATA%\NetworkProfiles stay.
$ErrorActionPreference = 'Stop'
$name = 'NetworkProfiles'

if (Get-Service $name -ErrorAction SilentlyContinue) {
    Stop-Service $name -Force
    sc.exe delete $name | Out-Null
}
Remove-Item -Recurse -Force (Join-Path $env:ProgramFiles 'Network Profiles') -ErrorAction SilentlyContinue
Write-Host 'Network Profiles service removed.'
