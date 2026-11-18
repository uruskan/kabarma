using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Kadim Taslar (ICERIK 17, GDD 17.1): bes tas, E ile deneme, kazaninca guc acilir. Tamamlanmis tasta E: guc ve ulti
/// takma menusu. Kural Sim KadimTasRules; burada tasin gorunusu, halkalar, gozculer, korunan Gidak ve hayalet dogumu.
/// Taslarin yeri abilities.json (biomes.json pois'e tasinabilir: dunya akisi). Gorunus koddan (YER TUTUCU).
/// </summary>
public partial class KadimTasSystem : Node3D
{
    readonly AbilitySystem _sys;
    Director D => _sys.D;
    AbilitiesFile F => _sys.F;
    readonly Dictionary<string, Node3D> _stones = new();
    readonly Dictionary<string, StandardMaterial3D> _runeMat = new();

    public TrialRun Run { get; private set; }
    KadimTasDef _def;
    readonly List<Node3D> _props = new();
    readonly List<MeshInstance3D> _rings = new();
    Node3D _guard;
    readonly List<TrialGhost> _mine = new();
    float _hudT;
    /// <summary>Son denemenin sonucu (test icin): "kadim_atilma:won", "...:lost:Sure doldu".</summary>
    public readonly List<string> Results = new();

    public const float UseRadius = 2.6f;

    public KadimTasSystem(AbilitySystem sys) { _sys = sys; Name = "KadimTas"; }

    public override void _Ready() => Callable.From(Rebuild).CallDeferred(); // dunya kuruldaktan sonra

    public Vector3 StonePos(KadimTasDef s) => D.World.Ground(new Vector3(s.X, 0, s.Z));
    public Vector3 Local(KadimTasDef s, float dx, float dz) => D.World.Ground(new Vector3(s.X + dx, 0, s.Z + dz));

    public void Rebuild()
    {
        foreach (var s in F.Stones)
        {
            if (!_stones.TryGetValue(s.Id, out var n))
            {
                n = BuildStone(s);
                AddChild(n);
                _stones[s.Id] = n;
            }
            n.GlobalPosition = StonePos(s);
            bool done = D.State.KadimTas.Done.Contains(s.Id);
            _runeMat[s.Id].Emission = done ? Palette.MayaTeal : Palette.MayaViolet;
            _runeMat[s.Id].EmissionEnergyMultiplier = done ? 2.2f : 0.9f;
        }
        GD.Print("KADIM stones " + string.Join(" ", F.Stones.Select(s => $"{s.Id}@{s.X:0},{s.Z:0},y{StonePos(s).Y:0.0}")));
    }

    Node3D BuildStone(KadimTasDef s)
    {
        var root = new Node3D { Name = "Kadim_" + s.Id };
        var kit = new MeshKit();
        var stone = Palette.Shadow.Lerp(Palette.OffWhite, 0.35f);
        kit.Pillow(new Vector3(1.9f, 0.5f, 1.9f), MeshKit.At(0, 0.2f, 0), stone.Darkened(0.2f), 0.4f);
        kit.Pillow(new Vector3(1.1f, 3.2f, 0.7f), MeshKit.At(new Vector3(0, 1.9f, 0), new Vector3(0, 0, 0.04f)), stone, 0.3f);
        kit.Pillow(new Vector3(1.3f, 0.35f, 0.85f), MeshKit.At(0, 3.55f, 0), stone.Darkened(0.1f), 0.4f);
        root.AddChild(kit.Instance());
        var mat = new StandardMaterial3D { VertexColorUseAsAlbedo = true, EmissionEnabled = true, Emission = Palette.MayaViolet, EmissionEnergyMultiplier = 0.9f };
        _runeMat[s.Id] = mat;
        var rune = new MeshKit();
        rune.Pillow(new Vector3(0.5f, 0.5f, 0.08f), MeshKit.At(0, 2.3f, 0.36f), Palette.MayaViolet, 0.5f);
        rune.Pillow(new Vector3(0.5f, 0.5f, 0.08f), MeshKit.At(new Vector3(0, 2.3f, -0.36f), new Vector3(0, Mathf.Pi, 0)), Palette.MayaViolet, 0.5f);
        rune.Pillow(new Vector3(0.12f, 0.9f, 0.08f), MeshKit.At(0, 1.4f, 0.36f), Palette.MayaViolet, 0.5f);
        root.AddChild(rune.Instance(mat));
        root.AddChild(new Label3D
        {
            Text = s.Name, Position = new Vector3(0, 4.3f, 0), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 48, PixelSize = 0.006f, Modulate = Palette.OffWhite, OutlineSize = 10, NoDepthTest = false,
        });
        root.AddChild(new OmniLight3D { Position = new Vector3(0, 2.4f, 0), LightColor = Palette.MayaViolet, LightEnergy = 0.8f, OmniRange = 6f });
        return root;
    }

