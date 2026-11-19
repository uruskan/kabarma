using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Maya Feneri (GDD 14.6) ve Tatbikat (GDD 14.7). Kurallar Sim TatbikatRules; gece akisi kabarma3'undur.
/// GECICI (kabarma3 Wave* sozlesmesi main'e girene kadar, LocalKabarmaRequest + IKabarmaStarter):
/// - Fener: istegi Director'in Kucuk Kabarma'sina verir (40 m / 4 dk / kart siniri o sozlesmeyle uygulanir).
/// - Tatbikat: hayalet dalgalarini bu dugum dogurur (TrialGhost; KS'ye gore can ve hasar, GDD 14.3), odulsuzdur,
///   bitince yapilari baslangic canina onarir.
/// - Maya Kesesi: kabarmis yaratik kesesiyle olunce cantaya 1 Maya Kesesi (ICERIK 2 #36; ganimet savas/kabarma3'te).
/// </summary>
public partial class TatbikatSystem : Node3D, IKabarmaStarter
{
    readonly AbilitySystem _sys;
    Director D => _sys.D;
    AbilitiesFile F => _sys.F;
    readonly Dictionary<string, Node3D> _feners = new();

    TatbikatRun _run;
    int _wave;
    float _waveT;
    readonly List<TrialGhost> _mine = new();
    public bool Active => _run != null;
    /// <summary>Test icin: son Tatbikat'ta dogan hayalet ve onarilan yapi sayisi.</summary>
    public int Spawned { get; private set; }
    public int Repaired { get; private set; }

    public const float FenerReach = 2.4f;

    public TatbikatSystem(AbilitySystem sys) { _sys = sys; Name = "Tatbikat"; }

    public override void _Ready()
    {
        Events.CreatureKilled += OnKilled;
        Callable.From(RebuildFeners).CallDeferred();
    }

    public override void _ExitTree() => Events.CreatureKilled -= OnKilled;

    // ---------- IKabarmaStarter (GECICI) ----------

    public int HighestKs => Mathf.Max(1, D.Data.Kabarma.FirstNight.Ks); // KS merdiveni kabarma3'te
    bool IKabarmaStarter.Active => D.NightActive || Active;

    public string Start(LocalKabarmaRequest r)
    {
        if (r.Kind == "tatbikat") return StartDrill(r.Ks);
        if (r.Kind == "fener")
        {
            GD.Print($"FENER request ks={r.Ks} r={r.Radius} s={r.Seconds} cards<={r.MaxCardLevels} (GECICI: Kucuk Kabarma)");
            return D.RestartKucukKabarma() ? null : Loc.T("tatbikat.no_start");
        }
        return Loc.T("tatbikat.unknown_request");
    }

    void OnKilled(string id, bool sac)
    {
        if (!sac || !D.NightActive) return;
        int left = D.State.Backpack.Add("maya_kesesi", 1);
        if (left == 0) GD.Print("ABILITY maya_kesesi +1 (GECICI ganimet)");
    }

    // ---------- Tatbikat ----------

    /// <summary>Ocak menusunden (ya da test): acilmis KS ile Tatbikat. Olmuyorsa Turkce neden.</summary>
    public string StartDrill(int ks)
    {
        var req = TatbikatRules.Tatbikat(F, ks, HighestKs, D.NightActive || Active || (_sys.Stones.Run is { Over: false }), out var why);
        if (req == null) return why;
        _run = new TatbikatRun { Ks = ks };
        foreach (var p in D.Build?.System?.Pieces ?? Enumerable.Empty<BuildPiece>())
            if (IsInstanceValid(p) && p.S != null) _run.Hp[p.S.Id.ToString()] = p.Hp;
        _wave = -1;
        _waveT = 2f;
        Spawned = 0;
        Repaired = 0;
        D.Hud.Big(Loc.T("tatbikat.big", ks), Palette.Warning, 2.5f);
        D.Hud.Say(Loc.T("tatbikat.say"));
        Sfx.Play("siren", null, -6f);
        GD.Print($"TATBIKAT start ks={ks} pieces={_run.Hp.Count}");
        return null;
    }

