$path = "c:\Users\raiso\Documents\GitHub\Ori-Coop-Plus\Modules\ORIDEModules\ORIDEClientModule.dll"
$bytes = [System.IO.File]::ReadAllBytes($path)
$ascii = [System.Text.Encoding]::ASCII.GetString($bytes)
$regex = [regex]'[A-Za-z0-9_\.\<\>]{5,}'
$matches = $regex.Matches($ascii)
$words = @{}
foreach ($m in $matches) {
    $val = $m.Value
    if ($val -match 'Sein|Ori|Player|Clone|Render|Anim|Mesh|Sprite|Hide|Show|Visible|Alpha|Camera|Frustum|Disable|Enable') {
        $words[$val] = $true
    }
}
$words.Keys | Sort-Object | Out-File "c:\Users\raiso\.gemini\antigravity-ide\brain\c51e8fdd-be3a-4058-8c87-93bed4118004\scratch\strings_found.txt"
Write-Output "Extracted $($words.Count) matching strings."
