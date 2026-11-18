using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Oyuk sinamasi (--test=oyuk; M3 oyuk akisi): gercek oyun yolu (Director, OyukSystem, Capa) uzerinden ICERIK §4.3.
/// Oyuncu noktalar arasina tasinir (yurume botun isi; burada mekanik sinanir). Sinama kurulumu: su deposu Sim'den doldurulur
/// (gol yolculugu yerine), Cekim'le tutulan kaya deligin ustune tasinir (nisan yerine).
///  - 6 oda, 3'er koruyucu; oda tabani HeightAt'te; iceride gunes ve sis yok, cikinca geri gelir;
///  - Kostebek: saklıyken hedef yok; cikinca Sulama deligi islatir → sersem; tikac kayalari delikleri tikar → sikisir;
///    silah koprusuyle (IAtaTarget) yenilir; sandiklar odulu verir, ikinci acilis bos; Oyuk temizlenir;
///  - Yarasa: toplu iken hedef yok ve dalis oyuncuya vurur; kandil yakilinca dagilir, ana yarasa vurulur, yenilir;
///  - temiz Oyuga yeniden giriste dovus yok.
/// Cikti: "OYUK RESULT ok|fail ..." ve cikis kodu.
/// </summary>
public partial class OyukTest : Node
{
    readonly Director _d;
    readonly List<string> _fail = new();
    readonly List<string> _info = new();
    readonly IEnumerator<double> _steps;
    double _wait;
    int _cleared;

    public OyukTest(Director d) { _d = d; Name = "OyukTest"; _steps = Steps().GetEnumerator(); }

    OyukSystem O => _d.Oyuk;
    World W => _d.World;

    void Check(bool ok, string what) { if (!ok) _fail.Add(what); GD.Print($"OYUK {(ok ? "ok" : "FAIL")} {what}"); }

    public override void _Ready() => Events.OyukCleared += _ => _cleared++;

    public override void _Process(double delta)
    {
        if (_wait > 0) { _wait -= delta; return; }
        try
        {
            if (!_steps.MoveNext()) { Finish(); return; }
            _wait = _steps.Current;
        }
        catch (System.Exception e)
        {
            _fail.Add($"exception {e.GetType().Name}: {e.Message}");
            GD.PrintErr(e.ToString());
            Finish();
        }
    }

    /// <summary>--oyuk_shots=klasor (pencereli koşu): oyuncu kamerasindan ekran goruntusu.</summary>
    IEnumerable<double> Shot(string name, Vector3? look = null)
    {
        if (!Main.Args.TryGetValue("oyuk_shots", out var dir) || string.IsNullOrEmpty(dir) || DisplayServer.GetName() == "headless") yield break;
        if (look is { } l && _d.Player.Yaw != null)
        {
            var to = l - _d.Player.GlobalPosition;
            _d.Player.Yaw.Rotation = new Vector3(0, Mathf.Atan2(-to.X, -to.Z), 0); // kamera agaci -Z'ye bakar
        }
        for (int i = 0; i < 8; i++) yield return 0.02;
        DirAccess.MakeDirRecursiveAbsolute(dir);
        var img = GetViewport().GetTexture().GetImage();
        var path = dir.Replace('\\', '/').TrimEnd('/') + "/" + name + ".png";
        img.SavePng(path);
        GD.Print($"OYUK shot {path}");
    }

    void Put(Vector3 at)
    {
        _d.Player.GlobalPosition = W.Ground(at) + Vector3.Up * 0.3f;
        _d.Player.Velocity = Vector3.Zero;
    }

    /// <summary>Gecici savas koprusu: koruyucunun hedefi silah sorgusunda gorunuyor mu.</summary>
    IAtaTarget Target(Vector3 near) => _d.AtaTargetsNear(near, 3f).OfType<OyukTarget>().FirstOrDefault();

    /// <summary>Civi Tabancasi'nin hasari (tool.json weapon): koprunun aldigi gunduz hasari.</summary>
    float WeaponDamage => Mathf.Max(1f, _d.Data.Tool.Weapon.Damage);

