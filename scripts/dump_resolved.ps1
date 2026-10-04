$managedDir = "C:\Program Files (x86)\Steam\steamapps\common\Ori DE\oriDE_Data\Managed"
$apiDir = "c:\Users\raiso\Documents\GitHub\Ori-Coop-Plus\API\Client"

[System.AppDomain]::CurrentDomain.add_AssemblyResolve({
    param($sender, $args)
    $name = (New-Object System.Reflection.AssemblyName($args.Name)).Name
    $p1 = Join-Path $managedDir "$name.dll"
    if (Test-Path $p1) { return [System.Reflection.Assembly]::LoadFrom($p1) }
    $p2 = Join-Path $apiDir "$name.dll"
    if (Test-Path $p2) { return [System.Reflection.Assembly]::LoadFrom($p2) }
    return $null
})

$path = "c:\Users\raiso\Documents\GitHub\Ori-Coop-Plus\Modules\ORIDEModules\ORIDEClientModule.dll"
$bytes = [System.IO.File]::ReadAllBytes($path)
$asm = [System.Reflection.Assembly]::Load($bytes)
try {
    $types = $asm.GetTypes()
} catch [System.Reflection.ReflectionTypeLoadException] {
    Write-Output "Loader exceptions:"
    foreach ($le in $_.Exception.LoaderExceptions) {
        Write-Output " - $($le.Message)"
    }
    $types = $_.Exception.Types
}

$sb = New-Object System.Text.StringBuilder
foreach ($t in $types) {
    if ($t -eq $null) { continue }
    [void]$sb.AppendLine("=== TYPE: $($t.FullName) (Base: $($t.BaseType.FullName)) ===")
    foreach ($f in $t.GetFields([System.Reflection.BindingFlags]'Public,NonPublic,Instance,Static,DeclaredOnly')) {
        [void]$sb.AppendLine("   FIELD: $($f.FieldType.Name) $($f.Name)")
    }
    foreach ($m in $t.GetMethods([System.Reflection.BindingFlags]'Public,NonPublic,Instance,Static,DeclaredOnly')) {
        [void]$sb.AppendLine("   METHOD: $($m.ReturnType.Name) $($m.Name)")
    }
}
$sb.ToString() | Set-Content -Encoding utf8 "c:\Users\raiso\.gemini\antigravity-ide\brain\c51e8fdd-be3a-4058-8c87-93bed4118004\scratch\all_types_resolved.txt"
Write-Output "Wrote $($types.Count) types!"
