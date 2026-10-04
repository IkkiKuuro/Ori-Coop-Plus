param (
    [string]$GameDir = "C:\Program Files (x86)\Steam\steamapps\common\Ori DE"
)

$ErrorActionPreference = "Stop"

$cecilPath = Join-Path $GameDir "BepInEx\core\Mono.Cecil.dll"
if (-not (Test-Path $cecilPath)) {
    Write-Error "Mono.Cecil.dll nao encontrada em: $cecilPath. Verifique se o BepInEx 5.4.x x86 esta instalado."
    exit 1
}

[System.Reflection.Assembly]::Load([System.IO.File]::ReadAllBytes($cecilPath)) | Out-Null
Write-Output "Mono.Cecil carregado com sucesso."

$destPath = Join-Path $GameDir "oriDE_Data\Managed\UnityEngine.dll"
$backupPath = Join-Path $GameDir "oriDE_Data\Managed\UnityEngine.dll.bak"

if (Test-Path $destPath) {
    if (-not (Test-Path $backupPath)) {
        Copy-Item $destPath $backupPath -Force
        Write-Output "Backup de UnityEngine.dll criado em: $backupPath"
    }
}

# Remove 0Harmony antigo do Managed caso exista
$oldHarmony = Join-Path $GameDir "oriDE_Data\Managed\0Harmony.dll"
if (Test-Path $oldHarmony) {
    $disabledPlugins = Join-Path $GameDir "disabled_plugins"
    if (-not (Test-Path $disabledPlugins)) { New-Item -ItemType Directory -Path $disabledPlugins -Force | Out-Null }
    Move-Item $oldHarmony (Join-Path $disabledPlugins "0Harmony_OldManaged.dll") -Force
    Write-Output "0Harmony.dll obsoleto movido de Managed para disabled_plugins."
}

# Inspeciona e limpa injecoes de mod loaders antigos (ex: WWClient)
$asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($destPath)
$mb = $asm.MainModule.GetType("UnityEngine.MonoBehaviour")

$changed = $false
if ($mb -ne $null) {
    $awake = $mb.Methods | Where-Object { $_.Name -eq "Awake" } | Select-Object -First 1
    if ($awake -ne $null) {
        $mb.Methods.Remove($awake) | Out-Null
        Write-Output "Removido metodo injetado 'Awake' de UnityEngine.MonoBehaviour."
        $changed = $true
    }
}

$wwRef = $asm.MainModule.AssemblyReferences | Where-Object { $_.Name -eq "WWClient" } | Select-Object -First 1
if ($wwRef -ne $null) {
    $asm.MainModule.AssemblyReferences.Remove($wwRef) | Out-Null
    Write-Output "Removida referencia ao assembly 'WWClient'."
    $changed = $true
}

$mscor4Ref = $asm.MainModule.AssemblyReferences | Where-Object { $_.Name -eq "mscorlib" -and $_.Version.Major -eq 4 } | Select-Object -First 1
if ($mscor4Ref -ne $null) {
    $asm.MainModule.AssemblyReferences.Remove($mscor4Ref) | Out-Null
    Write-Output "Removida referencia indevida ao assembly 'mscorlib 4.0'."
    $changed = $true
}

if ($changed) {
    $tempOutput = $destPath + ".tmp"
    $asm.Write($tempOutput)
    $asm.Dispose()
    Move-Item $tempOutput $destPath -Force
    Write-Output "UnityEngine.dll purificada e salva com sucesso em: $destPath"
} else {
    $asm.Dispose()
    Write-Output "UnityEngine.dll ja se encontra limpa (nenhuma injecao detectada)."
}
