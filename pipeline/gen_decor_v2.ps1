# dunya akisi URETIM ISTEGI: SANAT-SES §9 olcut 3'ten (kalinlik) kalan sus bitkilerinin yeniden uretimi (Tripo API).
# Varsayilan --dry-run: kredi HARCAMAZ, yalniz plan ve kredi tahmini yazar. Uretim yalniz ana oturumda, sahibin
# onayli siniri icinde (URETIM §6 M1 kararlari: "ince sapli ot ve bugdayin yeniden uretimi ~80'e kadar") -Run ile.
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File pipeline/gen_decor_v2.ps1                  # tahmin
#   powershell -NoProfile -ExecutionPolicy Bypass -File pipeline/gen_decor_v2.ps1 -Only ot,bugday -Run   # URETIR
#
# Cikti: ana calisma kopyasinin .tools/tripo/<ad>_v2/ klasoru. Ardindan:
#   pipeline/import_decor.ps1 -Only <ad> -Suffix _v2  -> game/assets/models/<ad>/<ad>.glb ayni yol, kod degismez.
# Olcutten gecerse biomes.json'daki "placeholder" satiri silinir.
param([string[]]$Only = @(), [switch]$Run)

$ErrorActionPreference = 'Continue'
$Tripo = 'C:\Users\uruskan\Documents\GitHub\kabarma\.tools\tripo'
Set-Location $Tripo
$Style = 'Stylised hand-painted game asset, grounded believable materials with subtle wear, chunky readable silhouette, soft bevelled forms, no thin spikes, warm sunny sci-fi farm on a golden meadow moon.'
# Kalinlik olcutu: en ince parca en uzun kenarin %6'sindan kalin. Ince yaprak ve ayri sap yerine tek, kalin, kaynasik govde.
$Solid = 'Sculpted as ONE solid chunky mass: all blades and stalks are fused together into a single thick clump, every blade at least as thick as a finger relative to the plant, no separate thin leaves, no paper-thin surfaces, rounded clay-like volumes.'
$items = [ordered]@{
    ot     = @{ Prompt = "A tall clump of soft feathery meadow grass with plume tips, golden green colour. $Solid $Style"; Face = 2500 }
    bugday = @{ Prompt = "A clump of wild golden wheat with five heavy fat seed heads on thick stalks, rising from a tuft of broad green leaves. $Solid $Style"; Face = 3000 }
    cali   = @{ Prompt = "A round meadow shrub bush made of a few big soft leafy lobes and clusters of small orange berries. $Solid $Style"; Face = 3000 }
    cicek  = @{ Prompt = "A cluster of meadow wildflowers: big chunky orange and cream daisy blossoms on short thick stems with broad rounded leaves. $Solid $Style"; Face = 3000 }
}
$Only = @($Only | ForEach-Object { $_ -split ',' } | Where-Object { $_ })
$total = 0
foreach ($n in $items.Keys) {
    if ($Only.Count -gt 0 -and $Only -notcontains $n) { continue }
    $it = $items[$n]
    $targs = @('make', $it.Prompt, '--model', 'tripo-v3.1', '-p', 'texture_quality=standard', '-p', 'texture_version=v3.5', '-p', "face_limit=$($it.Face)", '--name', "${n}_v2")
    $plan = & tripo @targs --dry-run --json 2>$null | Out-String
    try { $p = $plan | ConvertFrom-Json } catch { Write-Host "PLAN $n okunamadi: $plan"; continue }
    Write-Host ("PLAN {0} valid={1} kredi={2} uyari={3} hata={4}" -f $n, $p.valid, $p.generation_credits_estimate, ($p.warnings -join '|'), ($p.errors -join '|'))
    $total += [int]$p.generation_credits_estimate
    if (-not $Run -or -not $p.valid) { continue }
    & tripo @targs --out "${n}_v2" --json --yes --no-open > "${n}_v2.json" 2> "${n}_v2.err"
    Write-Host "DONE $n exit=$LASTEXITCODE"
    if ($LASTEXITCODE -eq 4) { Write-Host 'STOP: kredi'; break }
}
Write-Host "TOPLAM tahmini kredi: $total (Tripo API havuzu)"
if ($Run) { & tripo balance }
