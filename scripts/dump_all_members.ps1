$path = "c:\Users\raiso\Documents\GitHub\Ori-Coop-Plus\Modules\ORIDEModules\ORIDEClientModule.dll"
$bytes = [System.IO.File]::ReadAllBytes($path)

# Let's inspect all types in the assembly by resolving assembly dependencies dynamically
[System.AppDomain]::CurrentDomain.add_AssemblyResolve({
    param($s, $e)
    # Return dummy or null
    return $null
})

# Let's see what types exist in ORIDEClientModule
$asm = [System.Reflection.Assembly]::Load($bytes)
$types = try { $asm.GetTypes() } catch [System.Reflection.ReflectionTypeLoadException] { $_.Exception.Types }

foreach ($t in $types) {
    if ($t -eq $null) { continue }
    Write-Output "TYPE: $($t.FullName)"
    foreach ($f in $t.GetFields([System.Reflection.BindingFlags]'Public,NonPublic,Instance,Static,DeclaredOnly')) {
        Write-Output "  FIELD: $($f.FieldType.Name) $($f.Name)"
    }
    foreach ($m in $t.GetMethods([System.Reflection.BindingFlags]'Public,NonPublic,Instance,Static,DeclaredOnly')) {
        Write-Output "  METHOD: $($m.ReturnType.Name) $($m.Name)"
    }
}
