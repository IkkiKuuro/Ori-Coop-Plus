Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::OpenRead("c:\Users\raiso\Documents\GitHub\Ori-Coop-Plus\API\Client\BepInEx.zip")
foreach ($entry in $zip.Entries) {
    if ($entry.FullName.EndsWith(".dll")) {
        Write-Output "DLL: $($entry.FullName)"
    }
}
$zip.Dispose()