    public override void _Process(double delta)
    {
        if (_run == null) return;
        float dt = (float)delta;
        _mine.RemoveAll(g => !IsInstanceValid(g) || g.Dead);
        if (D.Player.Down && D.Player.Hp <= 0) { End(Loc.T("tatbikat.you_fell")); return; }
        if (_mine.Count > 0) { D.Hud.Hint(Loc.T("tatbikat.wave_hint", _wave + 1, F.Tatbikat.Waves, _mine.Count), 0.3f); return; }
        _waveT -= dt;
        if (_waveT > 0) return;
        if (_wave + 1 >= F.Tatbikat.Waves) { End(""); return; }
        _wave++;
        _waveT = 3f;
        int n = Mathf.Max(1, Mathf.RoundToInt((30 + 8 * _run.Ks) * F.Tatbikat.CountScale));
        float hp = Mathf.Pow(1.15f, _run.Ks - 1), dmg = Mathf.Pow(1.08f, _run.Ks - 1); // GDD 14.3
        var c = D.FarmCenter();
        for (int i = 0; i < n; i++)
        {
            float a = (float)GD.RandRange(0, Mathf.Tau);
            float r = (float)GD.RandRange(F.Tatbikat.SpawnMin, F.Tatbikat.SpawnMax);
            var g = new TrialGhost(_sys, F.Trial.Ghost, hp, dmg);
            AddChild(g);
            g.GlobalPosition = D.World.Ground(c + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * r);
            _mine.Add(g);
        }
        Spawned += n;
        D.Hud.Big(Loc.T("tatbikat.ghost_wave", _wave + 1), Palette.MayaViolet, 1.6f);
        GD.Print($"TATBIKAT wave {_wave + 1} count={n}");
    }

    /// <summary>Tatbikat'i bitirir: hayaletler soner, yapilar baslangic canina onarilir. Odul yok.</summary>
    public void End(string reason)
    {
        if (_run == null) return;
        foreach (var g in _mine) if (IsInstanceValid(g)) g.Fade();
        _mine.Clear();
        var now = new Dictionary<string, float>();
        var pieces = (D.Build?.System?.Pieces ?? Enumerable.Empty<BuildPiece>()).Where(p => IsInstanceValid(p) && p.S != null).ToList();
        foreach (var p in pieces) now[p.S.Id.ToString()] = p.Hp;
        var fix = TatbikatRules.Repairs(F, _run, now);
        foreach (var p in pieces) if (fix.TryGetValue(p.S.Id.ToString(), out var hp)) { p.Hp = hp; Repaired++; }
        GD.Print($"TATBIKAT end ks={_run.Ks} waves={_wave + 1} spawned={Spawned} repaired={Repaired} reason='{reason}'");
        D.Hud.Big(reason == "" ? Loc.T("tatbikat.done") : Loc.T("tatbikat.aborted"), reason == "" ? Palette.MayaTeal : Palette.Warning, 2.5f);
        if (reason != "") D.Hud.Hint(reason, 2f);
        _run = null;
    }

    /// <summary>Ocak'in E menusune eklenecek satirlar (Ocak.cs'ten bir satirla).</summary>
    public IEnumerable<Hud.MenuItem> OcakMenuItems()
    {
        int top = HighestKs;
        for (int ks = 1; ks <= top; ks++)
        {
            int k = ks;
            yield return new Hud.MenuItem(Loc.T("tatbikat.menu_item", k), () => !Active && !D.NightActive, () => { D.Hud.HideMenu(); var e = StartDrill(k); if (e != null) D.Hud.Hint(e, 1.5f); });
        }
        if (Active) yield return new Hud.MenuItem(Loc.T("tatbikat.end_item"), () => true, () => { D.Hud.HideMenu(); End(Loc.T("tatbikat.stopped")); });
    }

    // ---------- Maya Feneri ----------

