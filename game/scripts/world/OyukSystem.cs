using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Oyuklar (M3, oyuk akisi; ICERIK §4.3, §17 "M3: Oyuklar"). Yuzeydeki agizdan (keşif noktasi "oyuk") E ile iceri,
/// kapidan E ile disari. Iceride koruyucu dovusu (Sim: MoleFight, BatKnotFight) her kare isler; Sulama deligi islatir
/// (Capa.Sulama tek satir), Cekim'le getirilen tikac kayasi deligi tikar, kandil/Meşale yumagi dagitir; silah isabeti
/// gecici savas koprusunden (OyukTarget). Yenilince Oyuk temizlenir (Sim OyukProgress, kayit bolumu "oyuk") ve sandiklar
/// acilir. Dovus kayda girmez: cikinca bastan.
/// Ortak oyun: kalici durum kayit koprusuyle gider; dovus her oyuncunun kendi makinesinde isler (M3: kismen).
/// </summary>
public partial class OyukSystem : Node3D
{
    readonly Director _d;
    public readonly List<OyukRoom> Rooms = new();
    public OyukRoom Inside { get; private set; }
    public IOyukFight Fight { get; private set; }
    public MoleFight Mole => Fight as MoleFight;
    public BatKnotFight Bats => Fight as BatKnotFight;
    public MudFight Mud => Fight as MudFight;
    public ScorpionFight Scorpion => Fight as ScorpionFight;
    public MirrorFight Mirror => Fight as MirrorFight;
    MudVisual _mudVis;
    ScorpionVisual _scorpVis;
    MirrorVisual _mirrorVis;
    readonly System.Collections.Generic.List<(OyukPoint pt, Node3D node)> _points = new();
    int _lastCopies;
    MoleVisual _moleVis;
    BatVisual _batVis;
    OyukTarget _target;
    readonly List<(Node3D node, MeshInstance3D wet)> _holes = new();
    Label _bar;
    Vector3 _lastMouth;
    /// <summary>Sinama icin: son vurus, son oyuncu hasari.</summary>
    public float LastHit, Hurt;

    public OyukSystem(Director d) { _d = d; Name = "Oyuklar"; }

    OyukFile Def => _d.Data.Oyuk;
    OyukProgress P => _d.State.Oyuk;

