$path = "c:\Users\raiso\Documents\GitHub\Ori-Coop-Plus\API\Client\WWClient.dll"
$bytes = [System.IO.File]::ReadAllBytes($path)
$asm = [System.Reflection.Assembly]::Load($bytes)
$types = try { $asm.GetTypes() } catch [System.Reflection.ReflectionTypeLoadException] { $_.Exception.Types }
foreach ($type in $types) {
    if ($type -ne $null) {
        Write-Output "TYPE: $($type.FullName)"
        foreach ($m in $type.GetMembers([System.Reflection.BindingFlags]'Public,NonPublic,Instance,Static,DeclaredOnly')) {
            Write-Output "   MEMBER: $($m.Name)"
        }
    }
}