    KadimTasDef Nearest(out float dist)
    {
        var p = D.Player.GlobalPosition;
        KadimTasDef best = null; dist = float.MaxValue;
        foreach (var s in F.Stones)
        {
            float d = (StonePos(s) - p).Length();
            if (d < dist) { dist = d; best = s; }
        }
        return best;
    }

    /// <summary>Director.Interact (E): tasin yanindaysa deneme ya da takma menusu. Isledi ise true.</summary>
    public bool Interact()
    {
        var s = Nearest(out float dist);
        if (s == null || dist > UseRadius) return false;
        if (Run != null && !Run.Over) { D.Hud.Hint("Deneme sürüyor", 1f); return true; }
        if (D.State.KadimTas.Done.Contains(s.Id)) { OpenLoadout(s); return true; }
        return StartTrial(s.Id);
    }

    /// <summary>Denemeyi baslatir (E ya da ortak oyunda host istegi).</summary>
    public bool StartTrial(string id)
    {
        var run = KadimTasRules.Start(F, D.State.KadimTas, id, out var why);
        if (run == null) { D.Hud.Hint(why, 1.5f); return true; }
        if (_sys.Drill.Active || D.NightActive) { D.Hud.Hint("Kabarma sürerken taş susar", 1.5f); return true; }
        Run = run;
        _def = F.Stone(id);
        BuildProps();
        D.Hud.Big(_def.Name.Replace("Kadim Taş: ", "").ToUpperInvariant(), Palette.MayaViolet, 2f);
        D.Hud.Say(Brief(_def));
        Sfx.Play("siren", StonePos(_def), -12f);
        GD.Print($"KADIM start {id} trial={_def.Trial}");
        return true;
    }

    static string Brief(KadimTasDef s) => s.Trial switch
    {
        "parkur" => $"Halkalardan sırayla geç: {s.Rings.Count} halka, {s.Seconds:0} saniye.",
        "koru" => $"Taşın Gıdak'ını {s.Seconds:0} saniye koru.",
        "dalga" => s.NoDown ? $"{s.Waves.Count} dalga. Kimse yere düşmesin." : $"{s.Waves.Count} dalgaya dayan.",
        "gizlen" => $"Gözcülere görünmeden işarete var: {s.Seconds:0} saniye.",
        _ => "",
    };

