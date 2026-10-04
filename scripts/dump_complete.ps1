[System.AppDomain]::CurrentDomain.add_AssemblyResolve({
    param($sender, $args)
    $name = (New-Object System.Reflection.AssemblyName($args.Name)).Name
    $found = Get-ChildItem -Path "c:\Users\raiso\Documents\GitHub\Ori-Coop-Plus" -Recurse -Filter "$name.dll" -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($found) {
        return [System.Reflection.Assembly]::LoadFrom($found.FullName)
    }
    # Create a dynamic assembly to satisfy missing dependencies like Assembly-CSharp and UnityEngine
    $an = New-Object System.Reflection.AssemblyName($name)
    $ab = [System.AppDomain]::CurrentDomain.DefineDynamicAssembly($an, [System.Reflection.Emit.AssemblyBuilderAccess]::Run)
    return $ab
})

$path = "c:\Users\raiso\Documents\GitHub\Ori-Coop-Plus\Modules\ORIDEModules\ORIDEClientModule.dll"
$bytes = [System.IO.File]::ReadAllBytes($path)
$asm = [System.Reflection.Assembly]::Load($bytes)
try {
    $types = $asm.GetTypes()
} catch [System.Reflection.ReflectionTypeLoadException] {
    $types = $_.Exception.Types
}

$output = @()
foreach ($t in $types) {
    if ($t -eq $null) { continue }
    $output += "=== TYPE: $($t.FullName) ==="
    foreach ($m in $t.GetMembers([System.Reflection.BindingFlags]'Public,NonPublic,Instance,Static,DeclaredOnly')) {
        $output += "   $($m.MemberType): $($m.ToString())"
    }
}
$output | Set-Content -Encoding utf8 "c:\Users\raiso\.gemini\antigravity-ide\brain\c51e8fdd-be3a-4058-8c87-93bed4118004\scratch\all_decompiled_types.txt"
Write-Output "Done. Types: $($types.Count)"
