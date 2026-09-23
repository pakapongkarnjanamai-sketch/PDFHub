<#
.SYNOPSIS
    Copies PDFHub data off the server: the PDF files and the app's daily database backups.

.DESCRIPTION
    The app already writes one consistent database copy per day to <DataRoot>\backup. This script copies
    those copies and the PDF folder to another disk, a NAS or a USB drive. It never copies the live
    pdfhub.db, which can be mid-write.

    -Register creates a daily Windows scheduled task that runs this script (run as Administrator).

.EXAMPLE
    .\Backup-PDFHubData.ps1 -Destination \\NAS01\Backup\PDFHub
.EXAMPLE
    .\Backup-PDFHubData.ps1 -Destination E:\PDFHubBackup -Register -At 22:00
#>
[CmdletBinding()]
param(
    [string]$DataRoot = 'D:\PDFHubData',
    [Parameter(Mandatory = $true)]
    [string]$Destination,
    [switch]$Register,
    [string]$At = '22:00'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($Register) {
    $action = New-ScheduledTaskAction -Execute 'powershell.exe' `
        -Argument "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`" -DataRoot `"$DataRoot`" -Destination `"$Destination`""
    $trigger = New-ScheduledTaskTrigger -Daily -At $At
    Register-ScheduledTask -TaskName 'PDFHub data backup' -Action $action -Trigger $trigger -User 'SYSTEM' -RunLevel Highest -Force | Out-Null
    Write-Host "Scheduled 'PDFHub data backup' daily at $At -> $Destination" -ForegroundColor Green
    return
}

New-Item -ItemType Directory -Force -Path $Destination | Out-Null
$log = Join-Path $Destination 'backup-log.txt'

# /E adds and updates but never deletes at the destination, so a PDF removed by mistake stays in the backup.
foreach ($pair in @(@('pdf', 'pdf'), @('backup', 'db'))) {
    & robocopy (Join-Path $DataRoot $pair[0]) (Join-Path $Destination $pair[1]) /E /R:2 /W:5 /NP /NDL "/LOG+:$log"
    if ($LASTEXITCODE -ge 8) { throw "robocopy failed with exit code $LASTEXITCODE (see $log)" }
}
$global:LASTEXITCODE = 0
Write-Host "Backup finished -> $Destination" -ForegroundColor Green
