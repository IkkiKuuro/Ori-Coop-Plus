$destZip = "c:\Users\raiso\Documents\GitHub\Ori-Coop-Plus\API\Client\BepInEx_x86_5.4.21.0.zip"
$destDir = "c:\Users\raiso\Documents\GitHub\Ori-Coop-Plus\API\Client"

[System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12
$wc = New-Object System.Net.WebClient
$wc.Headers.Add("User-Agent", "Mozilla/5.0")
try {
    $wc.DownloadFile("https://github.com/BepInEx/BepInEx/releases/download/v5.4.21/BepInEx_x86_5.4.21.0.zip", $destZip)
    Write-Output "Downloaded zip successfully."
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [System.IO.Compression.ZipFile]::OpenRead($destZip)
    foreach ($entry in $zip.Entries) {
        if ($entry.Name -eq "BepInEx.dll") {
            $target = Join-Path $destDir "BepInEx.dll"
            [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $target, $true)
            Write-Output "Extracted BepInEx.dll to $target"
        }
    }
    $zip.Dispose()
} catch {
    Write-Output "Error: $($_.Exception.ToString())"
}
