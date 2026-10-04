$managedDir = "C:\Program Files (x86)\Steam\steamapps\common\Ori DE\oriDE_Data\Managed"
[System.AppDomain]::CurrentDomain.add_AssemblyResolve({
    param($s, $e)
    $n = (New-Object System.Reflection.AssemblyName($e.Name)).Name
    $p = Join-Path $managedDir "$n.dll"
    if (Test-Path $p) { return [System.Reflection.Assembly]::Load([System.IO.File]::ReadAllBytes($p)) }
    return $null
})
$asm = [System.Reflection.Assembly]::Load([System.IO.File]::ReadAllBytes((Join-Path $managedDir "Assembly-CSharp.dll")))
$tSein = $asm.GetType("SeinCharacter")
$methods = @()
foreach ($t in $asm.GetTypes()) {
    if ($t.Name -like "*Cutscene*" -or $t.Name -like "*Sein*" -or $t.Name -like "*Visibility*") {
        foreach ($m in $t.GetMethods([System.Reflection.BindingFlags]'Public,NonPublic,Instance,Static,DeclaredOnly')) {
            if ($m.Name -match "Hide|Show|Visible|Disappear|Appear|Suspend|Resume|Disable|Enable") {
                $methods += "$($t.Name)::$($m.Name)"
            }
        }
    }
}
$methods | Select-Object -First 40