    /// <summary>Capa.Insa: kemerdeki Maya Feneri'ni kurar. Fener degilse false.</summary>
    public bool Place(PlaceableDef def, Vector3 at, float yaw)
    {
        if (def.Id != F.Fener.Id) return false;
        var site = D.State.Fener.Add(at.X, at.Y, at.Z, yaw);
        Spawn(site);
        Sfx.Play("place", at, 2f);
        GD.Print($"PLACED {def.Id} at {at} fener={site.Id}");
        Events.RaiseStationPlaced(def.Id);
        _sys.SyncNet();
        return true;
    }

    public void RebuildFeners()
    {
        foreach (var n in _feners.Values) n.QueueFree();
        _feners.Clear();
        foreach (var s in D.State.Fener.Sites) Spawn(s);
    }

    void Spawn(FenerSite s)
    {
        var n = FenerLook();
        AddChild(n);
        n.GlobalPosition = new Vector3(s.X, s.Y, s.Z);
        n.Rotation = new Vector3(0, s.Yaw, 0);
        _feners[s.Id] = n;
    }

    /// <summary>Director.Interact (E): fenerin yanindaysa yakar. Isledi ise true.</summary>
    public bool Interact()
    {
        var p = D.Player.GlobalPosition;
        var site = D.State.Fener.Sites.OrderBy(s => new Vector3(s.X, s.Y, s.Z).DistanceTo(p)).FirstOrDefault();
        if (site == null || new Vector3(site.X, site.Y, site.Z).DistanceTo(p) > FenerReach) return false;
        if (D.Net?.IsClient == true) { D.Net.Forward("fener", w => w.Str(site.Id)); return true; } // ortak: host yakar
        Light(site.Id);
        return true;
    }

    public void Light(string id)
    {
        var site = D.State.Fener.Sites.FirstOrDefault(s => s.Id == id);
        if (site == null) return;
        int day = D.State.Calendar.Running ? D.State.Calendar.Day : 0;
        var req = TatbikatRules.LightFener(F, D.State.Fener, id, day, HighestKs, D.NightActive || Active, site.X, site.Z, out var why);
        if (req == null) { D.Hud.Hint(why, 2f); return; }
        var err = Start(req);
        if (err != null) { TatbikatRules.Forget(D.State.Fener, id); D.Hud.Hint(err, 2f); return; }
        if (_feners.TryGetValue(id, out var n)) Fx.Burst(n.GlobalPosition + Vector3.Up * 1.6f, Palette.MayaViolet, 40, 6f, 0.12f, 1f, true, -2f);
        GD.Print($"FENER lit {id} day={day} ks={req.Ks}");
        _sys.SyncNet();
    }

    static Node3D FenerLook()
    {
        var kit = new MeshKit();
        kit.Cylinder(0.35f, 0.45f, 0.3f, MeshKit.At(0, 0.15f, 0), Palette.Shadow.Lerp(Palette.OffWhite, 0.3f), 10);
        kit.Cylinder(0.08f, 0.1f, 1.2f, MeshKit.At(0, 0.9f, 0), Palette.Wood, 8);
        kit.Cylinder(0.32f, 0.22f, 0.12f, MeshKit.At(0, 1.55f, 0), Palette.Warning.Darkened(0.35f), 10);
        var n = new Node3D { Name = "MayaFeneri" };
        n.AddChild(kit.Instance());
        var glow = new MeshKit();
        glow.Sphere(0.24f, MeshKit.At(0, 1.82f, 0), Palette.MayaViolet, 12, 6);
        n.AddChild(glow.Instance(new StandardMaterial3D { VertexColorUseAsAlbedo = true, EmissionEnabled = true, Emission = Palette.MayaViolet, EmissionEnergyMultiplier = 2f }));
        n.AddChild(new OmniLight3D { Position = new Vector3(0, 1.85f, 0), LightColor = Palette.MayaViolet, LightEnergy = 0.7f, OmniRange = 5f });
        return n;
    }
}
