<#
.SYNOPSIS
    Builds PDFHub (API + React) and copies it into the IIS folder.

.DESCRIPTION
    Phases, each with its own -Skip switch so a failed deploy resumes without repeating the expensive parts:
      1. Build  - npm build of src/PDFHub.Web + dotnet publish of src/PDFHub.Api -> artifacts\PDFHub
      2. Copy   - stop the app pool, mirror the artifact, start the pool again (in finally)
      3. Verify - smoke-test the public URLs

    Run it on the server itself (TargetPath is a local folder, no ServerHost), or from a workstation with
    TargetPath as a share (\\SERVER\c$\inetpub\PDFHub) and -ServerHost for WinRM pool control.
    Run scripts\Setup-PDFHub-IIS.ps1 on the server once before the first deploy.

.EXAMPLE
    .\Deploy-PDFHub.ps1                                   # on the server
.EXAMPLE
    .\Deploy-PDFHub.ps1 -TargetPath \\FACTORY-SRV\c$\inetpub\PDFHub -ServerHost FACTORY-SRV
#>
[CmdletBinding()]
param(
    [string]$TargetPath = 'C:\inetpub\PDFHub',
    # Hostname (not IP) of the IIS server when deploying from another machine.
    [string]$ServerHost,
    [PSCredential]$Credential,
    [string]$AppPath = '/PDFHub',
    [string]$AppPoolName = 'PDFHub',
    # Defaults to http://<server>/PDFHub
    [string]$PublicBaseUrl,
    [switch]$SkipBuild,
    [switch]$SkipCopy,
    [switch]$SkipSmokeTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot
$artifact = Join-Path $repo 'artifacts\PDFHub'
$webRoot = Join-Path $repo 'src\PDFHub.Web'
$appBase = '/' + $AppPath.Trim('/') + '/'
if (-not $PublicBaseUrl) {
    $computer = if ($ServerHost) { $ServerHost } else { $env:COMPUTERNAME }
    $PublicBaseUrl = "http://$computer$($AppPath.TrimEnd('/'))"
}
$PublicBaseUrl = $PublicBaseUrl.TrimEnd('/')

function Write-Step([string]$Message) { Write-Host "`n==> $Message" -ForegroundColor Cyan }

function Invoke-External([string]$FilePath, [string[]]$Arguments, [string]$WorkingDirectory) {
    Push-Location $WorkingDirectory
    try {
        & $FilePath @Arguments
        if ($LASTEXITCODE -ne 0) { throw "Failed ($LASTEXITCODE): $FilePath $($Arguments -join ' ')" }
    } finally { Pop-Location }
}

function Invoke-Mirror([string]$Source, [string]$Destination, [string[]]$ExcludeFiles = @(), [string[]]$ExcludeDirs = @()) {
    New-Item -ItemType Directory -Force -Path $Destination | Out-Null
    $arguments = @($Source, $Destination, '/MIR', '/R:2', '/W:2', '/NFL', '/NDL', '/NJH', '/NJS', '/NP')
    if ($ExcludeFiles.Count) { $arguments += '/XF'; $arguments += $ExcludeFiles }
    if ($ExcludeDirs.Count) { $arguments += '/XD'; $arguments += $ExcludeDirs }
    & robocopy @arguments
    # Robocopy success codes are 0-7 (1 = files copied). Leaving one set would fail the whole script.
    if ($LASTEXITCODE -ge 8) { throw "robocopy failed with exit code $LASTEXITCODE" }
    $global:LASTEXITCODE = 0
}

function Invoke-OnServer([scriptblock]$Script, [object[]]$Arguments) {
    if ($ServerHost) {
        $params = @{ ComputerName = $ServerHost; ScriptBlock = $Script; ArgumentList = $Arguments }
        if ($Credential) { $params.Credential = $Credential }
        Invoke-Command @params
    } else {
        & $Script @Arguments
    }
}

# ------------------------------------------------------------------ 1. Build
if (-not $SkipBuild) {
    Write-Step "Building the React app (base path $appBase)"
    $oldBase = $env:VITE_PDFHUB_APP_BASE_PATH
    try {
        $env:VITE_PDFHUB_APP_BASE_PATH = $appBase
        Invoke-External npm @('ci') $webRoot
        Invoke-External npm @('run', 'build') $webRoot
    } finally { $env:VITE_PDFHUB_APP_BASE_PATH = $oldBase }

    Write-Step 'Publishing the API'
    if (Test-Path $artifact) { Remove-Item $artifact -Recurse -Force }
    Invoke-External dotnet @('publish', 'src\PDFHub.Api\PDFHub.Api.csproj', '-c', 'Release', '-o', $artifact) $repo
    # The API serves the React build from wwwroot.
    Invoke-Mirror (Join-Path $webRoot 'dist') (Join-Path $artifact 'wwwroot')
    Remove-Item (Join-Path $artifact 'appsettings.Development.json') -ErrorAction SilentlyContinue
}

# ------------------------------------------------------------------ 2. Copy
if (-not $SkipCopy) {
    Write-Step "Stopping app pool $AppPoolName"
    # ASP.NET Core keeps its DLLs open; copying over a running pool fails on random files.
    Invoke-OnServer {
        param($pool)
        Import-Module WebAdministration
        if ((Get-WebAppPoolState -Name $pool).Value -ne 'Stopped') {
            Stop-WebAppPool -Name $pool
            $deadline = (Get-Date).AddSeconds(30)
            while ((Get-WebAppPoolState -Name $pool).Value -ne 'Stopped' -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 500 }
        }
    } @($AppPoolName)

    try {
        Write-Step "Copying to $TargetPath"
        # appsettings.Production.json holds the server's settings and logs\ the stdout logs: never overwrite or purge them.
        Invoke-Mirror $artifact $TargetPath -ExcludeFiles @('appsettings.Production.json') -ExcludeDirs @('logs')
    } finally {
        Write-Step "Starting app pool $AppPoolName"
        Invoke-OnServer { param($pool) Import-Module WebAdministration; Start-WebAppPool -Name $pool } @($AppPoolName)
    }
}

# ------------------------------------------------------------------ 3. Verify
if (-not $SkipSmokeTest) {
    Write-Step "Smoke tests against $PublicBaseUrl"
    $checks = @(
        @{ Path = '/'; Expect = 200 },
        @{ Path = '/drawings'; Expect = 200 },          # React deep link
        @{ Path = '/api/session/me'; Expect = 200 },
        @{ Path = '/api/drawings?pageSize=10'; Expect = 200 },
        @{ Path = '/api/no-such-route'; Expect = 404 }
    )
    $failed = 0
    foreach ($check in $checks) {
        $url = "$PublicBaseUrl$($check.Path)"
        try {
            $status = [int](Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 30 -ErrorAction Stop).StatusCode
        } catch {
            $status = if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { 'ERR' }
        }
        $ok = $status -eq $check.Expect
        if (-not $ok) { $failed++ }
        Write-Host ('{0,-5} {1,-4} {2}' -f $(if ($ok) { 'OK' } else { 'FAIL' }), $status, $url) -ForegroundColor $(if ($ok) { 'Green' } else { 'Red' })
    }
    if ($failed) { throw "$failed smoke test(s) failed. See Doc/DEPLOYMENT.md > Troubleshooting." }
}

Write-Step "Deployment complete: $PublicBaseUrl"
