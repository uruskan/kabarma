using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Capa, vurus hissi katmanlari (GDD 4 "Vurus hissi olcutleri", SANAT-SES 7): butun sayilar feel.json kaynagindan
/// (sources.ID) ve ana carpanlardan (master). Atis: namlu efekti, geri tepme, sarsinti, atis sesi. Isabet: duraklama,
/// sarsinti, parca sacilmasi, ezilip esneme, isabet sesi (kritik ve zayif nokta ayri), parca sesi, kritikte parlama.
/// Hasar sayilari Zipzip.Hit -> Fx.DamageNumber (feel.json damageNumbers).
/// </summary>
public partial class Capa
{
    CanvasLayer _flashLayer;
    ColorRect _flash;
    float _flashT;

    FeelFile F => _d.Feel.Data;

    /// <summary>Baslik kimligi: heads.json "heads" ve feel.json kaynak adlari (kazma ... insa, tirpan, civi).</summary>
    public static string HeadId(Head h) => h.ToString().ToLowerInvariant();

    /// <summary>Atis katmani.</summary>
    public void FeelFire(string id, Vector3 muzzle, Color color)
    {
        var s = F.Source(id); var m = F.Master;
        if (s.FireRecoil > 0) _d.Player.Recoil(s.FireRecoil * m.Recoil);
        if (s.FireShake > 0) _d.Player.Shake(s.FireShake * m.Shake);
        int n = Count(s.Muzzle);
        if (n > 0) Fx.Burst(muzzle, color, n, s.MuzzleSpeed, s.MuzzleSize, s.MuzzleLife, true, 0f);
        FeelSound(s.FireSfx, s.FireDb, muzzle);
        Vfx.Head(id, "muzzle", muzzle, color); // fx/Vfx.cs: baslik namlu efekti (head.<id>.muzzle)
    }

    /// <summary>Isabet katmani. squash: esnetilecek hedef (yoksa null; Zipzip kendi esnemesini yapar).</summary>
    public void FeelHit(string id, Vector3 at, Color color, bool crit = false, bool weak = false, Node3D squash = null)
    {
        var s = F.Source(id); var m = F.Master;
        float stopMs = crit ? s.CritHitStopMs : s.HitStopMs;
        if (stopMs > 0) _d.HitStop(stopMs / 1000f * m.HitStop);
        float sh = crit ? s.CritHitShake : s.HitShake;
        if (sh > 0) _d.Player.Shake(sh * m.Shake);
        if (s.HitRecoil > 0) _d.Player.Recoil(s.HitRecoil * m.Recoil);
        FeelDebris(s, at, color, crit);
        if (squash != null && s.Squash > 0) Squash.Kick(squash, s.Squash * F.Squash.Amount * m.Squash, F.Squash.Seconds, F.Squash.Wobbles);
        if (s.SfxChance >= 1f || GD.Randf() < s.SfxChance)
        {
            if (weak && !string.IsNullOrEmpty(s.WeakSfx)) FeelSound(s.WeakSfx, s.WeakDb, at);
            else if (crit && !string.IsNullOrEmpty(s.CritSfx)) FeelSound(s.CritSfx, s.CritDb, at);
            else FeelSound(s.HitSfx, s.HitDb, at);
        }
        FeelSound(s.DebrisSfx, s.DebrisDb, at);
        if (crit && s.CritFlash > 0) ScreenFlash(color, s.CritFlash);
        Vfx.Head(id, "impact", at, color); // fx/Vfx.cs: baslik isabet efekti (head.<id>.impact)
    }

    /// <summary>Parca sacilmasi katmani (kaynagin debris sayilari).</summary>
    public void FeelDebris(string id, Vector3 at, Color color) => FeelDebris(F.Source(id), at, color, false);

    void FeelDebris(FeelSource s, Vector3 at, Color color, bool crit)
    {
        int n = Count(crit && s.CritDebris > 0 ? s.CritDebris : s.Debris);
        if (n > 0) Fx.Burst(at, color, n, s.DebrisSpeed, s.DebrisSize, s.DebrisLife, crit, s.DebrisGravity);
    }

    /// <summary>Esnetme (kaynagin squash carpani x genel esneme).</summary>
    public void FeelSquash(string id, Node3D target)
    {
        float a = F.Source(id).Squash;
        if (a > 0) Squash.Kick(target, a * F.Squash.Amount * F.Master.Squash, F.Squash.Seconds, F.Squash.Wobbles);
    }

    /// <summary>Bos tetik: cephane yok, yuk cok agir.</summary>
    public void FeelEmpty(string id, Vector3 at)
    {
        var s = F.Source(id);
        if (s.FireRecoil > 0) _d.Player.Recoil(s.FireRecoil * F.Master.Recoil);
        FeelSound(s.EmptySfx, s.EmptyDb, null);
    }

    /// <summary>Ses: ana ses carpani desibele cevrilir (0 susturur).</summary>
    public void FeelSound(string name, float db, Vector3? at)
    {
        float m = F.Master.Sound;
        if (string.IsNullOrEmpty(name) || m <= 0f) return;
        Sfx.Play(name, at, db + 20f * Mathf.Log(m) / Mathf.Log(10f));
    }

    int Count(float n) => Mathf.RoundToInt(n * F.Master.Debris);

    /// <summary>Kritik vurusta kisa ekran parlamasi (SANAT-SES 8.3 "ekran parlamasi" surgusu = master.screenFlash).</summary>
    void ScreenFlash(Color color, float strength)
    {
        float a = F.ScreenFlash.Alpha * strength * F.Master.ScreenFlash;
        if (a <= 0f || F.ScreenFlash.Seconds <= 0f) return;
        if (_flashLayer == null)
        {
            _flashLayer = new CanvasLayer { Layer = 5 };
            _flash = new ColorRect { MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
            _flash.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _flashLayer.AddChild(_flash);
            AddChild(_flashLayer);
        }
        _flash.Color = new Color(color.Lerp(Colors.White, 0.6f), a);
        _flash.Visible = true;
        _flashA = a;
        _flashT = F.ScreenFlash.Seconds;
    }

    float _flashA;

    /// <summary>Parlamayi soldurur (gercek zamanla: duraklamada da soner).</summary>
    void TickFlash()
    {
        if (_flash == null || !_flash.Visible) return;
        _flashT -= (float)GetProcessDeltaTime() / Mathf.Max(0.01f, (float)Engine.TimeScale);
        if (_flashT <= 0f) { _flash.Visible = false; return; }
        _flash.Color = _flash.Color with { A = _flashA * _flashT / Mathf.Max(0.001f, F.ScreenFlash.Seconds) };
    }
}
