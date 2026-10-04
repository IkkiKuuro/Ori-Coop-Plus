$managedDir = "C:\Program Files (x86)\Steam\steamapps\common\Ori DE\oriDE_Data\Managed"
[System.AppDomain]::CurrentDomain.add_AssemblyResolve({
    param($s, $e)
    $n = (New-Object System.Reflection.AssemblyName($e.Name)).Name
    $p = Join-Path $managedDir "$n.dll"
    if (Test-Path $p) { return [System.Reflection.Assembly]::Load([System.IO.File]::ReadAllBytes($p)) }
    return $null
})
$asm = [System.Reflection.Assembly]::Load([System.IO.File]::ReadAllBytes((Join-Path $managedDir "Assembly-CSharp.dll")))
$t = $asm.GetType("TextureAnimator")
Write-Output "=== TextureAnimator ==="
foreach ($f in $t.GetFields([System.Reflection.BindingFlags]'Public,NonPublic,Instance')) {
    Write-Output "  $($f.FieldType.Name) $($f.Name)"
}
$t2 = $asm.GetType("CharacterSpriteMirror")
Write-Output "=== CharacterSpriteMirror ==="
foreach ($f in $t2.GetFields([System.Reflection.BindingFlags]'Public,NonPublic,Instance')) {
    Write-Output "  $($f.FieldType.Name) $($f.Name)"
}
