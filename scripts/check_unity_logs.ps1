$paths = @(
    "C:\Program Files (x86)\Steam\steamapps\common\Ori DE\output_log.txt",
    "C:\Program Files (x86)\Steam\steamapps\common\Ori DE\oriDE_Data\output_log.txt",
    "C:\Users\raiso\AppData\LocalLow\Moon Studios\Ori and the Blind Forest Definitive Edition\output_log.txt"
)
foreach ($p in $paths) {
    if (Test-Path $p) {
        Write-Output "=== Found $p (Length: $((Get-Item $p).Length)) ==="
        Get-Content $p -Tail 60
    }
}
