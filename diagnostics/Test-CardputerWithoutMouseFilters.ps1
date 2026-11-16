$ErrorActionPreference = 'Stop'

$classKey =
    'HKLM:\SYSTEM\CurrentControlSet\Control\Class\' +
    '{4D36E96F-E325-11CE-BFC1-08002BE10318}'
$deviceId =
    'HID\VID_303A&PID_4046&MI_00\8&DA50CBC&0&0000'
$logPath = Join-Path $PSScriptRoot 'mouse-filter-isolation.log'

$originalFilters =
    (Get-ItemProperty -LiteralPath $classKey -Name UpperFilters).
        UpperFilters

try {
    Set-ItemProperty `
        -LiteralPath $classKey `
        -Name UpperFilters `
        -Type MultiString `
        -Value @('mouclass')

    Disable-PnpDevice `
        -InstanceId $deviceId `
        -Confirm:$false
    Start-Sleep -Seconds 1
    Enable-PnpDevice `
        -InstanceId $deviceId `
        -Confirm:$false
    Start-Sleep -Seconds 2

    $stack =
        (Get-PnpDeviceProperty `
            -InstanceId $deviceId `
            -KeyName DEVPKEY_Device_Stack).Data

    @(
        "Device: $deviceId"
        "Temporary stack: $($stack -join ' | ')"
        "Result: Cardputer restarted without class mouse filters."
    ) | Set-Content -LiteralPath $logPath
}
catch {
    @(
        "Device: $deviceId"
        "Result: FAILED"
        $_.Exception.ToString()
    ) | Set-Content -LiteralPath $logPath
    throw
}
finally {
    Set-ItemProperty `
        -LiteralPath $classKey `
        -Name UpperFilters `
        -Type MultiString `
        -Value $originalFilters
}
