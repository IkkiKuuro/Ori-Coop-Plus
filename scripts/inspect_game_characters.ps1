$managed = "C:\Program Files (x86)\Steam\steamapps\common\Ori DE\oriDE_Data\Managed"
[System.AppDomain]::CurrentDomain.add_AssemblyResolve({
    param($s, $e)
    $n = (New-Object System.Reflection.AssemblyName($e.Name)).Name
    $p = Join-Path $managed "$n.dll"
    if (Test-Path $p) { return [System.Reflection.Assembly]::Load([System.IO.File]::ReadAllBytes($p)) }
    return $null
})
$asm = [System.Reflection.Assembly]::Load([System.IO.File]::ReadAllBytes((Join-Path $managed "Assembly-CSharp.dll")))
$t = $asm.GetType("Game.Characters")
Write-Output "Type: $($t.FullName)"
foreach ($p in $t.GetProperties()) {
    Write-Output "Property: $($p.PropertyType.Name) $($p.Name)"
}
