# Builds the single-file app and service executables and the MSI installer.
# Run from the repository root on Windows:  .\build-release.ps1 [-Version 1.2.3]
# The GitHub Actions workflow (.github/workflows/build.yml) runs this same script.
param(
    # Stamped on both executables and the MSI. Defaults to ProductVersion in the .wixproj.
    [string]$Version
)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

if ($Version -and $Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Version must look like 1.2.3 (got '$Version')."
}
$versionArgs = @()
if ($Version) { $versionArgs = @("-p:Version=$Version", "-p:ProductVersion=$Version") }

Remove-Item -Recurse -Force publish -ErrorAction SilentlyContinue

dotnet test
if ($LASTEXITCODE) { throw 'Tests failed.' }

dotnet publish src/NetworkProfiles.App -p:PublishProfile=SingleFile @versionArgs
if ($LASTEXITCODE) { throw 'Publishing the app failed.' }

dotnet publish src/NetworkProfiles.Service -p:PublishProfile=SingleFile @versionArgs
if ($LASTEXITCODE) { throw 'Publishing the service failed.' }

dotnet build installer -c Release @versionArgs
if ($LASTEXITCODE) { throw 'Building the installer failed.' }

Get-ChildItem publish/*.exe, installer/bin/x64/Release/*.msi | Format-Table Name, Length
