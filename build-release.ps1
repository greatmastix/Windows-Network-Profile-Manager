# Builds the single-file app and service executables and the MSI installer.
# Run from the repository root on Windows:  .\build-release.ps1
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

Remove-Item -Recurse -Force publish -ErrorAction SilentlyContinue

dotnet test
if ($LASTEXITCODE) { throw 'Tests failed.' }

dotnet publish src/NetworkProfiles.App -p:PublishProfile=SingleFile
if ($LASTEXITCODE) { throw 'Publishing the app failed.' }

dotnet publish src/NetworkProfiles.Service -p:PublishProfile=SingleFile
if ($LASTEXITCODE) { throw 'Publishing the service failed.' }

dotnet build installer -c Release
if ($LASTEXITCODE) { throw 'Building the installer failed.' }

Get-ChildItem publish/*.exe, installer/bin/x64/Release/*.msi | Format-Table Name, Length
