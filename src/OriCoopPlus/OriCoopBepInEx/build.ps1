param(
    [string]$ManagedDir = "",
    [string]$ApiDir = "",
    [string]$OutDir = ""
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$repoRoot = (Resolve-Path (Join-Path $scriptDir "..\..\..")).Path

if ([string]::IsNullOrEmpty($ManagedDir)) {
    $possiblePaths = @(
        $env:ORI_MANAGED_PATH,
        "C:\Program Files (x86)\Steam\steamapps\common\Ori DE\oriDE_Data\Managed",
        "D:\SteamLibrary\steamapps\common\Ori DE\oriDE_Data\Managed"
    )
    foreach ($p in $possiblePaths) {
        if (-not [string]::IsNullOrEmpty($p) -and (Test-Path $p)) {
            $ManagedDir = $p
            break
        }
    }
}

if ([string]::IsNullOrEmpty($ManagedDir) -or (-not (Test-Path $ManagedDir))) {
    Write-Error "Pasta Managed do Ori no foi encontrada. Defina -ManagedDir ou a varivel de ambiente ORI_MANAGED_PATH."
}

if ([string]::IsNullOrEmpty($ApiDir)) {
    $ApiDir = Join-Path $repoRoot "API\Client"
}

if ([string]::IsNullOrEmpty($OutDir)) {
    $OutDir = Join-Path $scriptDir "bin\Release"
}

if (-not (Test-Path $OutDir)) {
    New-Item -ItemType Directory -Path $OutDir -Force | Out-Null
}

$outFile = Join-Path $OutDir "OriCoopBepInEx.dll"

# Localiza csc.exe
$csc = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) {
    Write-Error "csc.exe no foi localizado em $csc."
}

# Resolve referncia a BepInEx.dll e 0Harmony.dll
$bepInExDll = Join-Path $ApiDir "BepInEx.dll"
if (-not (Test-Path $bepInExDll)) {
    $gameBepInEx = Join-Path (Split-Path -Parent $ManagedDir) "..\BepInEx\core\BepInEx.dll"
    if (Test-Path $gameBepInEx) {
        $bepInExDll = (Resolve-Path $gameBepInEx).Path
    }
}

$harmonyDll = Join-Path $ManagedDir "0Harmony.dll"
if (-not (Test-Path $harmonyDll)) {
    $gameHarmony = Join-Path (Split-Path -Parent $ManagedDir) "..\BepInEx\core\0Harmony.dll"
    if (Test-Path $gameHarmony) {
        $harmonyDll = (Resolve-Path $gameHarmony).Path
    } else {
        $harmonyDll = Join-Path $ApiDir "0Harmony.dll"
    }
}

$sourceFiles = Get-ChildItem -Path $scriptDir, (Join-Path $scriptDir "..\OriCoopShared") -Recurse -Filter "*.cs" | 
    Where-Object { $_.FullName -notmatch '\\obj\\' -and $_.FullName -notmatch '\\bin\\' } | 
    Select-Object -ExpandProperty FullName

$tempRsp = [System.IO.Path]::GetTempFileName()
$rspContent = @(
    "/target:library",
    "/out:`"$outFile`"",
    "/nostdlib+",
    "/optimize+",
    "/langversion:default",
    "/r:`"$ManagedDir\mscorlib.dll`"",
    "/r:`"$ManagedDir\System.dll`"",
    "/r:`"$ManagedDir\System.Core.dll`"",
    "/r:`"$ManagedDir\UnityEngine.dll`"",
    "/r:`"$ManagedDir\Assembly-CSharp.dll`"",
    "/r:`"$harmonyDll`"",
    "/r:`"$bepInExDll`""
)

foreach ($src in $sourceFiles) {
    $rspContent += "`"$src`""
}

$rspContent | Set-Content -Encoding utf8 $tempRsp

Write-Output "Compilando OriCoopBepInEx com $($sourceFiles.Count) arquivos de origem..."
$proc = Start-Process -FilePath $csc -ArgumentList "/noconfig @`"$tempRsp`"" -NoNewWindow -Wait -PassThru
Remove-Item $tempRsp -ErrorAction SilentlyContinue

if ($proc.ExitCode -eq 0) {
    $fi = Get-Item $outFile
    Write-Output "SUCESSO: $outFile gerado com $($fi.Length) bytes."
} else {
    Write-Error "Falha na compilao de OriCoopBepInEx (Cdigo $($proc.ExitCode))."
}
