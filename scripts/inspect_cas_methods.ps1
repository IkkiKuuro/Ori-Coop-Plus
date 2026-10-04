$managedDir = "C:\Program Files (x86)\Steam\steamapps\common\Ori DE\oriDE_Data\Managed"
[System.AppDomain]::CurrentDomain.add_AssemblyResolve({
    param($s, $e)
    $n = (New-Object System.Reflection.AssemblyName($e.Name)).Name
    $p = Join-Path $managedDir "$n.dll"
    if (Test-Path $p) { return [System.Reflection.Assembly]::Load([System.IO.File]::ReadAllBytes($p)) }
    return $null
})
$asm = [System.Reflection.Assembly]::Load([System.IO.File]::ReadAllBytes((Join-Path $managedDir "Assembly-CSharp.dll")))
$t = $asm.GetType("SpriteAnimatorWithTransitions")
Write-Output "=== SpriteAnimatorWithTransitions ==="
foreach ($m in $t.GetMethods([System.Reflection.BindingFlags]'Public,NonPublic,Instance,DeclaredOnly')) {
    $params = ($m.GetParameters() | ForEach-Object { "$($_.ParameterType.Name) $($_.Name)" }) -join ", "
    Write-Output "METHOD: $($m.ReturnType.Name) $($m.Name)($params)"
}
$t2 = $asm.GetType("CharacterAnimationSystem")
Write-Output "=== CharacterAnimationSystem ==="
foreach ($m in $t2.GetMethods([System.Reflection.BindingFlags]'Public,NonPublic,Instance,DeclaredOnly')) {
    $params = ($m.GetParameters() | ForEach-Object { "$($_.ParameterType.Name) $($_.Name)" }) -join ", "
    Write-Output "METHOD: $($m.ReturnType.Name) $($m.Name)($params)"
}
