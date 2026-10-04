$managedDir = "C:\Program Files (x86)\Steam\steamapps\common\Ori DE\oriDE_Data\Managed"
$apiDir = "c:\Users\raiso\Documents\GitHub\Ori-Coop-Plus\API\Client"

[System.AppDomain]::CurrentDomain.add_AssemblyResolve({
    param($s, $e)
    $n = (New-Object System.Reflection.AssemblyName($e.Name)).Name
    $p = Join-Path $managedDir "$n.dll"
    if (Test-Path $p) { return [System.Reflection.Assembly]::Load([System.IO.File]::ReadAllBytes($p)) }
    $p = Join-Path $apiDir "$n.dll"
    if (Test-Path $p) { return [System.Reflection.Assembly]::Load([System.IO.File]::ReadAllBytes($p)) }
    return $null
})

$path = "c:\Users\raiso\Documents\GitHub\Ori-Coop-Plus\Modules\ORIDEModules\ORIDEClientModule.dll"
$bytes = [System.IO.File]::ReadAllBytes($path)
$asm = [System.Reflection.Assembly]::Load($bytes)
try {
    $types = $asm.GetTypes()
} catch [System.Reflection.ReflectionTypeLoadException] {
    foreach ($le in $_.Exception.LoaderExceptions) {
        Write-Output "ERR: $($le.ToString())"
    }
    $types = $_.Exception.Types
}
foreach ($t in $types) {
    if ($t -ne $null) { Write-Output "T: $($t.FullName)" }
}
