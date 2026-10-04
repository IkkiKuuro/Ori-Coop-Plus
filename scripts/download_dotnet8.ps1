$url = "https://aka.ms/dotnet/8.0/dotnet-runtime-win-x64.exe"
$dest = "c:\Users\raiso\Documents\GitHub\Ori-Coop-Plus\src\OriCoopDedicatedServer\OriCoopDedicatedServer\bin\Release\net8.0\dotnet-8-runtime-installer.exe"

[System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12
$wc = New-Object System.Net.WebClient
$wc.Headers.Add("User-Agent", "Mozilla/5.0")
try {
    Write-Output "Downloading .NET 8 Runtime installer..."
    $wc.DownloadFile($url, $dest)
    Write-Output "Downloaded successfully: $dest ($((Get-Item $dest).Length) bytes)"
} catch {
    Write-Output "Error: $($_.Exception.Message)"
}
