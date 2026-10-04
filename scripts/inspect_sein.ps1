$managedDir = "C:\Program Files (x86)\Steam\steamapps\common\Ori DE\oriDE_Data\Managed"
$asm = [System.Reflection.Assembly]::Load([System.IO.File]::ReadAllBytes((Join-Path $managedDir "Assembly-CSharp.dll")))
$t = $asm.GetType("SeinCharacter")
Write-Output "=== SeinCharacter Base: $($t.BaseType.FullName) ==="
foreach ($f in $t.GetFields([System.Reflection.BindingFlags]'Public,NonPublic,Instance,Static')) {
    Write-Output "FIELD: $($f.FieldType.FullName) $($f.Name)"
}