    public override void _Ready()
    {
        var list = OyukRules.Oyuks(_d.State.Pois);
        for (int i = 0; i < list.Count; i++)
        {
            var g = Def.ForBiome(list[i].Biome);
            if (g == null) continue;
            var room = OyukRoom.Build(_d, list[i], g, i);
            AddChild(room);
            room.GlobalPosition = room.Center;
            Furnish(room);
            Rooms.Add(room);
        }
        _bar = new Label { Name = "OyukBar", Visible = false, HorizontalAlignment = HorizontalAlignment.Center };
        _bar.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
        _bar.Position = new Vector2(-260, 64);
        _bar.Size = new Vector2(520, 30);
        _bar.AddThemeFontSizeOverride("font_size", 22);
        _bar.AddThemeColorOverride("font_color", Palette.OffWhite);
        _bar.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.8f));
        _bar.AddThemeConstantOverride("outline_size", 6);
        _d.Hud.AddChild(_bar);
        GD.Print($"OYUK rooms {Rooms.Count}: " + string.Join(" ", Rooms.Select(r => $"{r.Poi.Id}({r.Guardian.Id} t{r.Tier})@{r.Center.X:0}")));
    }

    void Furnish(OyukRoom room)
    {
        var g = room.Guardian;
        // Sandiklar: kapinin karsisinda.
        for (int c = 0; c < g.Chests; c++)
        {
            var chest = new OyukChest { Name = $"Chest{c}", Slot = c };
            room.AddChild(chest);
            chest.Position = new Vector3((c - (g.Chests - 1) / 2f) * 2.4f, 0, -room.Radius + 2.2f);
            if (P.Of(room.Poi.Id).Looted.Contains(c)) chest.Open();
            room.Chests.Add(chest);
        }
        if (g.Kind == "kostebek")
        {
            // Tikac kayalari duvar dibinde (delik sayisindan fazla).
            for (int k = 0; k < g.PlugRocks; k++)
            {
                float a = Mathf.Pi * 0.6f + k * (Mathf.Pi * 0.8f / Mathf.Max(1, g.PlugRocks - 1));
                var rock = new PlugRock { Name = $"Plug{k}", Kg = g.PlugKg };
                room.AddChild(rock);
                rock.Position = new Vector3(Mathf.Cos(a) * (room.Radius - 2f), 0.45f, Mathf.Sin(a) * (room.Radius - 2f));
                room.Plugs.Add(rock);
            }
        }
        else if (g.Kind == "yarasa")
        {
            // Kandil direkleri yumagin altinda halka (isik yaricapinin icinde): her biri tek basina yeter.
            float ring = Mathf.Max(2f, g.LightRadius - 1.5f);
            for (int k = 0; k < g.Lamps; k++)
            {
                float a = -Mathf.Pi / 2f + k * Mathf.Tau / Mathf.Max(1, g.Lamps) + 0.5f;
                var lamp = new OyukLamp { Name = $"Lamp{k}" };
                room.AddChild(lamp);
                lamp.Position = new Vector3(Mathf.Cos(a) * ring, 0, Mathf.Sin(a) * ring);
                room.Lamps.Add(lamp);
            }
        }
    }

    // ---------- M4 (ata4): Buz Aynasi kopyalari ve gecici odalar ----------

    readonly System.Collections.Generic.Dictionary<MirrorFight.Copy, OyukPoint> _copyPts = new();

    /// <summary>Her kopya silahla vurulabilir hedef olur (Ates/Sok eritir); olen kopyanin hedefi kalkar.</summary>
    void SyncCopyTargets(OyukRoom room, MirrorFight mir)
    {
        foreach (var c in mir.Copies)
        {
            if (_copyPts.ContainsKey(c)) continue;
            var cc = c;
            var pt = new OyukPoint(() => room.Center + new Vector3(cc.X, 1.1f, cc.Z), 0.9f, () => mir.Copies.Contains(cc), h =>
            {
                int i = mir.Copies.IndexOf(cc);
                if (i >= 0 && mir.HitCopy(i, h.Amount, h.Type)) GD.Print("OYUK copy_melt");
            });
            _copyPts[c] = pt;
            _d.Atalar?.Targets.Add(pt);
        }
        foreach (var kv in _copyPts.ToList())
            if (!mir.Copies.Contains(kv.Key)) { _d.Atalar?.Targets.Remove(kv.Value); _copyPts.Remove(kv.Key); }
    }

    /// <summary>
    /// GECICI (ata4): yeni biyomlarin (Isil Sazlik, Kizil Kanyon, Ayaz Yaylasi) Oyuk noktalari biyom akisi main'e girene dek
    /// yoktur; sinama ve gelistirici bu biyomlarin koruyucusu icin sanal bir oda kurar (kesif noktasi olmadan; Enter ile girilir).
    /// Gercek noktalar gelince OyukRules.Oyuks bu odalari kendisi kurar ve bu yontem yalniz sinamada kalir.
    /// </summary>
    public OyukRoom AddTemporaryRoom(string guardianId, int tier)
    {
        var g = Def.Get(guardianId);
        var poi = new Poi { Id = $"oyuk_{g.Biome}_{tier}", Type = "oyuk", Biome = g.Biome, X = 240f, Z = 240f - tier * 12f };
        var room = OyukRoom.Build(_d, poi, g, Rooms.Count);
        AddChild(room);
        room.GlobalPosition = room.Center;
        Furnish(room);
        Rooms.Add(room);
        GD.Print($"OYUK temporary_room {poi.Id} guardian={g.Id} tier={room.Tier}");
        return room;
    }

    // ---------- Giris / cikis ----------

    public Poi MouthNear(Vector3 p)
    {
        float reach = Def.Interior.DoorReach + 2f;
        return Rooms.Select(r => r.Poi).FirstOrDefault(poi => new Vector2(p.X - poi.X, p.Z - poi.Z).Length() < reach && Mathf.Abs(p.Y - _d.World.Ground(new Vector3(poi.X, 0, poi.Z)).Y) < 4f);
    }

    public void Enter(OyukRoom room)
    {
        if (Inside != null) return;
        Inside = room;
        _lastMouth = _d.World.Ground(new Vector3(room.Poi.X, 0, room.Poi.Z));
        P.Of(room.Poi.Id).Entered++;
        _d.World.SetIndoor(true, room.Guardian.Ambient > 0 ? room.Guardian.Ambient : Def.Interior.Ambient);
        Teleport(room.DoorPos + (room.Center - room.DoorPos).Normalized() * 1.6f);
        if (!P.IsCleared(room.Poi.Id)) StartFight(room);
        if (_d.State.Pois.Any(p => p.Id == room.Poi.Id)) _d.State.Explore.Discover(room.Poi.Id); // gecici oda (ata4 sinamasi) noktasiz
        GD.Print($"OYUK enter {room.Poi.Id} guardian={room.Guardian.Id} tier={room.Tier} cleared={P.IsCleared(room.Poi.Id)}");
    }

    public void Exit()
    {
        if (Inside == null) return;
        var room = Inside;
        EndFight();
        Inside = null;
        _d.World.SetIndoor(false, 0);
        // Tasinan kaya oda disina cikmaz.
        if (_d.Capa.Held is PlugRock) _d.Capa.Drop();
        Teleport(_lastMouth + Vector3.Back * 3.2f);
        GD.Print($"OYUK exit {room.Poi.Id}");
    }

    void Teleport(Vector3 at)
    {
        _d.Player.GlobalPosition = at + Vector3.Up * 0.3f;
        _d.Player.Velocity = Vector3.Zero;
    }

    void StartFight(OyukRoom room)
    {
        var g = room.Guardian;
        var rng = new Rng((ulong)(room.Index * 7919 + P.Of(room.Poi.Id).Entered * 31 + 1));
        if (g.Kind == "kostebek")
        {
            var m = new MoleFight(g, room.Tier, rng);
            Fight = m;
            foreach (var (x, z) in m.Holes.Select(h => (h.X, h.Z)))
            {
                var hole = OyukHole.Make(Palette.Soil);
                room.AddChild(hole);
                hole.Position = new Vector3(x, 0, z);
                var wet = OyukHole.Puddle();
                hole.AddChild(wet);
                _holes.Add((hole, wet));
            }
            // Kayalar yerine doner (onceki dovusten tikali kalanlar dahil).
            for (int k = 0; k < room.Plugs.Count; k++)
            {
                var rock = room.Plugs[k];
                rock.Used = false; rock.Visible = true;
                float a = Mathf.Pi * 0.6f + k * (Mathf.Pi * 0.8f / Mathf.Max(1, room.Plugs.Count - 1));
                rock.Position = new Vector3(Mathf.Cos(a) * (room.Radius - 2f), 0.45f, Mathf.Sin(a) * (room.Radius - 2f));
            }
            _moleVis = new MoleVisual { Name = "KostebekBey" };
            room.AddChild(_moleVis);
            _target = new OyukTarget(() => _moleVis.GlobalPosition + Vector3.Up * 1.1f, g.Radius, m, () => m.Weak);
        }
        else if (g.Kind == "yarasa")
        {
            var b = new BatKnotFight(g, room.Tier);
            Fight = b;
            _batVis = new BatVisual { Name = "YarasaYumagi", RoomRadius = room.Radius };
            room.AddChild(_batVis);
            _batVis.Init(b.Bats, new Vector3(0, g.HangHeight, 0));
            foreach (var l in room.Lamps) l.SetLit(false);
            _target = new OyukTarget(() => _batVis.MainPos, g.Radius, b, () => true);
        }
        else if (g.Kind == "camur")
        {
            var m = new MudFight(g, room.Tier);
            Fight = m;
            _mudVis = new MudVisual { Name = "CamurDevi" };
            room.AddChild(_mudVis);
            // Gol (dev kuruyunca buraya yurur)
            room.AddChild(new MeshInstance3D { Name = "Gol", Mesh = new CylinderMesh { TopRadius = g.PoolRadius, BottomRadius = g.PoolRadius, Height = 0.06f }, MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("5A7A6A"), Roughness = 0.05f, Metallic = 0.2f }, Position = new Vector3(g.Pool[0], 0.04f, g.Pool[1]) });
            _target = new OyukTarget(() => _mudVis.GlobalPosition + Vector3.Up * 1.9f, g.Radius, m, () => m.Dry);
            _target.OnRaw = h => { if (h.Type == DamageType.Ates || (h.Procs & StatusProc.Burn) != 0) m.Heat(h.Amount); };
        }
        else if (g.Kind == "akrep")
        {
            var s = new ScorpionFight(g, room.Tier);
            Fight = s;
            _scorpVis = new ScorpionVisual { Name = "KuvarsAkrebi" };
            room.AddChild(_scorpVis);
            for (int i = 0; i < s.Stalactites.Count; i++)
            {
                int idx = i;
                var node = new Node3D { Name = "Sarkit" + i };
                node.AddChild(new MeshKit().Cylinder(0.5f, 0f, 2.6f, MeshKit.At(0, 0, 0), new Color("A89480"), 7).Instance());
                room.AddChild(node);
                node.Position = new Vector3(s.Stalactites[i].X, room.Height - 1.3f, s.Stalactites[i].Z);
                var pt = new OyukPoint(() => node.GlobalPosition, 1.1f, () => idx < s.Stalactites.Count && !s.Stalactites[idx].Fallen && s.Stalactites[idx].Fall < 0 && !s.Dead, h => { if (s.DropStalactite(idx)) GD.Print($"OYUK stalactite_drop {idx}"); });
                _points.Add((pt, node));
            }
            _target = new OyukTarget(() => _scorpVis.GlobalPosition + Vector3.Up * 1.1f, g.Radius, s, () => s.Cracked);
        }
        else
        {
            var mi = new MirrorFight(g, room.Tier, room.Radius);
            Fight = mi;
            _mirrorVis = new MirrorVisual { Name = "BuzAynasi" };
            room.AddChild(_mirrorVis);
            _mirrorVis.Position = new Vector3(mi.MirrorX, 0, mi.MirrorZ);
            _target = new OyukTarget(() => _mirrorVis.GlobalPosition + Vector3.Up * 3f, g.Radius, mi, () => mi.Vulnerable);
            _lastCopies = 0;
        }
        _target.OnHit = (got, crit) =>
        {
            LastHit = got;
            if (got > 0) { _moleVis?.Flash(); _batVis?.Flash(); }
        };
        LastHit = Hurt = 0;
    }

    void EndFight()
    {
        foreach (var (n, _) in _holes) n.QueueFree();
        _holes.Clear();
        _moleVis?.QueueFree(); _moleVis = null;
        _batVis?.QueueFree(); _batVis = null;
        _mudVis?.QueueFree(); _mudVis = null;
        _scorpVis?.QueueFree(); _scorpVis = null;
        if (_mirrorVis != null) { _mirrorVis.Clear(); _mirrorVis.QueueFree(); _mirrorVis = null; }
        foreach (var (pt, node) in _points) { _d.Atalar?.Targets.Remove(pt); node.QueueFree(); }
        _points.Clear();
        foreach (var cp in _copyPts.Values) _d.Atalar?.Targets.Remove(cp);
        _copyPts.Clear();
        if (_target != null) _d.Atalar?.Targets.Remove(_target);
        _target = null;
        Fight = null;
        _bar.Visible = false;
    }

    // ---------- Kare ----------

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        if (Inside == null) return;
        var room = Inside;
        // Duvardan kacan ya da asagi dusen oyuncu kapiya doner.
        if (!room.Contains(_d.Player.GlobalPosition)) Teleport(room.DoorPos);
        foreach (var l in room.Lamps.Where(l => l.Lit && l.Left > 0)) { l.Left -= dt; if (l.Left <= 0) l.SetLit(false); }
        if (Fight == null) { _bar.Visible = false; return; }
        if (Fight.Dead) { Cleared(room); return; }

        if (_target != null && _d.Atalar != null && !_d.Atalar.Targets.Contains(_target)) _d.Atalar.Targets.Add(_target);
        var (px, pz) = room.Local(_d.Player.GlobalPosition);
        if (Mole is { } m)
        {
            CheckPlugs(room, m);
            float dmg = m.Tick(dt, px, pz);
            if (dmg > 0) HurtPlayer(dmg, _moleVis.GlobalPosition);
            var (mx, mz) = m.Pos;
            _moleVis.Position = new Vector3(mx, 0, mz);
            _moleVis.LookAt(_d.Player.GlobalPosition with { Y = _moleVis.GlobalPosition.Y }, Vector3.Up, true);
            _moleVis.Show(m, dt);
            for (int i = 0; i < _holes.Count; i++) _holes[i].wet.Visible = m.Holes[i].Wet > 0;
        }
        else if (Bats is { } b)
        {
            b.Lit = LightNearKnot(room);
            float dmg = b.Tick(dt, px, pz);
            if (dmg > 0) { _batVis.Swoop(); HurtPlayer(dmg, room.World(0, 0, room.Guardian.HangHeight)); }
            _batVis.Show(b, dt, _d.Player.GlobalPosition - room.GlobalPosition);
        }
        else if (Mud is { } mu)
        {
            // Mesale ya da yerlestirilmis isik dev'e yakinsa kurutur (Alev basligi gelene dek)
            var gp = _mudVis.GlobalPosition;
            bool torch = (_d.Capa.HeldItem == "mesale" && _d.Player.GlobalPosition.DistanceTo(gp) < 3.5f) || _d.Furniture.Any(f => IsInstanceValid(f) && f.Def.LightRange > 0 && room.Contains(f.GlobalPosition) && f.GlobalPosition.DistanceTo(gp) < 3.5f);
            if (torch) mu.Heat(40f * dt);
            float dmg = mu.Tick(dt, px, pz);
            if (dmg > 0) { HurtPlayer(dmg, gp); Fx.Burst(gp + Vector3.Up, new Color("4A3A26"), 20, 5f, 0.2f, 0.6f); }
            _mudVis.Position = new Vector3(mu.X, 0, mu.Z);
            _mudVis.Show(mu, dt);
        }
        else if (Scorpion is { } sc)
        {
            float dmg = sc.Tick(dt, px, pz);
            if (dmg > 0) { HurtPlayer(dmg, _scorpVis.GlobalPosition); Fx.Burst(_scorpVis.GlobalPosition + Vector3.Up * 2.5f, new Color("6A3A5A"), 16, 6f, 0.14f, 0.6f, true); Sfx.Play("firlatma", _scorpVis.GlobalPosition, 0f, 0.6f); }
            _scorpVis.Position = new Vector3(sc.X, 0, sc.Z);
            _scorpVis.Show(sc, dt, _d.Player.GlobalPosition);
            for (int i = 0; i < _points.Count && i < sc.Stalactites.Count; i++)
            {
                var st = sc.Stalactites[i];
                _points[i].node.Visible = !st.Fallen;
                if (st.Fall >= 0) _points[i].node.Position += Vector3.Down * (room.Height / Mathf.Max(0.1f, room.Guardian.FallSeconds)) * dt * 0.8f;
                else if (!st.Fallen) _points[i].node.Position = new Vector3(st.X, room.Height - 1.3f, st.Z);
                if (_d.Atalar != null && !st.Fallen && st.Fall < 0 && !_d.Atalar.Targets.Contains(_points[i].pt)) _d.Atalar.Targets.Add(_points[i].pt);
            }
        }
        else if (Mirror is { } mir)
        {
            float dmg = mir.Tick(dt, px, pz);
            if (dmg > 0) HurtPlayer(dmg, room.World(mir.Copies.Count > 0 ? mir.Copies[0].X : 0, mir.Copies.Count > 0 ? mir.Copies[0].Z : 0, 1f));
            _mirrorVis.Show(mir, room.Center, dt);
            SyncCopyTargets(room, mir);
        }
        UpdateBar(room);
        if (Fight.Dead) Cleared(room);
    }

    void HurtPlayer(float dmg, Vector3 from)
    {
        Hurt += dmg;
        _d.Player.Hurt(dmg, from);
    }

    void CheckPlugs(OyukRoom room, MoleFight m)
    {
        foreach (var rock in room.Plugs)
        {
            if (rock.Used || rock.Held) continue;
            var (x, z) = room.Local(rock.GlobalPosition);
            int hole = m.HoleNear(x, z, room.Guardian.PlugRadius);
            if (hole < 0 || m.Holes[hole].Plugged) continue;
            if (rock.GetNodeOrNull<ThrowMotion>(ThrowMotion.NodeName) != null) continue; // havada
            if (rock.GlobalPosition.Y - room.Center.Y > 1.4f) continue;
            m.Plug(hole);
            rock.Used = true;
            rock.Position = new Vector3(m.Holes[hole].X, 0.35f, m.Holes[hole].Z);
            Sfx.Play("parca_darbe", rock.GlobalPosition, -4f);
            GD.Print($"OYUK plug hole={hole} state={m.State}");
        }
    }

    /// <summary>Yumagin altinda isik var mi: yanan kandil, odaya kurulan Meşale, elde tutulan Meşale.</summary>
    bool LightNearKnot(OyukRoom room)
    {
        var b = Bats;
        bool Lights(Vector3 p) { var (x, z) = room.Local(p); return b.InLight(x, z, 0, 0); }
        if (room.Lamps.Any(l => l.Lit && Lights(l.GlobalPosition))) return true;
        if (_d.Furniture.Any(f => IsInstanceValid(f) && f.Def.LightRange > 0 && room.Contains(f.GlobalPosition) && Lights(f.GlobalPosition))) return true;
        return _d.Capa.HeldItem == "mesale" && Lights(_d.Player.GlobalPosition);
    }

    void UpdateBar(OyukRoom room)
    {
        var f = Fight;
        int n = 20, full = Mathf.CeilToInt(f.Hp / Mathf.Max(1, f.MaxHp) * n);
        string state = Mole is { } m ? m.State switch
        {
            MoleFight.Phase.Hidden => Loc.T("world.oyuk_hidden"),
            MoleFight.Phase.Stunned => Loc.T("world.oyuk_stunned"),
            MoleFight.Phase.Trapped => m.Weak ? Loc.T("world.oyuk_trapped_weak") : Loc.T("world.oyuk_trapped"),
            _ => Loc.T("world.oyuk_up"),
        } : Bats is { } b ? (b.Scattered ? Loc.T("world.oyuk_bats_scattered") : Loc.T("world.oyuk_bats_grouped"))
          : Mud is { } mu ? (mu.Dry ? Loc.T("world.oyuk_mud_dry") : Loc.T("world.oyuk_mud_wet"))
          : Scorpion is { } sc ? (sc.Cracked ? Loc.T("world.oyuk_scorpion_cracked") : Loc.T("world.oyuk_scorpion_armored"))
          : Mirror is { } mir ? (mir.Copies.Count > 0 ? Loc.T("world.oyuk_mirror_copies", mir.Copies.Count) : Loc.T("world.oyuk_mirror_open")) : "";
        _bar.Text = $"{room.Guardian.Name.ToUpperInvariant()}  {new string('▮', full)}{new string('▯', n - full)}  {f.Hp:0}/{f.MaxHp:0}  · {state}";
        _bar.Visible = true;
    }

    void Cleared(OyukRoom room)
    {
        var rec = P.Of(room.Poi.Id);
        if (rec.Cleared) return;
        rec.Cleared = true;
        ForwardClear(room); // ortak: istemcide host'a da bildirilir (OyukSystem.Net.cs)
        GD.Print($"OYUK cleared {room.Poi.Id} guardian={room.Guardian.Id} tier={room.Tier}");
        Events.RaiseOyukCleared(room.Poi.Id);
        Sfx.Play("kabarmis_patlama", _target?.Center ?? room.Center, -2f);
        _d.Hud.Hint(Loc.T("world.oyuk_defeated", room.Guardian.Name), 3f);
        EndFight();
    }

    // ---------- Sulama ve E ----------

    /// <summary>Capa.Sulama: nisan odadaki bir delikteyse su delige gider (depo harcanir). Iceride her durumda true (tarla yok).</summary>
    public bool WaterAt(Vector3 aim, WaterTank tank)
    {
        if (Inside == null) return false;
        if (Mole is not { } m) return true;
        var (x, z) = Inside.Local(aim);
        int hole = m.HoleNear(x, z, 1.6f);
        if (hole < 0) return true;
        if (!tank.TryWaterCell()) { _d.Hud.Hint(Loc.T("world.tank_empty_short"), 0.5f); return true; }
        bool flooded = m.Water(hole, tank.PerCell);
        if (flooded) GD.Print($"OYUK flood hole={hole} state={m.State}");
        if (GD.Randf() < 0.4f) Sfx.Play("water", aim, -8f);
        Fx.Burst(aim + Vector3.Up * 0.2f, Palette.SkyNoon, 4, 2f, 0.05f, 0.3f);
        return true;
    }

    public (string verb, string obj)? Prompt(Vector3 p)
    {
        if (Inside == null)
            return MouthNear(p) is { } poi ? (Loc.T("world.oyuk_enter"), Loc.T("world.oyuk") + (P.IsCleared(poi.Id) ? " " + Loc.T("world.oyuk_clean") : "")) : null;
        var room = Inside;
        if (p.DistanceTo(room.DoorPos) < Def.Interior.DoorReach + 0.8f) return (Loc.T("world.oyuk_exit"), Loc.T("world.oyuk"));
        var chest = room.Chests.FirstOrDefault(c => !c.Opened && c.GlobalPosition.DistanceTo(p) < 2.2f);
        if (chest != null) return P.IsCleared(room.Poi.Id) ? (Loc.T("world.oyuk_open"), Loc.T("world.oyuk_chest")) : (Loc.T("world.oyuk_locked"), Loc.T("world.oyuk_chest_locked"));
        var lamp = room.Lamps.FirstOrDefault(l => !l.Lit && l.GlobalPosition.DistanceTo(p) < 2.2f);
        if (lamp != null) return (Loc.T("world.oyuk_light"), Loc.T("world.oyuk_lamp"));
        return null;
    }

    /// <summary>E: giris, cikis, sandik, kandil. Bir sey yaptiysa true.</summary>
    public bool Interact()
    {
        var p = _d.Player.GlobalPosition;
        if (Inside == null)
        {
            var poi = MouthNear(p);
            if (poi == null) return false;
            Enter(Rooms.First(r => r.Poi == poi));
            return true;
        }
        var room = Inside;
        if (p.DistanceTo(room.DoorPos) < Def.Interior.DoorReach + 0.8f) { Exit(); return true; }
        var chest = room.Chests.FirstOrDefault(c => !c.Opened && c.GlobalPosition.DistanceTo(p) < 2.2f);
        if (chest != null)
        {
            if (ForwardChest(room, chest)) return true; // ortak: odul host'ta aktorun cantasina
            var loot = P.OpenChest(room.Poi.Id, room.Guardian, room.Tier, chest.Slot);
            if (loot.Count == 0) { _d.Hud.Hint(Loc.T("world.oyuk_chest_locked_hint"), 1.5f); return true; }
            chest.Open();
            foreach (var (item, n) in loot)
            {
                _d.State.Backpack.Add(item, n);
                Events.RaiseGathered(item, n);
            }
            _d.Hud.Hint(string.Join(" · ", loot.Select(kv => $"+{kv.Value} {ItemNames.Of(kv.Key)}")), 2.5f);
            GD.Print($"OYUK chest {room.Poi.Id}#{chest.Slot} " + string.Join(",", loot.Select(kv => $"{kv.Key}={kv.Value}")));
            return true;
        }
        var lamp = room.Lamps.FirstOrDefault(l => !l.Lit && l.GlobalPosition.DistanceTo(p) < 2.2f);
        if (lamp != null) { lamp.SetLit(true, room.Guardian.LampSeconds); GD.Print($"OYUK lamp lit {lamp.Name}"); return true; }
        return false;
    }
}
