param(
    [switch]$Click
)

$ErrorActionPreference = "Stop"
$client = [System.Net.Sockets.UdpClient]::new()
$client.Client.ReceiveTimeout = 1500
$endpoint = [System.Net.IPEndPoint]::new(
    [System.Net.IPAddress]::Parse("192.168.4.1"),
    4210)

try {
    $ping = [System.Text.Encoding]::ASCII.GetBytes("COLORDETECTOR_V1 PING")
    [void]$client.Send($ping, $ping.Length, $endpoint)

    $remote = [System.Net.IPEndPoint]::new(
        [System.Net.IPAddress]::Any,
        0)
    $response = $client.Receive([ref]$remote)
    $text = [System.Text.Encoding]::ASCII.GetString($response)
    if ($text -ne "COLORDETECTOR_V1 PONG") {
        throw "Unexpected response: $text"
    }

    Write-Host "Cardputer is online at $($remote.Address)."

    if ($Click) {
        $command = [System.Text.Encoding]::ASCII.GetBytes(
            "COLORDETECTOR_V1 CLICK")
        [void]$client.Send($command, $command.Length, $endpoint)
        $clickResponse = $client.Receive([ref]$remote)
        $clickText = [System.Text.Encoding]::ASCII.GetString($clickResponse)
        if ($clickText -ne "COLORDETECTOR_V1 CLICKED") {
            throw "Unexpected click response: $clickText"
        }

        Write-Host "Test click confirmed by Cardputer."
    }
}
finally {
    $client.Dispose()
}
