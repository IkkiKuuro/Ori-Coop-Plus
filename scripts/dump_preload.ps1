$managedDir = "C:\Program Files (x86)\Steam\steamapps\common\Ori DE\oriDE_Data\Managed"
$apiDir = "c:\Users\raiso\Documents\GitHub\Ori-Coop-Plus\API\Client"

# Pre-load dependencies into AppDomain
$unity = [System.Reflection.Assembly]::LoadFrom((Join-Path $managedDir "UnityEngine.dll"))
$asmcsharp = [System.Reflection.Assembly]::LoadFrom((Join-Path $managedDir "Assembly-CSharp.dll"))
$wwclient = [System.Reflection.Assembly]::LoadFrom((Join-Path $apiDir "WWClient.dll"))
$harmony = [System.Reflection.Assembly]::LoadFrom((Join-Path $apiDir "0Harmony.dll"))

[System.AppDomain]::CurrentDomain.add_AssemblyResolve({
    param($s, $e)
    $n = (New-Object System.Reflection.AssemblyName($e.Name)).Name
    if ($n -eq "UnityEngine") { return $unity }
    if ($n -eq "Assembly-CSharp") { return $asmcsharp }
    if ($n -eq "WWClient") { return $wwclient }
    if ($n -eq "0Harmony") { return $harmony }
    $p = Join-Path $managedDir "$n.dll"
    if (Test-Path $p) { return [System.Reflection.Assembly]::LoadFrom($p) }
    return $null
})

$path = "c:\Users\raiso\Documents\GitHub\Ori-Coop-Plus\Modules\ORIDEModules\ORIDEClientModule.dll"
$bytes = [System.IO.File]::ReadAllBytes($path)
$asm = [System.Reflection.Assembly]::Load($bytes)
$types = $asm.GetTypes()
Write-Output "Successfully loaded all $($types.Count) types without exceptions!"

$sb = New-Object System.Text.StringBuilder
foreach ($t in $types) {
    [void]$sb.AppendLine("=== TYPE: $($t.FullName) (Base: $($t.BaseType.FullName)) ===")
    foreach ($f in $t.GetFields([System.Reflection.BindingFlags]'Public,NonPublic,Instance,Static,DeclaredOnly')) {
        [void]$sb.AppendLine("   FIELD: $($f.FieldType.Name) $($f.Name)")
    }
    foreach ($m in $t.GetMethods([System.Reflection.BindingFlags]'Public,NonPublic,Instance,Static,DeclaredOnly')) {
        [void]$sb.AppendLine("   METHOD: $($m.ReturnType.Name) $($m.Name)")
    }
}
$sb.ToString() | Set-Content -Encoding utf8 "c:\Users\raiso\.gemini\antigravity-ide\brain\c51e8fdd-be3a-4058-8c87-93bed4118004\scratch\all_types_detailed.txt"
