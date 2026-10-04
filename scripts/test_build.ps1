$managed = "C:\Program Files (x86)\Steam\steamapps\common\Ori DE\oriDE_Data\Managed"
$api = "c:\Users\raiso\Documents\GitHub\Ori-Coop-Plus\API\Client"
$outDir = "c:\Users\raiso\Documents\GitHub\Ori-Coop-Plus\src\OriCoopPlus\OriCoopBepInEx\bin\Release"
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir -Force }
$outFile = Join-Path $outDir "OriCoopBepInEx.dll"

$sourceFiles = Get-ChildItem -Path "c:\Users\raiso\Documents\GitHub\Ori-Coop-Plus\src\OriCoopPlus\OriCoopBepInEx", "c:\Users\raiso\Documents\GitHub\Ori-Coop-Plus\src\OriCoopPlus\OriCoopShared" -Recurse -Filter "*.cs" | Where-Object { $_.FullName -notmatch '\\obj\\' -and $_.FullName -notmatch '\\bin\\' } | Select-Object -ExpandProperty FullName

$refs = @(
    "$managed\mscorlib.dll",
    "$managed\System.dll",
    "$managed\System.Core.dll",
    "$managed\UnityEngine.dll",
    "$managed\Assembly-CSharp.dll",
    "$api\0Harmony.dll",
    "$api\BepInEx.dll"
)

$argList = @(
    "/target:library",
    "/out:$outFile",
    "/nostdlib+",
    "/noconfig",
    "/optimize+",
    "/langversion:default"
)
foreach ($r in $refs) {
    $argList += "/r:$r"
}
foreach ($s in $sourceFiles) {
    $argList += "$s"
}

Write-Output "Running csc with $($sourceFiles.Count) source files and $($refs.Count) references..."
$proc = Start-Process -FilePath "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe" -ArgumentList $argList -NoNewWindow -Wait -PassThru
if ($proc.ExitCode -eq 0) {
    Write-Output "BUILD SUCCESSFUL! Generated: $outFile (Size: $((Get-Item $outFile).Length) bytes)"
} else {
    Write-Output "BUILD FAILED with exit code $($proc.ExitCode)"
}
