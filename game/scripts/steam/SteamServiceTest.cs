using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// --test=steamsvc (M6 steam): Steam OLMADAN (ya da kapaliyken) basliksiz calisir ve cokmez. Bir basarim acilinca yerel deftere
/// (user://steam_state.json) hemen yazilir, Steam'e iletilmemis (Pending) kalir; ayni basarim ikinci kez yazilmaz; zengin durum
/// metni kurallari Sim'de sinanir. Steam acikken (elle, --steam_on) ayni test bekleyeni Steam'e de gonderir ve durumu yazar.
/// Cikti: "STEAMSVC RESULT ok|fail ...".
/// </summary>
public partial class SteamServiceTest : Node
{
    float _t, _stage;
    int _step, _checks;
    readonly System.Collections.Generic.List<string> _fail = new();
    SteamService _svc;
    Director _d;

    void Check(bool ok, string what) { _checks++; if (!ok) _fail.Add(what); GD.Print($"STEAMSVC {(ok ? "ok" : "FAIL")} {what}"); }

    public override void _Process(double delta)
    {
        _t += (float)delta; _stage += (float)delta;
        _d ??= GetParent().GetNodeOrNull<Director>("Director");
        if (_d == null || _t < 2f) return;
        if (_t > 120f) { _fail.Add("zaman asimi"); Finish(); return; }
        _svc ??= _d.GetNodeOrNull<SteamService>("SteamService");
        if (_svc == null) { if (_t > 6f) { _fail.Add("SteamService yok"); Finish(); } return; }
        switch (_step)
        {
            case 0:
                if (_stage < 1f) return;
                Check(!_svc.SteamOn || Main.Args.ContainsKey("steam_on"), "Steam'siz kosuda servis kapali (cokmedi)");
                var before = _svc.Ledger.Unlocked.Count;
                Check(!_svc.Ledger.Unlocked.ContainsKey("ilk_gece"), "defterde henuz ilk_gece yok");
                _d.State.Orders.Report("kabarma_survived", 1); // basarim: Ilk Gece
                Check(_d.State.Achievements.IsUnlocked("ilk_gece"), "basarim Sim'de acildi (ilk_gece)");
                Check(_svc.Ledger.Unlocked.ContainsKey("ilk_gece"), "yerel deftere yazildi");
                Check(_svc.LedgerWrites >= 1 && FileAccess.FileExists(_svc.LedgerPath), "defter dosyasi diskte");
                var disk = SteamLedger.FromJson(FileAccess.GetFileAsString(_svc.LedgerPath));
                Check(disk.Unlocked.ContainsKey("ilk_gece") && disk.Unlocked.Count >= before + 1, "diskteki defterde basarim var");
                if (!_svc.SteamOn) Check(_svc.Ledger.Pending().Contains("ilk_gece"), "Steam yok: Steam'e iletilmemis (bekliyor)");
                int w = _svc.LedgerWrites;
                _d.State.Orders.Report("kabarma_survived", 1);
                Check(_svc.LedgerWrites == w, "ayni basarim ikinci kez yazilmadi");
                Check(SteamPresence.Text(_d.Data.Steam, "gobek", 12, 3, true).Contains("Göbek") && SteamPresence.Text(_d.Data.Steam, "gobek", 12, 3, true).Contains("KS 12"), "zengin durum metni");
                _step++; _stage = 0;
                break;
            case 1:
                if (_stage < 4f) return; // birkac kare ve bir esitleme turu Steam'siz da sorunsuz
                Check(true, "birkac sn sonra hala calisiyor (cokme yok)");
                Finish();
                break;
        }
    }

    void Finish()
    {
        GD.Print($"STEAMSVC RESULT {(_fail.Count == 0 ? "ok" : "fail")} checks={_checks} steam={_svc?.SteamOn}" + (_fail.Count > 0 ? " :: " + string.Join(" | ", _fail) : ""));
        GetTree().Quit(_fail.Count == 0 ? 0 : 1);
    }
}