    void BuildProps()
    {
        ClearProps();
        var s = _def;
        switch (s.Trial)
        {
            case "parkur":
                for (int i = 0; i < s.Rings.Count; i++)
                {
                    var g = s.Rings[i];
                    var ring = new MeshInstance3D
                    {
                        Mesh = new TorusMesh { InnerRadius = s.RingRadius * 0.8f, OuterRadius = s.RingRadius, Rings = 24, RingSegments = 8 },
                        MaterialOverride = new StandardMaterial3D { AlbedoColor = Palette.MayaTeal, EmissionEnabled = true, Emission = Palette.MayaTeal, EmissionEnergyMultiplier = 1.4f, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded },
                        CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                    };
                    AddChild(ring);
                    var at = Local(s, g[0], g[1]) + Vector3.Up * (g.Length > 2 ? g[2] : 1f);
                    var next = i + 1 < s.Rings.Count ? Local(s, s.Rings[i + 1][0], s.Rings[i + 1][1]) : at + Vector3.Back;
                    ring.GlobalPosition = at;
                    var fwd = (next - at) with { Y = 0 };
                    ring.GlobalBasis = new Basis(Vector3.Right, Mathf.Pi / 2) .Rotated(Vector3.Up, Mathf.Atan2(fwd.X, fwd.Z));
                    _rings.Add(ring);
                    _props.Add(ring);
                }
                break;
            case "koru":
                _guard = GuardHen();
                AddChild(_guard);
                _guard.GlobalPosition = Local(s, 0, 2.5f);
                _props.Add(_guard);
                break;
            case "gizlen":
                foreach (var w in s.Watchers)
                {
                    var n = Watcher(s);
                    AddChild(n);
                    n.GlobalPosition = Local(s, w[0], w[1]);
                    n.Rotation = new Vector3(0, w[2] * Mathf.Pi / 180f, 0);
                    _props.Add(n);
                }
                var goal = new MeshInstance3D
                {
                    Mesh = new CylinderMesh { TopRadius = s.GoalRadius, BottomRadius = s.GoalRadius, Height = 0.08f },
                    MaterialOverride = new StandardMaterial3D { AlbedoColor = Palette.Warning with { A = 0.5f }, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, EmissionEnabled = true, Emission = Palette.Warning, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded },
                };
                AddChild(goal);
                goal.GlobalPosition = Local(s, s.Goal[0], s.Goal[1]) + Vector3.Up * 0.05f;
                _props.Add(goal);
                break;
        }
    }

    Node3D GuardHen()
    {
        var kit = new MeshKit();
        kit.Pillow(new Vector3(0.7f, 0.65f, 0.85f), MeshKit.At(0, 0.45f, 0), Palette.OffWhite, 0.6f);
        kit.Sphere(0.24f, MeshKit.At(0, 0.95f, 0.3f), Palette.OffWhite, 10, 5);
        kit.Pillow(new Vector3(0.12f, 0.1f, 0.18f), MeshKit.At(0, 0.92f, 0.55f), Palette.Warning, 0.4f);
        kit.Pillow(new Vector3(0.1f, 0.18f, 0.18f), MeshKit.At(0, 1.2f, 0.28f), Palette.Accent, 0.4f);
        var n = new Node3D { Name = "TasinGidagi" };
        n.AddChild(kit.Instance(AbilitySystem.GhostMaterial(Palette.MayaTeal, 0.85f)));
        n.AddChild(new Label3D { Text = "Taşın Gıdak'ı", Position = new Vector3(0, 1.7f, 0), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 32, PixelSize = 0.006f, OutlineSize = 8 });
        return n;
    }

