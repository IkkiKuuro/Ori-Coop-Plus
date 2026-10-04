$managedDir = "C:\Program Files (x86)\Steam\steamapps\common\Ori DE\oriDE_Data\Managed"
[System.AppDomain]::CurrentDomain.add_AssemblyResolve({
    param($s, $e)
    $n = (New-Object System.Reflection.AssemblyName($e.Name)).Name
    $p = Join-Path $managedDir "$n.dll"
    if (Test-Path $p) { return [System.Reflection.Assembly]::Load([System.IO.File]::ReadAllBytes($p)) }
    return $null
})
$asm = [System.Reflection.Assembly]::Load([System.IO.File]::ReadAllBytes((Join-Path $managedDir "Assembly-CSharp.dll")))
$ifo = $asm.GetType("IFrustumOptimizable")
$types = $asm.GetTypes() | Where-Object { $ifo.IsAssignableFrom($_) }
Write-Output "Types implementing IFrustumOptimizable ($($types.Count)):"
foreach ($t in $types) {
    Write-Output " - $($t.FullName)"
}
