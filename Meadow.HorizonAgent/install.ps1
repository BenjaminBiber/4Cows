#Requires -RunAsAdministrator
<#
    Richtet den Windows-Dienst "MeadowHorizonAgent" ein: Dienstkonto, Anmelderecht,
    Ereignisquelle, ACL auf appsettings.json und den Dienst selbst. Jeder Schritt ist
    idempotent - ein erneuter Lauf (z.B. nach einem Update) richtet nur nach, was fehlt.
#>

[CmdletBinding()]
param(
    [string]$InstallDir = $PSScriptRoot,
    [string]$ServiceAccount = "MeadowAgent",
    [System.Security.SecureString]$Password
)

$ErrorActionPreference = "Stop"

$ServiceName = "MeadowHorizonAgent"
$EventSource = "Meadow Horizon-Agent"
$UsersGroupSid = "S-1-5-32-545"
$AdministratorsSid = "S-1-5-32-544"
$SystemSid = "S-1-5-18"

function Grant-LogOnAsService {
    param([Parameter(Mandatory)][string]$Sid)

    $exportPath = Join-Path $env:TEMP "meadow-secedit-export-$([guid]::NewGuid()).cfg"
    $dbPath = Join-Path $env:TEMP "meadow-secedit-$([guid]::NewGuid()).sdb"
    try {
        & secedit /export /cfg $exportPath /areas USER_RIGHTS | Out-Null

        $lines = Get-Content -Path $exportPath
        $found = $false
        $updated = foreach ($line in $lines) {
            if ($line -match '^\s*SeServiceLogonRight\s*=\s*(.*)$') {
                $found = $true
                $existing = $Matches[1].Trim()
                if ($existing -split ',' | Where-Object { $_ -eq "*$Sid" }) {
                    $line
                }
                elseif ([string]::IsNullOrWhiteSpace($existing)) {
                    "SeServiceLogonRight = *$Sid"
                }
                else {
                    "SeServiceLogonRight = $existing,*$Sid"
                }
            }
            else {
                $line
            }
        }

        if (-not $found) {
            $updated = foreach ($line in $updated) {
                $line
                if ($line -match '^\[Privilege Rights\]\s*$') {
                    "SeServiceLogonRight = *$Sid"
                }
            }
        }

        Set-Content -Path $exportPath -Value $updated -Encoding Unicode
        & secedit /configure /db $dbPath /cfg $exportPath /areas USER_RIGHTS /quiet | Out-Null
    }
    finally {
        Remove-Item -Path $exportPath, $dbPath -ErrorAction SilentlyContinue
    }
}

Write-Host "1. Dienstkonto $ServiceAccount"
$existingUser = Get-LocalUser -Name $ServiceAccount -ErrorAction SilentlyContinue
if (-not $existingUser) {
    if (-not $Password) {
        $Password = Read-Host -Prompt "Passwort fuer das neue Konto $ServiceAccount" -AsSecureString
    }

    New-LocalUser -Name $ServiceAccount -Password $Password -FullName "Meadow Horizon-Agent Dienstkonto" `
        -Description "Dienstkonto fuer MeadowHorizonAgent - nur lesender Zugriff auf die Horizon-Datenbank." `
        -PasswordNeverExpires -UserMayNotChangePassword -AccountNeverExpires | Out-Null
    Write-Host "   Konto angelegt."
}
else {
    if (-not $Password) {
        $Password = Read-Host -Prompt "Passwort fuer $ServiceAccount (fuer den Dienst, Konto existiert bereits)" -AsSecureString
    }

    Write-Host "   Konto existiert bereits."
}

$accountSid = (Get-LocalUser -Name $ServiceAccount).SID.Value

$usersGroup = Get-LocalGroup | Where-Object { $_.SID.Value -eq $UsersGroupSid }
$isMember = Get-LocalGroupMember -Group $usersGroup -ErrorAction SilentlyContinue |
    Where-Object { $_.SID.Value -eq $accountSid }
if (-not $isMember) {
    Add-LocalGroupMember -Group $usersGroup -Member $ServiceAccount
    Write-Host "   Mitglied von $($usersGroup.Name)."
}
else {
    Write-Host "   Ist bereits Mitglied von $($usersGroup.Name)."
}

Write-Host "2. Anmelderecht 'Als Dienst anmelden'"
Grant-LogOnAsService -Sid $accountSid
Write-Host "   SeServiceLogonRight gesetzt."

Write-Host "3. Ereignisquelle $EventSource"
if (-not [System.Diagnostics.EventLog]::SourceExists($EventSource)) {
    New-EventLog -LogName Application -Source $EventSource
    Write-Host "   Angelegt."
}
else {
    Write-Host "   Existiert bereits."
}

Write-Host "4. ACL auf appsettings.json"
$appSettingsPath = Join-Path $InstallDir "appsettings.json"
if (Test-Path $appSettingsPath) {
    & icacls $appSettingsPath /inheritance:r /grant:r "*${AdministratorsSid}:(F)" "*${SystemSid}:(F)" "${ServiceAccount}:(R)" | Out-Null
    Write-Host "   Gesetzt: Administratoren und SYSTEM voll, $ServiceAccount nur lesend."
}
else {
    Write-Warning "   $appSettingsPath nicht gefunden - ACL uebersprungen."
}

Write-Host "5. Dienst $ServiceName"
$bstr = [System.Runtime.InteropServices.Marshal]::SecureStringToGlobalAllocUnicode($Password)
try {
    $plainPassword = [System.Runtime.InteropServices.Marshal]::PtrToStringUni($bstr)
}
finally {
    [System.Runtime.InteropServices.Marshal]::ZeroFreeGlobalAllocUnicode($bstr)
}

$exePath = Join-Path $InstallDir "Meadow.HorizonAgent.exe"
$binPath = "`"$exePath`""
$existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if (-not $existingService) {
    & sc.exe create $ServiceName binPath= $binPath start= delayed-auto obj= ".\$ServiceAccount" password= $plainPassword DisplayName= "Meadow Horizon-Agent" | Out-Null
    Write-Host "   Angelegt."
}
else {
    & sc.exe config $ServiceName binPath= $binPath start= delayed-auto obj= ".\$ServiceAccount" password= $plainPassword DisplayName= "Meadow Horizon-Agent" | Out-Null
    Write-Host "   Aktualisiert."
}
$plainPassword = $null

& sc.exe failure $ServiceName reset= 86400 actions= restart/60000/restart/60000/restart/60000 | Out-Null
& sc.exe description $ServiceName "Liest Bestandsdaten aus Lely Horizon und meldet sie an Meadow." | Out-Null

Write-Host ""
Write-Host "Fertig. Vor dem ersten Start pruefen:"
Write-Host "  runas /user:$ServiceAccount `"$exePath --dry-run`""
Write-Host "Dann den Dienst starten:"
Write-Host "  Start-Service $ServiceName"
