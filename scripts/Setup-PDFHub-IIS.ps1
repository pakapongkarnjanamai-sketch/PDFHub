<#
.SYNOPSIS
    One-time (and re-runnable) IIS registration for PDFHub. Run ON the server, as Administrator.

.DESCRIPTION
    Idempotent: re-running updates the existing pool and application instead of failing.
    Follows the tools-windows-iis-deploy skill; see Doc/DEPLOYMENT.md for why each setting exists.

.EXAMPLE
    .\Setup-PDFHub-IIS.ps1 -DataRoot D:\PDFHubData
#>
#Requires -RunAsAdministrator
[CmdletBinding()]
param(
    [string]$SiteName = 'Default Web Site',
    # URL path of the app: http://<server>/PDFHub
    [string]$AppPath = '/PDFHub',
    [string]$PhysicalPath = 'C:\inetpub\PDFHub',
    [string]$AppPoolName = 'PDFHub',
    # Database, PDFs and backups. Keep it outside $PhysicalPath so a deploy never touches data.
    [string]$DataRoot = 'D:\PDFHubData'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-Step([string]$Message) { Write-Host "`n==> $Message" -ForegroundColor Cyan }

Write-Step 'Checking prerequisites'
if (-not (Get-Service W3SVC -ErrorAction SilentlyContinue)) {
    throw 'IIS is not installed. Server Manager > Add Roles and Features > Web Server (IIS), then re-run.'
}
$ancm = Join-Path $env:ProgramFiles 'IIS\Asp.Net Core Module\V2\aspnetcorev2.dll'
if (-not (Test-Path $ancm)) {
    throw 'The ASP.NET Core Hosting Bundle (.NET 10) is not installed. Download "Hosting Bundle" from https://dotnet.microsoft.com/download/dotnet/10.0, install it, run "iisreset", then re-run this script.'
}
Import-Module WebAdministration
$appcmd = Join-Path $env:SystemRoot 'System32\inetsrv\appcmd.exe'
$appName = $AppPath.Trim('/')

Write-Step 'Creating folders'
foreach ($dir in @($PhysicalPath, $DataRoot)) {
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
}

Write-Step "App pool $AppPoolName"
if (-not (Test-Path "IIS:\AppPools\$AppPoolName")) {
    New-WebAppPool -Name $AppPoolName | Out-Null
}
$pool = "IIS:\AppPools\$AppPoolName"
Set-ItemProperty $pool -Name managedRuntimeVersion -Value ''          # No Managed Code: ASP.NET Core runs its own runtime
# The daily database backup runs inside the app. Under IIS defaults the pool stops after 20 idle
# minutes (every night), so the backup would never run.
Set-ItemProperty $pool -Name startMode -Value 'AlwaysRunning'
Set-ItemProperty $pool -Name processModel.idleTimeout -Value ([TimeSpan]::Zero)

Write-Step "IIS application $SiteName$AppPath"
# Get-WebApplication, not Test-Path: Test-Path is also true for a plain folder or virtual directory.
$existing = Get-WebApplication -Site $SiteName -Name $appName -ErrorAction SilentlyContinue
if (-not $existing) {
    New-WebApplication -Site $SiteName -Name $appName -PhysicalPath $PhysicalPath -ApplicationPool $AppPoolName | Out-Null
} else {
    Set-ItemProperty "IIS:\Sites\$SiteName\$appName" -Name physicalPath -Value $PhysicalPath
    Set-ItemProperty "IIS:\Sites\$SiteName\$appName" -Name applicationPool -Value $AppPoolName
}
Set-ItemProperty "IIS:\Sites\$SiteName\$appName" -Name preloadEnabled -Value $true

# The app has its own username/password login, so IIS lets every request through.
& $appcmd set config "$SiteName/$appName" '-section:system.webServer/security/authentication/anonymousAuthentication' '-enabled:true' /commit:apphost | Out-Null
& $appcmd set config "$SiteName/$appName" '-section:system.webServer/security/authentication/windowsAuthentication' '-enabled:false' /commit:apphost | Out-Null

Write-Step 'File permissions'
$identity = "IIS AppPool\$AppPoolName"
icacls $PhysicalPath /grant "${identity}:(OI)(CI)RX" /T /C /Q | Out-Null
# Modify, not read: the app writes the database, uploaded PDFs and backups here.
icacls $DataRoot /grant "${identity}:(OI)(CI)M" /T /C /Q | Out-Null

Write-Step 'Server settings (appsettings.Production.json)'
$settings = Join-Path $PhysicalPath 'appsettings.Production.json'
if (-not (Test-Path $settings)) {
    # Deploys never overwrite this file, so it is the place for server-specific values.
    @{ PdfHub = @{ DataRoot = $DataRoot } } | ConvertTo-Json -Depth 3 | Set-Content -Path $settings -Encoding UTF8
    Write-Host "Created $settings"
} else {
    Write-Host "Kept existing $settings"
}

Write-Step 'Restarting app pool'
if ((Get-WebAppPoolState -Name $AppPoolName).Value -eq 'Started') { Restart-WebAppPool -Name $AppPoolName }
else { Start-WebAppPool -Name $AppPoolName }

$port = (Get-WebBinding -Name $SiteName -Protocol http | Select-Object -First 1).bindingInformation.Split(':')[1]
$url = if ($port -eq '80') { "http://$env:COMPUTERNAME$AppPath" } else { "http://${env:COMPUTERNAME}:$port$AppPath" }
Write-Host "`nIIS is ready. Deploy the app with scripts\Deploy-PDFHub.ps1, then open $url" -ForegroundColor Green
