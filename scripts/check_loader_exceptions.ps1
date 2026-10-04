$path = "c:\Users\raiso\Documents\GitHub\Ori-Coop-Plus\Modules\ORIDEModules\ORIDEClientModule.dll"
$bytes = [System.IO.File]::ReadAllBytes($path)
$asm = [System.Reflection.Assembly]::Load($bytes)
try {
    $t = $asm.GetTypes()
} catch [System.Reflection.ReflectionTypeLoadException] {
    foreach ($le in $_.Exception.LoaderExceptions) {
        Write-Output "LOADER EXCEPTION: $($le.Message)"
    }
}