    Node3D Watcher(KadimTasDef s)
    {
        var kit = new MeshKit();
        kit.Cylinder(0.25f, 0.35f, 2.2f, MeshKit.At(0, 1.1f, 0), Palette.Shadow.Lerp(Palette.OffWhite, 0.3f), 10);
        kit.Sphere(0.32f, MeshKit.At(0, 2.4f, 0), Palette.Shadow.Lerp(Palette.OffWhite, 0.3f), 10, 5);
        kit.Sphere(0.11f, MeshKit.At(0, 2.45f, 0.28f), Palette.Warning, 8, 4);
        var n = new Node3D { Name = "Gozcu" };
        n.AddChild(kit.Instance());
        // Gorus konisi: yerde yari saydam yelpaze
        float half = s.Fov * 0.5f * Mathf.Pi / 180f;
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        const int seg = 12;
        for (int i = 0; i < seg; i++)
        {
            float a0 = -half + 2 * half * i / seg, a1 = -half + 2 * half * (i + 1) / seg;
            st.AddVertex(new Vector3(0, 0.06f, 0));
            st.AddVertex(new Vector3(Mathf.Sin(a1) * s.Sight, 0.06f, Mathf.Cos(a1) * s.Sight));
            st.AddVertex(new Vector3(Mathf.Sin(a0) * s.Sight, 0.06f, Mathf.Cos(a0) * s.Sight));
        }
        n.AddChild(new MeshInstance3D
        {
            Mesh = st.Commit(),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = Palette.Warning with { A = 0.22f }, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, CullMode = BaseMaterial3D.CullModeEnum.Disabled },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
        return n;
    }

    void ClearProps()
    {
        foreach (var n in _props) if (IsInstanceValid(n)) n.QueueFree();
        _props.Clear();
        _rings.Clear();
        _guard = null;
        foreach (var g in _mine) if (IsInstanceValid(g)) g.Fade();
        _mine.Clear();
    }

    public override void _Process(double delta)
    {
        if (Run == null || Run.Over) return;
        float dt = (float)delta;
        var s = _def;
        var p = D.Player;
        var stone = StonePos(s);
        _mine.RemoveAll(g => !IsInstanceValid(g) || g.Dead);
        float guardDmg = 0;
        foreach (var g in _mine) { guardDmg += g.GuardDamage; g.GuardDamage = 0; }
        bool down = p.Down;
        if (s.NoDown && D.Net != null) foreach (var (_, _, pd) in D.Net.Players()) down |= pd;
        var sense = new TrialSense
        {
            Dx = p.GlobalPosition.X - s.X, Dz = p.GlobalPosition.Z - s.Z,
            Height = p.GlobalPosition.Y - D.World.HeightAt(p.GlobalPosition.X, p.GlobalPosition.Z) + 1f,
            Down = down, Alive = _mine.Count, GuardDamage = guardDmg,
        };
        var step = KadimTasRules.Tick(F, D.State.KadimTas, Run, sense, dt);
        if (step.RingReached >= 0 && step.RingReached < _rings.Count)
        {
            var r = _rings[step.RingReached];
            Fx.Burst(r.GlobalPosition, Palette.MayaTeal, 18, 4f, 0.1f, 0.5f, true, -1f);
            Sfx.Play("pickup", r.GlobalPosition, -4f);
            r.Visible = false;
            GD.Print($"KADIM ring {step.RingReached + 1}/{_rings.Count} t={Run.Time:0.0}");
        }
        for (int i = 0; i < step.Spawn; i++) SpawnGhost(s, stone, i, step.Spawn);
        if (step.Won) Win();
        else if (step.Lost) Lose();
        else
        {
            _hudT -= dt;
            if (_hudT <= 0) { _hudT = 0.2f; D.Hud.Hint(Status(), 0.3f); }
        }
    }

    string Status()
    {
        var s = _def;
        return s.Trial switch
        {
            "parkur" => $"{s.Name} · halka {Run.Ring}/{s.Rings.Count} · {Mathf.Max(0, s.Seconds - Run.Time):0} sn",
            "koru" => $"{s.Name} · Gıdak {Mathf.Max(0, Run.GuardHp):0}/{s.GuardHp:0} · {Mathf.Max(0, s.Seconds - Run.Time):0} sn",
            "dalga" => $"{s.Name} · dalga {Run.Wave + 1}/{s.Waves.Count} · kalan {_mine.Count}",
            "gizlen" => $"{s.Name} · {(Run.SeenT > 0 ? "GÖRÜLÜYORSUN!" : "gizli")} · {Mathf.Max(0, s.Seconds - Run.Time):0} sn",
            _ => s.Name,
        };
    }

    void SpawnGhost(KadimTasDef s, Vector3 stone, int i, int n)
    {
        float a = Mathf.Tau * i / Mathf.Max(1, n) + (float)GD.RandRange(-0.3, 0.3);
        var at = D.World.Ground(stone + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * F.Trial.SpawnRadius);
        var g = new TrialGhost(_sys, F.Trial.Ghost) { Guard = _guard };
        AddChild(g);
        g.GlobalPosition = at;
        _mine.Add(g);
        Fx.Burst(at + Vector3.Up * 0.5f, Palette.MayaViolet, 10, 3f, 0.1f, 0.5f, true, -2f);
    }

    void Win()
    {
        var s = _def;
        bool fresh = AbilityRules.Unlock(F, D.State.Abilities, s.Ability);
        AbilityRules.AutoEquip(F, D.State.Abilities, _sys.L, D.State.Atalar.Ultis);
        Results.Add($"{s.Id}:won");
        GD.Print($"KADIM won {s.Id} ability={s.Ability} fresh={fresh} t={Run.Time:0.0} slots={string.Join(",", _sys.L.Slots)}");
        D.Hud.Big($"GÜÇ: {F.Def(s.Ability).Name.ToUpperInvariant()}", Palette.MayaTeal, 3f);
        D.Hud.Say($"{F.Def(s.Ability).Name} açıldı. {{act:ability_1}} ile kullan. Yuvayı tamamlanmış bir Kadim Taş'ta değiştirirsin.");
        Sfx.Play("levelup", StonePos(s), 0f);
        Fx.Burst(StonePos(s) + Vector3.Up * 2.3f, Palette.MayaTeal, 40, 6f, 0.12f, 0.9f, true, -1f);
        ClearProps();
        Rebuild();
        _sys.SyncNet();
    }

    void Lose()
    {
        Results.Add($"{_def.Id}:lost:{Run.Reason}");
        GD.Print($"KADIM lost {_def.Id} reason={Run.Reason} t={Run.Time:0.0}");
        D.Hud.Big("DENEME BİTTİ", Palette.Warning, 2f);
        D.Hud.Hint($"{Run.Reason}. Taş {F.Trial.RetrySeconds:0} saniye dinlenecek.", 3f);
        ClearProps();
    }

    /// <summary>Tamamlanmis tas: guc ve ulti takma menusu (Rust tarzi menu).</summary>
    void OpenLoadout(KadimTasDef s)
    {
        var l = _sys.L;
        AbilityRules.Normalize(F, l);
        var items = new List<Hud.MenuItem>();
        void Re() => OpenLoadout(s);
        foreach (var id in D.State.Abilities.Unlocked)
        {
            var def = F.Def(id);
            bool on = l.Slots[0] == id;
            items.Add(new Hud.MenuItem($"{(on ? "● " : "")}{def.Name}  ·  Şarj {def.Charge:0} · {def.Cooldown:0} sn", () => !on, () => { var e = _sys.Equip(0, id); if (e != null) D.Hud.Hint(e, 1.5f); Sfx.Play("place", null, -8f); Re(); }));
        }
        items.Add(new Hud.MenuItem("2. yuva: kilitli (Kurbağa Ağa)", () => false, () => { }));
        foreach (var u in D.State.Atalar.Ultis)
        {
            var def = F.Def(u);
            if (def == null) continue;
            bool on = l.Ulti == u;
            items.Add(new Hud.MenuItem($"{(on ? "● " : "")}Ulti: {def.Name}", () => !on, () => { var e = _sys.Equip(-1, u); if (e != null) D.Hud.Hint(e, 1.5f); Sfx.Play("place", null, -8f); Re(); }));
        }
        if (D.State.Atalar.Ultis.Count == 0) items.Add(new Hud.MenuItem("Ulti: yok (Atalar verir)", () => false, () => { }));
        D.Hud.ShowMenu(s.Name, items, $"Şarj {l.Charge:0}/{F.Charge.Max:0} · Maya ölçeri {l.Meter:0}/{F.UltiMeter.Max:0}\n{{act:interact}} / {{act:ui_cancel}}: kapat");
    }

    /// <summary>Denemeyi bitirir (oyuncu oldu, Kabarma basladi, yukleme).</summary>
    public void Abort(string reason)
    {
        if (Run == null || Run.Over) return;
        KadimTasRules.Abort(F, D.State.KadimTas, Run, reason);
        Lose();
    }

    public KadimTasDef Def(string id) => F.Stone(id);
}
