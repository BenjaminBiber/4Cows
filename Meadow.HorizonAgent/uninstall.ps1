#Requires -RunAsAdministrator
<#
    Macht install.ps1 rueckgaengig: Dienst stoppen und entfernen, Ereignisquelle entfernen.
    Das Dienstkonto bleibt erhalten, ausser -RemoveUser ist gesetzt.
#>

[CmdletBinding()]
param(
    [string]$ServiceAccount = "MeadowAgent",
    [switch]$RemoveUser
)

$ErrorActionPreference = "Stop"

$ServiceName = "MeadowHorizonAgent"
$EventSource = "Meadow Horizon-Agent"

function Revoke-LogOnAsService {
    param([Parameter(Mandatory)][string]$Sid)

    $exportPath = Join-Path $env:TEMP "meadow-secedit-export-$([guid]::NewGuid()).cfg"
    $dbPath = Join-Path $env:TEMP "meadow-secedit-$([guid]::NewGuid()).sdb"
    try {
        & secedit /export /cfg $exportPath /areas USER_RIGHTS | Out-Null

        $lines = Get-Content -Path $exportPath
        $updated = foreach ($line in $lines) {
            if ($line -match '^\s*SeServiceLogonRight\s*=\s*(.*)$') {
                $remaining = ($Matches[1].Trim() -split ',') | Where-Object { $_ -and $_ -ne "*$Sid" }
                "SeServiceLogonRight = $($remaining -join ',')"
            }
            else {
                $line
            }
        }

        Set-Content -Path $exportPath -Value $updated -Encoding Unicode
        & secedit /configure /db $dbPath /cfg $exportPath /areas USER_RIGHTS /quiet | Out-Null
    }
    finally {
        Remove-Item -Path $exportPath, $dbPath -ErrorAction SilentlyContinue
    }
}

Write-Host "1. Dienst $ServiceName"
$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($service) {
    if ($service.Status -ne "Stopped") {
        Stop-Service -Name $ServiceName
        Write-Host "   Gestoppt."
    }

    & sc.exe delete $ServiceName | Out-Null
    Write-Host "   Entfernt."
}
else {
    Write-Host "   Nicht vorhanden."
}

Write-Host "2. Ereignisquelle $EventSource"
if ([System.Diagnostics.EventLog]::SourceExists($EventSource)) {
    Remove-EventLog -Source $EventSource
    Write-Host "   Entfernt."
}
else {
    Write-Host "   Nicht vorhanden."
}

if ($RemoveUser) {
    Write-Host "3. Dienstkonto $ServiceAccount"
    $existingUser = Get-LocalUser -Name $ServiceAccount -ErrorAction SilentlyContinue
    if ($existingUser) {
        Revoke-LogOnAsService -Sid $existingUser.SID.Value
        Write-Host "   Anmelderecht entfernt."

        Remove-LocalUser -Name $ServiceAccount
        Write-Host "   Konto entfernt."
    }
    else {
        Write-Host "   Nicht vorhanden."
    }
}
else {
    Write-Host "3. Dienstkonto $ServiceAccount bleibt bestehen (-RemoveUser nicht gesetzt)."
}

Write-Host ""
Write-Host "Fertig."
