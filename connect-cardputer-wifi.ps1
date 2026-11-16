$ErrorActionPreference = "Stop"

$profilePath = Join-Path $env:TEMP "ColorDetector-M5.xml"
$profile = @'
<?xml version="1.0"?>
<WLANProfile xmlns="http://www.microsoft.com/networking/WLAN/profile/v1">
  <name>ColorDetector-M5</name>
  <SSIDConfig>
    <SSID>
      <name>ColorDetector-M5</name>
    </SSID>
  </SSIDConfig>
  <connectionType>ESS</connectionType>
  <connectionMode>auto</connectionMode>
  <MSM>
    <security>
      <authEncryption>
        <authentication>WPA2PSK</authentication>
        <encryption>AES</encryption>
        <useOneX>false</useOneX>
      </authEncryption>
      <sharedKey>
        <keyType>passPhrase</keyType>
        <protected>false</protected>
        <keyMaterial>ColorDetector-2026</keyMaterial>
      </sharedKey>
    </security>
  </MSM>
</WLANProfile>
'@

Set-Content -LiteralPath $profilePath -Value $profile -Encoding utf8
try {
    netsh wlan add profile filename="$profilePath" user=current
    netsh wlan connect name="ColorDetector-M5" ssid="ColorDetector-M5"
}
finally {
    Remove-Item -LiteralPath $profilePath -Force -ErrorAction SilentlyContinue
}
