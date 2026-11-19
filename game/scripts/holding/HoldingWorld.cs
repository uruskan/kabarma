using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Bereket Holding'in dunyadaki yuzu (GDD §19; holding, M3). Kural ve durum Sim'de (<see cref="HoldingSystem"/>, kayit
/// bolumu "holding"); bu dugum her kare durumdan cizer (kamp, birim, kervan araci), host'ta birimleri yurutur ve kervani
/// surer, gunduz oyuncuyu Perde'den iter, Capa mermilerinin kampa, birimlere ve araca degmesini Sim'e iletir.
/// Ortak oyun: istemci yalniz cizer (HoldingWorld.Net.cs). Birim hasari savas akisinin Combat.Apply'indan gecer
/// (HoldingSystem.HitUnit); yapi ve arac cani duz HP'dir.
/// </summary>
public partial class HoldingWorld : Node3D
{
    public Director D { get; }
    public HoldingSystem H => D.State.Holding;
    public HoldingFile Def => D.Data.Holding;
    public bool Host => !D.NetClient;
    public float ClaimRadius => Def.Raid.ClaimRadius;

    readonly Dictionary<int, HoldingCampNode> _camps = new();
    readonly Dictionary<int, HoldingUnitActor> _units = new();
    readonly Dictionary<int, Node3D> _trucks = new();
    readonly Dictionary<int, float> _provoked = new();
    readonly Dictionary<int, float> _wait = new();
    Inventory _lastBag;
    bool _perdeHinted;

    public HoldingWorld(Director d) { D = d; Name = "HoldingWorld"; }

    /// <summary>Suren Mudur dovusu (M4); Genel Merkez Cekirdegi dusunce kurulur, Sim durumu bosalinca kalkar.</summary>
    public MudurEncounter Encounter { get; private set; }

    void EncounterTick()
    {
        if (H.State.Mudur != null && Encounter == null) { Encounter = new MudurEncounter(this); AddChild(Encounter); }
        else if (H.State.Mudur == null && Encounter != null) { Encounter.QueueFree(); Encounter = null; }
    }

    public IReadOnlyDictionary<int, HoldingUnitActor> Units => _units;
    public HoldingCampNode CampNode(int id) => _camps.GetValueOrDefault(id);
    public Node3D Truck(int campId) => _trucks.GetValueOrDefault(campId);

    public override void _Ready()
    {
        H.Team = () => D.Net?.PlayerCount ?? 1; // Mudur cani ekip boyuna gore
        H.CampFounded += c =>
        {
            GD.Print($"HOLDING founded camp#{c.Id} slot={c.Slot} kind={c.Kind} at {c.X:0},{c.Z:0} level={c.Level}");
            bool hq = c.Kind == "hq";
            D.Hud.Big(hq ? Loc.T("holding.big_hq") : Loc.T("holding.big_camp"), Palette.HoldingRed, 3f);
            D.Hud.Hint(hq ? Loc.T("holding.hint_hq") : c.Slot == 1 ? Loc.T("holding.hint_branch") : Loc.T("holding.hint_new_branch", c.Slot), 5f);
        };
        H.LevelUp += (c, l) => { GD.Print($"HOLDING camp#{c.Id} levelup {l}"); D.Hud.Big($"Holding kampı · Sv. {l}", Palette.HoldingRed, 2f); };
        H.CaravanDeparted += (c, k) => GD.Print($"HOLDING caravan camp#{c.Id} day={k.Day} guards={c.Units.Count(u => u.Job == "caravan")}");
        H.Ambushed += OnAmbushed;
        H.StructureDestroyed += OnStructureDestroyed;
        H.RaidLaunched += (c, team) => { GD.Print($"HOLDING raid camp#{c.Id} n={team.Count}"); D.Hud.Big("HOLDING BASKINI", Palette.HoldingRed, 2.5f); };
        InitNet(); // HoldingWorld.Net.cs
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        if (Host) TickCaravans(dt);
        Reconcile(dt);
        EncounterTick();
        PushOutOfPerde(dt);
        foreach (var k in _provoked.Keys.ToList()) _provoked[k] = Mathf.Max(0, _provoked[k] - dt);
    }

    // ---------------------------------------------------------------- durumdan cizim

    void Reconcile(float dt)
    {
        var seen = new HashSet<int>();
        var seenCamps = new HashSet<int>();
        foreach (var c in H.State.Camps)
        {
            seenCamps.Add(c.Id);
            if (!_camps.TryGetValue(c.Id, out var cn)) { cn = new HoldingCampNode(this, c.Id); AddChild(cn); _camps[c.Id] = cn; }
            cn.Sync(c, H.PerdeUp(c));
            SyncTruck(c, dt);
            for (int i = 0; i < c.Units.Count; i++)
            {
                var u = c.Units[i];
                if (!u.Alive) continue;
                seen.Add(u.Id);
                if (!_units.TryGetValue(u.Id, out var a))
                {
                    if (u.X == c.X && u.Z == c.Z) // yeni birim: kampin cevresine dagilir
                    {
                        float ang = u.Id * 2.39996f;
                        u.X += Mathf.Cos(ang) * (5f + u.Id % 4); u.Z += Mathf.Sin(ang) * (5f + u.Id % 4);
                    }
                    a = new HoldingUnitActor(this, c.Id, u, H.Def.Unit(u.Kind));
                    AddChild(a);
                    a.GlobalPosition = D.World.Ground(new Vector3(u.X, 0, u.Z));
                    _units[u.Id] = a;
                }
                a.Step(dt, c, u, Host);
                if (Host) { u.X = a.GlobalPosition.X; u.Z = a.GlobalPosition.Z; }
            }
        }
        foreach (var id in _units.Keys.Where(k => !seen.Contains(k)).ToList())
        {
            var a = _units[id];
            a.DropCrop();
            if (IsInstanceValid(a)) { Fx.Burst(a.Center, Palette.HoldingRed, 8, 2.5f, 0.08f, 0.5f, false, 4f); a.QueueFree(); }
            _units.Remove(id);
        }
        foreach (var id in _camps.Keys.Where(k => !seenCamps.Contains(k)).ToList()) { _camps[id].QueueFree(); _camps.Remove(id); }
    }

    void SyncTruck(HoldingCamp c, float dt)
    {
        var k = c.Caravan;
        bool on = k != null && k.Phase != "done";
        if (!_trucks.TryGetValue(c.Id, out var t))
        {
            if (!on) return;
            t = BuildTruck();
            AddChild(t);
            t.GlobalPosition = D.World.Ground(new Vector3(k.X, 0, k.Z));
            _trucks[c.Id] = t;
        }
        t.Visible = on;
        if (!on) return;
        var to = D.World.Ground(new Vector3(k.X, 0, k.Z));
        var from = t.GlobalPosition;
        t.GlobalPosition = Host || from.DistanceTo(to) > 10f ? to : from.Lerp(to, Mathf.Min(1f, dt * 6f));
        var dir = new Vector3(to.X - from.X, 0, to.Z - from.Z);
        if (dir.LengthSquared() > 1e-5f) t.Rotation = new Vector3(0, Mathf.LerpAngle(t.Rotation.Y, Mathf.Atan2(dir.X, dir.Z), Mathf.Min(1f, dt * 4f)), 0);
        if (t.GetNodeOrNull<Label3D>("Label") is Label3D l) l.Text = Loc.T("holding.caravan_label", Mathf.CeilToInt(k.VehicleHp));
    }

    /// <summary>YER TUTUCU kervan araci: kirmizi kabin, kasa, sandiklar (gercek model uretim isteginde).</summary>
    static Node3D BuildTruck()
    {
        var root = new Node3D { Name = "HoldingTruck" };
        var k = new MeshKit();
        k.Pillow(new Vector3(2.2f, 1.6f, 1.8f), MeshKit.At(0, 1.3f, 1.6f), Palette.HoldingRed, 0.25f);
        k.Pillow(new Vector3(2.3f, 0.5f, 3.6f), MeshKit.At(0, 0.75f, -0.8f), Palette.DarkGrey, 0.15f);
        k.Pillow(new Vector3(0.9f, 0.9f, 0.9f), MeshKit.At(-0.5f, 1.45f, -0.3f), Palette.Wood, 0.12f);
        k.Pillow(new Vector3(0.9f, 0.9f, 0.9f), MeshKit.At(0.5f, 1.45f, -1.4f), Palette.Wood, 0.12f);
        foreach (var (x, z) in new[] { (-1.1f, 1.6f), (1.1f, 1.6f), (-1.1f, -1.6f), (1.1f, -1.6f) })
            k.Cylinder(0.45f, 0.45f, 0.35f, MeshKit.At(new Vector3(x, 0.45f, z), new Vector3(0, 0, Mathf.Pi * 0.5f)), Palette.NightDeep);
        root.AddChild(k.Instance());
        root.AddChild(new Label3D { Name = "Label", Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, Position = new Vector3(0, 3.2f, 0), FontSize = 40, OutlineSize = 10, Modulate = Palette.HoldingRed.Lightened(0.4f), PixelSize = 0.008f });
        return root;
    }

    // ---------------------------------------------------------------- kervan (host)

    void TickCaravans(float dt)
    {
        var cal = D.State.Calendar;
        var kd = Def.Caravan;
        foreach (var c in H.State.Camps)
        {
            if (c.Destroyed || H.IsHq(c)) continue;
            var (dx, dz) = H.DepotOf(c); // kamp 1: caravan.depot; 2. ve 3. kamp: kampa gore teslim noktasi
            var depot = new Vector3(dx, 0, dz);
            var k = c.Caravan;
            if ((k == null || k.Phase == "done") && cal.Running && !cal.IsNight && cal.TimeOfDay >= kd.DepartTime && c.LastCaravanDay < cal.Day)
            {
                k = H.DepartCaravan(c.Id, cal.Day);
                _wait[c.Id] = kd.WaitSeconds;
            }
            if (k == null || k.Phase == "done") continue;
            var home = new Vector3(c.X, 0, c.Z);
            var pos = new Vector3(k.X, 0, k.Z);
            switch (k.Phase)
            {
                case "out":
                    pos = pos.MoveToward(depot, kd.Speed * dt);
                    if (pos.DistanceTo(depot) < 0.5f) k.Phase = "wait";
                    break;
                case "wait":
                    _wait[c.Id] = _wait.GetValueOrDefault(c.Id, kd.WaitSeconds) - dt;
                    if (_wait[c.Id] <= 0) k.Phase = "back";
                    break;
                case "back":
                    pos = pos.MoveToward(home, kd.Speed * dt);
                    if (pos.DistanceTo(home) < 1f) { H.CaravanHome(c.Id); GD.Print($"HOLDING caravan camp#{c.Id} home"); }
                    break;
            }
            k.X = pos.X; k.Z = pos.Z;
        }
    }

    /// <summary>Kervan korumasinin araca gore yeri (sira kimlige gore, iki yanda).</summary>
    public Vector3 EscortSlot(int campId, int unitId)
    {
        var c = H.Camp(campId);
        var k = c?.Caravan;
        if (k == null) return new Vector3(c?.X ?? 0, 0, c?.Z ?? 0);
        var list = c.Units.Where(u => u.Job == "caravan").Select(u => u.Id).OrderBy(i => i).ToList();
        int i = Mathf.Max(0, list.IndexOf(unitId));
        var t = _trucks.GetValueOrDefault(campId);
        var basis = t != null ? t.GlobalBasis : Basis.Identity;
        var off = new Vector3(i % 2 == 0 ? -2.6f : 2.6f, 0, 2.5f - (i / 2) * 4.5f);
        return new Vector3(k.X, 0, k.Z) + basis * off;
    }

    // ---------------------------------------------------------------- Perde ve hedefler

    /// <summary>Gunduz Perde'si yerel oyuncuyu disari iter (safakta icerde kalan da itilir).</summary>
    void PushOutOfPerde(float dt)
    {
        var p = D.Player;
        if (p == null) return;
        foreach (var c in H.State.Camps)
        {
            if (!H.PerdeUp(c)) continue;
            var d = new Vector3(p.GlobalPosition.X - c.X, 0, p.GlobalPosition.Z - c.Z);
            float r = Def.Camp.PerdeRadius;
            if (d.Length() >= r) continue;
            var dir = d.LengthSquared() < 0.01f ? Vector3.Back : d.Normalized();
            float deep = r - d.Length();
            var np = p.GlobalPosition + dir * Mathf.Min(deep + 0.05f, Def.Camp.PerdePush * (deep > 3f ? 3f : 1f) * dt);
            np.Y = Mathf.Max(np.Y, D.World.HeightAt(np.X, np.Z) + 0.05f); // yokusta toprağa gömülmesin
            p.GlobalPosition = np;
            if (!_perdeHinted) { _perdeHinted = true; D.Hud.Hint(Loc.T("holding.perde_hint"), 4f); }
        }
    }

    public bool InPerdeDay(int campId, Vector3 pos) => H.Camp(campId) is HoldingCamp c && H.PerdeUp(c) && H.InPerde(c, pos.X, pos.Z);
    public float ProvokedAt(int campId) => _provoked.GetValueOrDefault(campId);
    public void Provoke(int campId) => _provoked[campId] = Def.Camp.ProvokeSeconds;

    /// <summary>En yakin ayaktaki oyuncu (ortak oyunda butun oyuncular). (konum, es, var mi)</summary>
    public (Vector3 pos, byte peer, bool has) NearestTarget(Vector3 from)
    {
        (Vector3, byte, bool) best = (Vector3.Zero, 0, false);
        float bd = float.MaxValue;
        IEnumerable<(byte peer, Vector3 pos, bool down)> players = D.Net != null ? D.Net.Players() : new[] { ((byte)0, D.Player.GlobalPosition, D.Player.Down) };
        foreach (var (peer, pos, down) in players)
        {
            if (down) continue;
            float d = from.DistanceSquaredTo(pos);
            if (d < bd) { bd = d; best = (pos, peer, true); }
        }
        return best;
    }

    /// <summary>Birim ates eder: iz cizgisi, hedef oyuncu hasar alir (uzak oyuncuya "holding_hurt" bolumuyle).</summary>
    public void Shoot(HoldingUnitActor a, Vector3 target, byte peer, float dmg)
    {
        var muzzle = a.Center + Vector3.Up * 0.3f;
        var hit = target + Vector3.Up * 1.0f;
        Tracer(muzzle, hit);
        Sfx.Play("civi", muzzle, -8f);
        byte local = D.Net?.LocalPeer ?? 0;
        if (peer == local) D.Player.Hurt(dmg, a.GlobalPosition);
        else HurtRemote(peer, dmg, a.GlobalPosition);
    }

    void Tracer(Vector3 a, Vector3 b)
    {
        var mat = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = Palette.HoldingRed.Lightened(0.3f), EmissionEnabled = true, Emission = Palette.HoldingRed, EmissionEnergyMultiplier = 2f };
        float len = a.DistanceTo(b);
        var m = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.03f, BottomRadius = 0.03f, Height = len, RadialSegments = 6, Rings = 1 }, MaterialOverride = mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(m);
        var dir = (b - a) / Mathf.Max(0.001f, len);
        var side = Mathf.Abs(dir.Dot(Vector3.Up)) > 0.95f ? Vector3.Right : Vector3.Up.Cross(dir).Normalized();
        m.GlobalTransform = new Transform3D(new Basis(side, dir, side.Cross(dir)), (a + b) * 0.5f);
        GetTree().CreateTimer(0.08).Timeout += () => { if (IsInstanceValid(m)) m.QueueFree(); };
    }

    // ---------------------------------------------------------------- baskin ekibi

    public bool IsCarrier(HoldingUnitActor a)
    {
        var raid = _units.Values.Where(x => x.CampId == a.CampId && x.Job == "raid" && IsInstanceValid(x)).OrderBy(x => x.UnitId).Take(Def.Raid.Carriers);
        return raid.Contains(a);
    }

    /// <summary>
    /// Tapudaki en yakin kabarik urun: yerdeki (kimse tasimiyor) ya da tarlada duran (tasiyici varinca soker, PlotView.Lift).
    /// </summary>
    public Node3D FreeCrop(Vector3 from)
    {
        float reach = ClaimRadius * 2f;
        var loose = GetTree().GetNodesInGroup("swollen").OfType<SwollenCrop>()
            .Where(c => IsInstanceValid(c) && !c.Gone && !c.Held && c.GlobalPosition.DistanceTo(D.OcakPos) < reach)
            .Cast<Node3D>();
        var planted = D.FarmView == null ? Enumerable.Empty<Node3D>() : D.FarmView.Views
            .Where(v => v.Plot.Swollen != null && v.Plot.Planted && !v.Plot.Wild && v.Lifted == null && v.GlobalPosition.DistanceTo(D.OcakPos) < reach)
            .Cast<Node3D>();
        return loose.Concat(planted).OrderBy(n => n.GlobalPosition.DistanceSquaredTo(from)).FirstOrDefault();
    }

    public void RaidEntered(int campId)
    {
        var c = H.Camp(campId);
        bool first = c != null && c.RaidSent && !c.RaidEntered;
        H.OnRaidEntered(campId);
        if (!first) return;
        GD.Print($"HOLDING raid entered camp#{campId} husumet={c.Husumet:0}");
        D.Hud.Big(Loc.T("holding.entered_claim"), Palette.HoldingRed, 2f);
    }

    public void Stolen(HoldingUnitActor a, HoldingUnit u)
    {
        string crop = a.Carried?.CropId ?? "?";
        a.ConsumeCrop();
        u.Carrying = false;
        GD.Print($"HOLDING stolen {crop} camp#{a.CampId}");
        D.Hud.Big(Loc.T("holding.stole_crop"), Palette.HoldingRed, 2f);
    }

    // ---------------------------------------------------------------- vuruslar

    enum HitKind { Structure, Unit, Caravan, Shield }

    readonly record struct HitFound(HitKind Kind, int Camp, string Structure, int Unit, Vector3 Point);

    /// <summary>
    /// Capa mermisi bu karede from->to dogrusunda Holding'e degdi mi (yapi, birim, kervan araci ya da gunduz Perde'si);
    /// degdiyse hasar verir ve true doner. Istemcide istek host'a gider ("holding_hit").
    /// </summary>
    public bool ProjectileHit(Vector3 from, Vector3 to, float radius, float damage, ISet<Node3D> already = null)
    {
        if (H.State.Camps.Count == 0) return false;
        if (Find(from, to, radius, already) is not HitFound f) return false;
        if (f.Kind == HitKind.Shield) { Fx.Burst(f.Point, Palette.HoldingRed.Lightened(0.4f), 6, 2f, 0.06f, 0.35f, true, 0f); return true; }
        if (ForwardHit(f, damage)) { Fx.Burst(f.Point, Palette.OffWhite, 5, 2f, 0.06f, 0.3f, true, -4f); return true; } // HoldingWorld.Net.cs
        Apply(f, damage, D.State.Backpack);
        return true;
    }

    HitFound? Find(Vector3 from, Vector3 to, float radius, ISet<Node3D> already)
    {
        var seg = to - from;
        float len2 = Mathf.Max(0.0001f, seg.LengthSquared());
        bool Touch(Vector3 center, float r) { float t = Mathf.Clamp((center - from).Dot(seg) / len2, 0, 1); return (from + seg * t).DistanceTo(center) <= radius + r; }
        foreach (var c in H.State.Camps)
        {
            if (c.Destroyed) continue;
            float pr = Def.Camp.PerdeRadius;
            bool fromIn = H.InPerde(c, from.X, from.Z), toIn = H.InPerde(c, to.X, to.Z);
            if (H.PerdeUp(c) && !fromIn && toIn) return new HitFound(HitKind.Shield, c.Id, "", 0, to);
            if (new Vector2(to.X - c.X, to.Z - c.Z).Length() > pr + 60f) continue;
            if (_camps.TryGetValue(c.Id, out var cn))
                foreach (var (id, center, r, node) in cn.Targets(c))
                {
                    if ((already?.Contains(node) ?? false) || !Touch(center, r)) continue;
                    already?.Add(node); // Tirpan: bir savurus bir yapiya bir kez
                    return new HitFound(HitKind.Structure, c.Id, id, 0, center);
                }
        }
        foreach (var a in _units.Values)
        {
            if (!IsInstanceValid(a) || (already?.Contains(a) ?? false)) continue;
            if (!Touch(a.Center, a.HitRadius)) continue;
            already?.Add(a);
            return new HitFound(HitKind.Unit, a.CampId, "", a.UnitId, a.Center);
        }
        foreach (var (campId, t) in _trucks)
        {
            if (!IsInstanceValid(t) || !t.Visible || (already?.Contains(t) ?? false)) continue;
            var center = t.GlobalPosition + Vector3.Up * 1.2f;
            if (!Touch(center, 1.9f)) continue;
            already?.Add(t);
            return new HitFound(HitKind.Caravan, campId, "", 0, center);
        }
        return null;
    }

    /// <summary>Host: vurusu Sim'e uygular; ganimet vuranin cantasina (ortak oyunda aktorun cantasi).</summary>
    void Apply(HitFound f, float damage, Inventory bag)
    {
        _lastBag = bag;
        switch (f.Kind)
        {
            case HitKind.Structure:
                var r = H.HitStructure(f.Camp, f.Structure, damage, out var loot);
                Provoke(f.Camp);
                if (r == HoldingHit.Locked) { Fx.Text3D(f.Point + Vector3.Up * 2f, Loc.T("holding.locked_first"), Palette.Warning, 48, 1.2f, 1.2f); return; }
                if (r == HoldingHit.Blocked) { Fx.Burst(f.Point, Palette.HoldingRed.Lightened(0.4f), 6, 2f, 0.06f, 0.35f, true, 0f); return; }
                Fx.DamageNumber(f.Point + Vector3.Up * 1.5f, damage, false);
                Fx.Burst(f.Point, Palette.Wood, 6, 3f, 0.1f, 0.5f, false, 9f);
                break;
            case HitKind.Unit:
                float done = H.HitUnit(f.Unit, damage);
                Provoke(f.Camp);
                if (done > 0) { Fx.DamageNumber(f.Point + Vector3.Up * 0.8f, done, false); Fx.Burst(f.Point, Palette.HoldingRed, 6, 2.5f, 0.07f, 0.4f, false, 6f); }
                if (H.Unit(f.Unit) is HoldingUnit u && !u.Alive) GD.Print($"HOLDING unit #{u.Id} {u.Kind} down");
                break;
            case HitKind.Caravan:
                float dv = H.HitCaravan(f.Camp, damage);
                Provoke(f.Camp);
                if (dv > 0) { Fx.DamageNumber(f.Point + Vector3.Up, dv, false); Fx.Burst(f.Point, Palette.DarkGrey, 6, 3f, 0.08f, 0.4f, false, 9f); }
                break;
        }
    }

    void OnStructureDestroyed(HoldingCamp c, string id, HoldingLoot loot)
    {
        var bag = _lastBag ?? D.State.Backpack;
        var left = D.State.ApplyLoot(loot, bag);
        DropLeftovers(left, c);
        string name = Def.Camp.Structures.FirstOrDefault(s => s.Id == id)?.Name ?? id;
        GD.Print($"HOLDING destroyed camp#{c.Id} {id} credits={loot.Credits} items={string.Join(",", loot.Items.Select(kv => kv.Key + "x" + kv.Value))} token={loot.Token}");
        D.Hud.Big(Loc.T("holding.destroyed", name, LootText(loot)), Palette.Warning, 2.5f);
        Sfx.Play("pop", new Vector3(c.X, D.World.HeightAt(c.X, c.Z) + 2f, c.Z), 0f);
        Events.RaiseHoldingRaided(c.Id, id);
        if (Def.Camp.Structures.Any(s => s.Id == id && s.Core))
        {
            Events.RaiseHoldingCoreDestroyed(c.Id);
            D.Hud.Big(Loc.T("holding.core_fell"), Palette.Warning, 3f);
        }
    }

    void OnAmbushed(HoldingCamp c, HoldingLoot loot)
    {
        var bag = _lastBag ?? D.State.Backpack;
        var left = D.State.ApplyLoot(loot, bag);
        DropLeftovers(left, c);
        GD.Print($"HOLDING ambush camp#{c.Id} credits={loot.Credits} items={string.Join(",", loot.Items.Select(kv => kv.Key + "x" + kv.Value))}");
        D.Hud.Big(Loc.T("holding.caravan_fell", LootText(loot)), Palette.Warning, 2.5f);
        Events.RaiseHoldingAmbushed(c.Id);
    }

    static string LootText(HoldingLoot l)
    {
        var parts = l.Items.Select(kv => $"{ItemNames.Of(kv.Key)} ×{kv.Value}").ToList();
        if (l.Credits > 0) parts.Add(Loc.T("holding.credits", l.Credits));
        if (l.Token.Length > 0) parts.Add(ItemNames.Of(l.Token) + " (depoda)");
        return string.Join(", ", parts);
    }

    /// <summary>Cantaya sigmayan ganimet Kooperatif deposuna (HoldingState.Depot, kayitta) gider; kaybolmaz.</summary>
    void DropLeftovers(Dictionary<string, int> left, HoldingCamp c)
    {
        if (left.Count == 0) return;
        foreach (var (id, n) in left) H.State.Depot[id] = H.State.Depot.GetValueOrDefault(id) + n;
        GD.Print($"HOLDING loot overflow->depot camp#{c.Id} {string.Join(",", left.Select(kv => kv.Key + "x" + kv.Value))}");
        D.Hud.Hint(Loc.T("holding.bag_full_loot"), 3f);
    }
}
