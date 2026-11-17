# agy (Google Antigravity CLI) ile esya ikonu uretir: pipeline/icons_agy.json'daki her kimlik icin bir istek.
# Var olan ham dosya atlanir; her istekten sonra PNG'nin indigi denetlenir; kota hatasinda (429) durur.
# Kullanim: powershell -NoProfile -File pipeline/gen_icons_agy.ps1 -Raw <ham_klasor> [-Only id1,id2]
param([Parameter(Mandatory)][string]$Raw, [string]$Only = "", [int]$Timeout = 300)

$root = Split-Path $PSScriptRoot -Parent
$cfg = Get-Content (Join-Path $PSScriptRoot "icons_agy.json") -Raw -Encoding UTF8 | ConvertFrom-Json
New-Item -ItemType Directory -Force $Raw | Out-Null
$Raw = (Resolve-Path $Raw).Path
$want = @($Only.Split(",") | Where-Object { $_ })
$log = Join-Path $Raw "agy_log.txt"

foreach ($it in $cfg.items) {
    if ($want.Count -and $want -notcontains $it.id) { continue }
    $dst = Join-Path $Raw "$($it.id).png"
    if (Test-Path $dst) { Write-Output "SKIP $($it.id)"; continue }
    $prompt = "$($it.subject). $($cfg.style)"
    $ask = "Use your image generation tool exactly once to generate this image, then save the generated PNG to this exact absolute path: $dst . Do not edit or post-process the image. Reply only with the saved path or the error. Image prompt: $prompt"
    Push-Location $Raw
    $out = agy -p $ask --mode accept-edits --add-dir $Raw --print-timeout "$($Timeout)s" 2>&1 | Out-String
    Pop-Location
    Add-Content $log "=== $($it.id)`n$out" -Encoding UTF8
    if (Test-Path $dst) { Write-Output "OK $($it.id)" }
    else {
        Write-Output "FAIL $($it.id): $($out.Trim() -replace '\s+',' ' | Select-Object -First 1)"
        if ($out -match "429|RESOURCE_EXHAUSTED|quota") { Write-Output "QUOTA: durdu"; break }
    }
}