    IEnumerable<double> Steps()
    {
        yield return 1.0; // dunya ve odalar kurulsun
        var rooms = O.Rooms;
        Check(rooms.Count == 6, $"rooms {rooms.Count}");
        Check(rooms.Count(r => r.Guardian.Kind == "kostebek") == 3 && rooms.Count(r => r.Guardian.Kind == "yarasa") == 3, "3 Kostebek + 3 Yarasa odasi");
        var r0 = rooms.First(r => r.Guardian.Kind == "kostebek" && r.Tier == 0);
        Check(Mathf.Abs(W.HeightAt(r0.Center.X + 3, r0.Center.Z - 2) - r0.Center.Y) < 0.01f, "HeightAt oda icinde oda tabani");
        Check(Mathf.Abs(W.HeightAt(0, 0) - r0.Center.Y) > 100f, "HeightAt usste arazi");
        float sunBefore = W.Sun.LightEnergy;

        // ---------- Kostebek Bey ----------
        Put(new Vector3(r0.Poi.X, 0, r0.Poi.Z + 1f));
        yield return 0.2;
        Check(O.Prompt(_d.Player.GlobalPosition)?.verb == "Gir", "agizda 'Gir' istemi");
        Check(O.Interact(), "agizda E iceri alir");
        yield return 0.3;
        Check(O.Inside == r0 && W.Indoor, "iceride");
        Check(W.Sun.LightEnergy < 0.01f && !W.Env.Environment.FogEnabled, $"iceride gunes ve sis yok (gunes {W.Sun.LightEnergy:0.00}, once {sunBefore:0.00})");
        Check(Mathf.Abs(_d.Player.GlobalPosition.Y - r0.Center.Y) < 2f, $"oyuncu oda tabaninda y={_d.Player.GlobalPosition.Y:0.0}");
        var m = O.Mole;
        Check(m != null, "kostebek dovusu basladi");
        if (m == null) yield break;
        Check(Target(r0.Center) == null, "sakli kostebek hedef degil");
        _d.State.Tank.Fill(60f); // kurulum: depo dolu (gol yolculugu yerine)
        // Ustteyken deligini islat → sersem.
        for (int i = 0; i < 200 && m.State != MoleFight.Phase.Surfaced; i++) yield return 0.05;
        Check(m.State == MoleFight.Phase.Surfaced, "kostebek cikti");
        var hole = r0.World(m.Holes[m.At].X, m.Holes[m.At].Z);
        Put(hole + (r0.Center - hole).Normalized() * 3.5f);
        foreach (var w in Shot("kostebek_ustte", hole)) yield return w;
        for (int i = 0; i < 40 && m.State == MoleFight.Phase.Surfaced; i++) { _d.Capa.WaterAt(hole); yield return 0.03; }
        Check(m.State == MoleFight.Phase.Stunned, $"Sulama deligi islatinca sersem (durum {m.State})");
        var t = Target(r0.World(m.Pos.x, m.Pos.z, 1f));
        Check(t != null && t.IsWeakPoint, "sersem kostebek zayif hedef");
        float hp0 = m.Hp;
        t?.TakeWeapon(10f, false, Vector3.Forward, 0f);
        Check(Mathf.IsEqualApprox(hp0 - m.Hp, 10f * r0.Guardian.WeakMult), $"sersem hasar carpani ({hp0 - m.Hp:0.0})");
        // Butun delikleri Cekim'le getirilen kayayla tika → sikisir.
        _d.Capa.Drop();
        int plugged = 0;
        foreach (var (h, idx) in m.Holes.Select((h, i) => (h, i)).ToList())
        {
            if (h.Plugged) continue;
            var rock = r0.Plugs.FirstOrDefault(p => !p.Used && !p.Held);
            if (rock == null) break;
            Put(rock.GlobalPosition + Vector3.Back * 2f);
            yield return 0.1;
            Check(_d.Capa.TryGrab(rock.GlobalPosition), $"Cekim kayayi tutar ({rock.Name})");
            yield return 0.1;
            rock.GlobalPosition = r0.World(h.X, h.Z, 1.2f); // tasindi (nisan yerine)
            _d.Capa.Drop();
            for (int k = 0; k < 30 && !h.Plugged; k++) yield return 0.05;
            if (h.Plugged) plugged++;
        }
        Check(plugged > 0 && m.AllPlugged, $"delikler tikandi {plugged}/{m.Holes.Count}");
        Check(m.State == MoleFight.Phase.Trapped, $"butun delikler tikali: sikisti ({m.State})");
        Put(r0.Center + Vector3.Back * 4f);
        foreach (var w in Shot("kostebek_sikisti", r0.Center)) yield return w;
        for (int i = 0; i < 200 && !m.Dead; i++)
        {
            Target(r0.Center + Vector3.Up)?.TakeWeapon(WeaponDamage, false, Vector3.Forward, 0f);
            yield return 0.05;
        }
        yield return 0.2;
        Check(m.Dead && _d.State.Oyuk.IsCleared(r0.Poi.Id), "Kostebek Bey yenildi, Oyuk temiz");
        Check(_cleared == 1, $"Events.OyukCleared {_cleared}");
        // Sandiklar.
        int copper0 = _d.State.Backpack.Count("copper_ingot");
        foreach (var c in r0.Chests) { Put(c.GlobalPosition + Vector3.Back * 1.2f); yield return 0.1; Check(O.Interact() && c.Opened, $"sandik {c.Slot} acildi"); }
        int copperGot = _d.State.Backpack.Count("copper_ingot") - copper0;
        Check(copperGot == OyukGuardianDef.Tier(r0.Guardian.Rewards["copper_ingot"], 0), $"Bakir Kulce odulu {copperGot}");
        Check(_d.State.Oyuk.OpenChest(r0.Poi.Id, r0.Guardian, 0, 0).Count == 0, "ikinci acilis bos");
        // Cikis.
        Put(r0.DoorPos);
        yield return 0.1;
        Check(O.Interact() && O.Inside == null && !W.Indoor, "kapidan cikis");
        yield return 0.3;
        Check(W.Sun.LightEnergy > 0.01f || sunBefore < 0.01f, "disarida gunes geri geldi");
        Check(_d.Player.GlobalPosition.DistanceTo(W.Ground(new Vector3(r0.Poi.X, 0, r0.Poi.Z))) < 6f, "agzin yanina cikti");

        // ---------- Yarasa Yumagi ----------
        var r1 = rooms.First(r => r.Guardian.Kind == "yarasa" && r.Tier == 0);
        Put(new Vector3(r1.Poi.X, 0, r1.Poi.Z + 6f));
        foreach (var w in Shot("koru_oyuk_agzi", W.Ground(new Vector3(r1.Poi.X, 0, r1.Poi.Z)))) yield return w;
        Put(new Vector3(r1.Poi.X, 0, r1.Poi.Z + 1f));
        yield return 0.2;
        Check(O.Interact() && O.Inside == r1, "Koru Oyuguna giris");
        var b = O.Bats;
        Check(b != null && !b.Scattered, "yumak toplu");
        if (b == null) yield break;
        Check(Target(r1.Center + Vector3.Up * 5f) == null, "toplu yumak hedef degil");
        foreach (var w in Shot("yarasa_toplu", r1.Center + Vector3.Up * r1.Guardian.HangHeight)) yield return w;
        float hurt0 = O.Hurt;
        for (int i = 0; i < 120 && O.Hurt <= hurt0; i++) yield return 0.05;
        Check(O.Hurt > hurt0, "karanlikta dalis oyuncuya vurdu");
        var lamp = r1.Lamps[0];
        Put(lamp.GlobalPosition + (lamp.GlobalPosition - r1.Center).Normalized() * 1.2f);
        yield return 0.1;
        Check(O.Prompt(_d.Player.GlobalPosition)?.verb == "Yak", "kandil istemi");
        Check(O.Interact() && lamp.Lit, "kandil yandi");
        for (int i = 0; i < 20 && !b.Scattered; i++) yield return 0.05;
        Check(b.Scattered, "isikla yumak dagildi");
        yield return 0.5;
        foreach (var w in Shot("yarasa_dagildi", r1.Center + Vector3.Up * 4f)) yield return w;
        for (int i = 0; i < 300 && !b.Dead; i++)
        {
            var bt = _d.AtaTargetsNear(r1.Center + Vector3.Up * 5f, 12f).OfType<OyukTarget>().FirstOrDefault();
            bt?.TakeWeapon(WeaponDamage, false, Vector3.Forward, 0f);
            yield return 0.05;
        }
        yield return 0.2;
        Check(b.Dead && _d.State.Oyuk.IsCleared(r1.Poi.Id), "Yarasa Yumagi yenildi, Oyuk temiz");
        Put(r1.DoorPos);
        yield return 0.1;
        O.Interact();
        yield return 0.2;
        // Temiz Oyuga yeniden giris: dovus yok.
        Put(new Vector3(r1.Poi.X, 0, r1.Poi.Z + 1f));
        yield return 0.2;
        O.Interact();
        yield return 0.2;
        Check(O.Inside == r1 && O.Fight == null, "temiz Oyukta dovus yok");
        O.Exit();
        Check(_d.State.Oyuk.ClearedCount == 2, $"temiz Oyuk {_d.State.Oyuk.ClearedCount}");
        _info.Add($"hurt={O.Hurt:0} rooms={rooms.Count}");
    }

    void Finish()
    {
        SetProcess(false);
        bool ok = _fail.Count == 0;
        GD.Print($"OYUK RESULT {(ok ? "ok" : "fail")} checks={(ok ? "passed" : string.Join("; ", _fail))} {string.Join(" ", _info)}");
        GetTree().Quit(ok ? 0 : 1);
    }
}
