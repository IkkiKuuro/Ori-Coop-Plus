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

function Disassemble-Method($method) {
    Write-Output "--------------------------------------------------------"
    Write-Output "METHOD: $($method.DeclaringType.FullName)::$($method.Name)"
    Write-Output "--------------------------------------------------------"
    $body = $method.GetMethodBody()
    if ($body -eq $null) {
        Write-Output "  (No body)"
        return
    }
    $il = $body.GetILAsByteArray()
    $module = $method.Module
    
    # Simple IL scanner to print tokens/calls
    for ($i = 0; $i -lt $il.Length; $i++) {
        $op = $il[$i]
        # Look for call (0x28) or callvirt (0x6f) or newobj (0x73) or ldfld (0x7b) or stfld (0x7d) or ldstr (0x72)
        if ($op -in @(0x28, 0x6f, 0x73, 0x7b, 0x7d, 0x72, 0x7e, 0x80)) {
            if ($i + 4 -lt $il.Length) {
                $token = [System.BitConverter]::ToInt32($il, $i + 1)
                try {
                    if ($op -eq 0x72) {
                        $str = $module.ResolveString($token)
                        Write-Output ("  [{0:x4}] ldstr `"{1}`"" -f $i, $str)
                    } else {
                        $member = $module.ResolveMember($token)
                        Write-Output ("  [{0:x4}] op_{1:x2} {2}::{3}" -f $i, $op, $member.DeclaringType.Name, $member.Name)
                    }
                } catch {
                    Write-Output ("  [{0:x4}] op_{1:x2} token:0x{2:x8}" -f $i, $op, $token)
                }
                $i += 4
            }
        }
    }
}

$types = $asm.GetTypes()
foreach ($t in $types) {
    if ($t.Name -in @("OriMPPlayer", "MPGameManager", "OriPatches", "CharacterAnimationSystemPatch")) {
        foreach ($m in $t.GetMethods([System.Reflection.BindingFlags]'Public,NonPublic,Instance,Static,DeclaredOnly')) {
            if ($m.Name -in @("FixInvisible", "SetAnimation", "CleanPrefabComponents", "SpawnPlayer", "Prefix", "FixedUpdate", "InitOtherPlayer")) {
                Disassemble-Method $m
            }
        }
    }
}
