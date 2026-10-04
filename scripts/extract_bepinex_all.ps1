Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::OpenRead("c:\Users\raiso\Documents\GitHub\Ori-Coop-Plus\API\Client\BepInEx.zip")
foreach ($entry in $zip.Entries) {
    if ($entry.Name -like "*BepInEx*") {
        Write-Output "Entry: $($entry.FullName)"
        $dest = "c:\Users\raiso\Documents\GitHub\Ori-Coop-Plus\API\Client\$($entry.Name)"
        if (-not (Test-Path $dest)) {
            [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $dest, $true)
            Write-Output "Extracted: $dest"
        }
    }
}
$zip.Dispose()
