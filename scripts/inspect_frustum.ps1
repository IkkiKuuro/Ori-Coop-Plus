$managedDir = "C:\Program Files (x86)\Steam\steamapps\common\Ori DE\oriDE_Data\Managed"
[System.AppDomain]::CurrentDomain.add_AssemblyResolve({
    param($s, $e)
    $n = (New-Object System.Reflection.AssemblyName($e.Name)).Name
    $p = Join-Path $managedDir "$n.dll"
    if (Test-Path $p) { return [System.Reflection.Assembly]::Load([System.IO.File]::ReadAllBytes($p)) }
    return $null
})
$asm = [System.Reflection.Assembly]::Load([System.IO.File]::ReadAllBytes((Join-Path $managedDir "Assembly-CSharp.dll")))
$t = $asm.GetType("CameraFrustumOptimizer")
$m = $t.GetMethod("ProcessFrustumOptimizable", [System.Reflection.BindingFlags]'Public,NonPublic,Static,Instance')
Write-Output "Params for ProcessFrustumOptimizable:"
foreach ($p in $m.GetParameters()) {
    Write-Output " - $($p.ParameterType.FullName) $($p.Name)"
}
$interfaces = $asm.GetTypes() | Where-Object { $_.Name -like "*FrustumOptimizable*" }
foreach ($i in $interfaces) {
    Write-Output "Frustum Type: $($i.FullName)"
    foreach ($mem in $i.GetMembers()) {
        Write-Output "   $($mem.Name)"
    }
}
