# Akisin dalini main'e kendisi alir (sahibin karari 25, 18 Kasim 2026): ana oturumu beklemeden.
# Kullanim (akisin worktree'sinde, her sey commit edilmisken):
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools/integrate.ps1
# Adimlar: guncel main'i dala birlestir -> derleme ve Sim testleri (tools/test.ps1 -Only build,sim) ->
# main'i bu dalin ucuna atomik olarak ilerlet (git update-ref eski-deger denetimiyle). Bu arada baska bir akis main'i
# ilerletmisse bastan tekrarlar. Catisma cikarsa durur: akis cozer, commit eder, betigi yeniden kosar.
# Ana calisma kopyasi (C:\...\kabarma) main'i "detached" tutar; ana oturum oyunu acmadan once onu main'e getirir.
param(
    [int]$Retries = 6,
    [switch]$SkipTests,   # yalniz belge/veri disi bir sey degismediyse
    [switch]$DryRun       # main'i ilerletmeden her seyi dener
)
$ErrorActionPreference = 'Stop'

$branch = (& git rev-parse --abbrev-ref HEAD).Trim()
if ($branch -eq 'main') { throw "main uzerinde calistirma: kendi dalinda (ya da ana oturumda detached HEAD'de) kos" }
$dirty = & git status --porcelain --untracked-files=no
if ($dirty) { $dirty | Select-Object -First 10 | ForEach-Object { Write-Host "  $_" }; throw "commit edilmemis degisiklik var; once commit et" }

for ($i = 1; $i -le $Retries; $i++) {
    $old = (& git rev-parse refs/heads/main).Trim()
    & git merge-base --is-ancestor $old HEAD; $alreadyIn = ($LASTEXITCODE -eq 0)
    if (-not $alreadyIn) {
        Write-Host "[$i] main ($($old.Substring(0,7))) dala birlestiriliyor"
        & git merge --no-edit refs/heads/main
        if ($LASTEXITCODE -ne 0) { throw "main ile catisma: coz, commit et, betigi yeniden kos (git merge --abort ile geri alinabilir)" }
    }
    if (-not $SkipTests) {
        Write-Host "[$i] derleme ve Sim testleri"
        & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'test.ps1') -Only build,sim
        if ($LASTEXITCODE -ne 0) { throw "derleme ya da Sim testleri kirmizi; duzelt, commit et, yeniden kos" }
    }
    $new = (& git rev-parse HEAD).Trim()
    if ($new -eq $old) { Write-Host "main zaten bu dalin ucunda ($($new.Substring(0,7)))"; exit 0 }
    if ($DryRun) { Write-Host "DRYRUN: main $($old.Substring(0,7)) -> $($new.Substring(0,7)) ($branch) olurdu"; exit 0 }
    & git update-ref -m "integrate $branch" refs/heads/main $new $old
    if ($LASTEXITCODE -eq 0) { Write-Host "TAMAM: main $($old.Substring(0,7)) -> $($new.Substring(0,7)) ($branch)"; exit 0 }
    Write-Host "[$i] main bu arada ilerledi; yeniden deneniyor"
}
throw "main $Retries denemede de ilerledi; biraz sonra yeniden kos"
