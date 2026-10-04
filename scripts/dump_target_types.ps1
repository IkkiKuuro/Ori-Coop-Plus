$managedDir = "C:\Program Files (x86)\Steam\steamapps\common\Ori DE\oriDE_Data\Managed"
$apiDir = "c:\Users\raiso\Documents\GitHub\Ori-Coop-Plus\API\Client"

[System.AppDomain]::CurrentDomain.add_AssemblyResolve({
    param($s, $e)
    $n = (New-Object System.Reflection.AssemblyName($e.Name)).Name
    $p = Join-Path $managedDir "$n.dll"
    if (Test-Path $p) { return [System.Reflection.Assembly]::Load([System.IO.File]::ReadAllBytes($p)) }
    $p = Join-Path $apiDir "$n.dll"
    if (Test-Path $p) { return [System.Reflection.Assembly]::Load([System.IO.File]::ReadAllBytes($p)) }
    return $null
})

$path = "c:\Users\raiso\Documents\GitHub\Ori-Coop-Plus\Modules\ORIDEModules\ORIDEClientModule.dll"
$bytes = [System.IO.File]::ReadAllBytes($path)
$asm = [System.Reflection.Assembly]::Load($bytes)
$types = $asm.GetTypes()

$sb = New-Object System.Text.StringBuilder
$targetTypes = @("MP_Client.OriMPPlayer", "MP_Client.MPGameManager", "MP_Client.Sync.PlayerPos", "MP_Client.Patches.OriPatches", "MP_Client.Patches.OriPatches+CharacterAnimationSystemPatch")

foreach ($t in $types) {
    if ($t.FullName -notin $targetTypes) { continue }
    [void]$sb.AppendLine("==================================================")
    [void]$sb.AppendLine("TYPE: $($t.FullName) (Base: $($t.BaseType.FullName))")
    [void]$sb.AppendLine("==================================================")
    foreach ($f in $t.GetFields([System.Reflection.BindingFlags]'Public,NonPublic,Instance,Static,DeclaredOnly')) {
        [void]$sb.AppendLine("  FIELD: $($f.FieldType.FullName) $($f.Name)")
    }
    foreach ($m in $t.GetMethods([System.Reflection.BindingFlags]'Public,NonPublic,Instance,Static,DeclaredOnly')) {
        $params = ($m.GetParameters() | ForEach-Object { "$($_.ParameterType.Name) $($_.Name)" }) -join ", "
        [void]$sb.AppendLine("  METHOD: $($m.ReturnType.Name) $($m.Name)($params)")
    }
}
$sb.ToString() | Set-Content -Encoding utf8 "c:\Users\raiso\.gemini\antigravity-ide\brain\c51e8fdd-be3a-4058-8c87-93bed4118004\scratch\target_types_dump.txt"
