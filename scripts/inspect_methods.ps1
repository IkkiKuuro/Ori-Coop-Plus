$path = "c:\Users\raiso\Documents\GitHub\Ori-Coop-Plus\Modules\ORIDEModules\ORIDEClientModule.dll"
$bytes = [System.IO.File]::ReadAllBytes($path)

[System.AppDomain]::CurrentDomain.add_AssemblyResolve({
    param($sender, $args)
    $name = (New-Object System.Reflection.AssemblyName($args.Name)).Name
    # Return null or try to find in current directory or API
    $apiClient = "c:\Users\raiso\Documents\GitHub\Ori-Coop-Plus\API\Client\$name.dll"
    if (Test-Path $apiClient) {
        return [System.Reflection.Assembly]::LoadFrom($apiClient)
    }
    return $null
})

# Let's inspect string literals and IL or methods from types in ORIDEClientModule
$asm = [System.Reflection.Assembly]::Load($bytes)
$types = try { $asm.GetTypes() } catch [System.Reflection.ReflectionTypeLoadException] { $_.Exception.Types }

foreach ($t in $types) {
    if ($t -eq $null) { continue }
    Write-Output "=== TYPE: $($t.FullName) ==="
    foreach ($m in $t.GetMethods([System.Reflection.BindingFlags]'Public,NonPublic,Instance,Static,DeclaredOnly')) {
        Write-Output "   METHOD: $($m.Name)"
    }
}
