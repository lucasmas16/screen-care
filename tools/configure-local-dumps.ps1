#Requires -RunAsAdministrator
param([switch]$Restore)
$ErrorActionPreference = 'Stop'
$registryPath = 'HKLM:\SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps\ScreenCare.exe'
$localDirectory = Join-Path $env:LOCALAPPDATA 'MonitorPausa'
$backupPath = Join-Path $localDirectory 'local-dumps-backup.clixml'
$names = @('DumpType', 'DumpCount', 'DumpFolder')
if ($Restore) {
    if (-not (Test-Path -LiteralPath $backupPath)) { throw 'Backup anterior não encontrado.' }
    $backup = Import-Clixml -LiteralPath $backupPath
    if (Test-Path -LiteralPath $registryPath) {
        foreach ($name in $names) { Remove-ItemProperty -LiteralPath $registryPath -Name $name -ErrorAction SilentlyContinue }
    }
    foreach ($entry in $backup.Values) {
        New-Item -Path $registryPath -Force | Out-Null
        New-ItemProperty -LiteralPath $registryPath -Name $entry.Name -Value $entry.Value -PropertyType $entry.Kind -Force | Out-Null
    }
    Remove-Item -LiteralPath $backupPath
    Write-Output 'Configuração anterior restaurada. Os dumps existentes foram preservados.'
    return
}
if (Test-Path -LiteralPath $backupPath) { throw 'Já existe um backup. Restaure antes de configurar novamente.' }
New-Item -ItemType Directory -Path $localDirectory -Force | Out-Null
$values = @()
if (Test-Path -LiteralPath $registryPath) {
    $key = Get-Item -LiteralPath $registryPath
    try {
        foreach ($name in $names) {
            if ($key.GetValueNames() -contains $name) {
                $values += [pscustomobject]@{Name=$name; Value=$key.GetValue($name, $null, [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames); Kind=$key.GetValueKind($name).ToString()}
            }
        }
    } finally { $key.Close() }
}
[pscustomobject]@{Values=$values} | Export-Clixml -LiteralPath $backupPath
$dumpDirectory = Join-Path $localDirectory 'dumps'
New-Item -ItemType Directory -Path $dumpDirectory -Force | Out-Null
New-Item -Path $registryPath -Force | Out-Null
New-ItemProperty -LiteralPath $registryPath -Name 'DumpType' -Value 2 -PropertyType DWord -Force | Out-Null
New-ItemProperty -LiteralPath $registryPath -Name 'DumpCount' -Value 3 -PropertyType DWord -Force | Out-Null
New-ItemProperty -LiteralPath $registryPath -Name 'DumpFolder' -Value $dumpDirectory -PropertyType ExpandString -Force | Out-Null
Write-Output 'Dumps locais habilitados para ScreenCare.exe. Use -Restore para restaurar a configuração anterior.'
