$managed = "C:\Program Files (x86)\Steam\steamapps\common\Ori DE\oriDE_Data\Managed"
$api = "c:\Users\raiso\Documents\GitHub\Ori-Coop-Plus\API\Client"
$outDir = "c:\Users\raiso\Documents\GitHub\Ori-Coop-Plus\src\OriCoopPlus\OriCoopBepInEx\bin\Release"
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir -Force }
$outFile = Join-Path $outDir "OriCoopBepInEx.dll"

$sourceFiles = Get-ChildItem -Path "c:\Users\raiso\Documents\GitHub\Ori-Coop-Plus\src\OriCoopPlus\OriCoopBepInEx", "c:\Users\raiso\Documents\GitHub\Ori-Coop-Plus\src\OriCoopPlus\OriCoopShared" -Recurse -Filter "*.cs" | Where-Object { $_.FullName -notmatch '\\obj\\' -and $_.FullName -notmatch '\\bin\\' } | Select-Object -ExpandProperty FullName

$rsp = @(
    "/target:library",
    "/out:`"$outFile`"",
    "/nostdlib+",
    "/optimize+",
    "/langversion:default",
    "/r:`"$managed\mscorlib.dll`"",
    "/r:`"$managed\System.dll`"",
    "/r:`"$managed\System.Core.dll`"",
    "/r:`"$managed\UnityEngine.dll`"",
    "/r:`"$managed\Assembly-CSharp.dll`"",
    "/r:`"$api\0Harmony.dll`"",
    "/r:`"$api\BepInEx.dll`""
)

foreach ($s in $sourceFiles) {
    $rsp += "`"$s`""
}

$rspFile = "c:\Users\raiso\.gemini\antigravity-ide\brain\c51e8fdd-be3a-4058-8c87-93bed4118004\scratch\build.rsp"
$rsp | Set-Content -Encoding utf8 $rspFile

Write-Output "Running csc with /noconfig..."
$proc = Start-Process -FilePath "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe" -ArgumentList "/noconfig @`"$rspFile`"" -NoNewWindow -Wait -PassThru
if ($proc.ExitCode -eq 0) {
    Write-Output "BUILD SUCCESSFUL! Generated: $outFile (Size: $((Get-Item $outFile).Length) bytes)"
} else {
    Write-Output "BUILD FAILED with exit code $($proc.ExitCode)"
}
