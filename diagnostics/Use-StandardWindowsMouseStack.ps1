$ErrorActionPreference = 'Stop'

$classKey =
    'HKLM:\SYSTEM\CurrentControlSet\Control\Class\' +
    '{4D36E96F-E325-11CE-BFC1-08002BE10318}'
$logPath = Join-Path $PSScriptRoot 'standard-mouse-stack.log'

$currentFilters =
    (Get-ItemProperty -LiteralPath $classKey -Name UpperFilters).
        UpperFilters

Set-ItemProperty `
    -LiteralPath $classKey `
    -Name UpperFilters `
    -Type MultiString `
    -Value @('mouclass')

@(
    "Previous filters: $($currentFilters -join ', ')"
    'Current filters: mouclass'
    'Interception and Custom Curve mouse filters are disabled for future device stacks.'
    'Backup: diagnostics\mouse-class-backup.reg'
) | Set-Content -LiteralPath $logPath
