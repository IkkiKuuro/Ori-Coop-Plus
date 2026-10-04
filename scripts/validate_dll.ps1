$managed = "C:\Program Files (x86)\Steam\steamapps\common\Ori DE\oriDE_Data\Managed"
$api = "c:\Users\raiso\Documents\GitHub\Ori-Coop-Plus\API\Client"

[System.AppDomain]::CurrentDomain.add_AssemblyResolve({
    param($s, $e)
    $n = (New-Object System.Reflection.AssemblyName($e.Name)).Name
    $p = Join-Path $managed "$n.dll"
    if (Test-Path $p) { return [System.Reflection.Assembly]::Load([System.IO.File]::ReadAllBytes($p)) }
    $p = Join-Path $api "$n.dll"
    if (Test-Path $p) { return [System.Reflection.Assembly]::Load([System.IO.File]::ReadAllBytes($p)) }
    return $null
})

$dll = "c:\Users\raiso\Documents\GitHub\Ori-Coop-Plus\src\OriCoopPlus\OriCoopBepInEx\bin\Release\OriCoopBepInEx.dll"
$asm = [System.Reflection.Assembly]::Load([System.IO.File]::ReadAllBytes($dll))

Write-Output "=== OriCoopBepInEx Assembly Validation ==="
Write-Output "Full Name: $($asm.FullName)"
$types = $asm.GetTypes()
Write-Output "Types Loaded: $($types.Count)"
foreach ($t in $types) {
    Write-Output " - $($t.FullName)"
}
