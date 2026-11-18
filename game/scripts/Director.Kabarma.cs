using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Director: tam Kabarma (kabarma3, GDD 14.1-14.6). Ilk gecenin akisi (aksam, alarm, sisme, dalgalar, kartlar, safak,
/// rapor) aynen kullanilir; burada plan kuyrugundan Kabaran dogurma, tapu sinirindan giris, Maya Kazani, elit etkileri,
/// suruler, bozgun ve takvim/Can tetigi var.
///   --kabarma=KS[:full|small|drill]  oyun kurulunca o Kabarma'yi hemen baslatir (sinama ve goruntu).
/// </summary>
public partial class Director
{
    KabarmaSpec _pendingSpec;
    int _bellKs;
    Label3D _kazanLabel;
    readonly List<WildSwarm> _nightSwarms = new();
    bool _routed;
    float _eliteTick;

    /// <summary>Calisan gece planli (tam/kucuk/tatbikat) mi.</summary>
    public bool FullNight => Night?.Plan != null;

    void InitKabarma()
    {
        State.Calendar.Dusk += OnKabarmaDusk;
        Events.WildActed += OnNightSwarmActed;
        if (Main.Args.ContainsKey("kabarmatest")) AddChild(new KabarmaTest(this) { Name = "KabarmaTest" }); // --test=kabarma
        if (Main.Args.TryGetValue("kabarma", out var a))
        {
            var parts = a.Split(':');
            int ks = int.TryParse(parts[0], out var k) ? k : 1;
            var kind = parts.Length > 1 ? parts[1] switch { "small" => KabarmaKind.Small, "drill" => KabarmaKind.Drill, _ => KabarmaKind.Full } : KabarmaKind.Full;
            CallDeferred(nameof(StartKabarmaDeferred), ks, (int)kind);
        }
    }

    void StartKabarmaDeferred(int ks, int kind) => StartKabarma((KabarmaKind)kind, ks);

    /// <summary>Can (GDD 2): bir sonraki gun batiminda secilen KS'de Kabarma baslar, takvim sifirlanir.</summary>
    public void RingBell(int ks)
    {
        _bellKs = Mathf.Clamp(ks, 1, State.UnlockedKs);
        Hud.Big($"ÇAN · KS {_bellKs}", Palette.MayaViolet, 2.5f);
        GD.Print($"KABARMA bell ks={_bellKs}");
    }

    /// <summary>Takvim (GDD 2): her N. gece ya da Can calindiysa gun batiminda tam Kabarma.</summary>
    void OnKabarmaDusk(int day)
    {
        // Otomatik koşular (--test, --bot) takvimden Kabarma başlatmaz: uzun günlü botları (hayvan, tarim) bozmasın.
        // Takvimi denemek için --kabarma_takvim.
        bool automated = (Main.Args.ContainsKey("test") || Main.Args.ContainsKey("bot")) && !Main.Args.ContainsKey("kabarma_takvim");
        if (automated && _bellKs == 0) return;
        int every = Data.Kabarma.Full.EveryDays;
        bool due = _bellKs > 0 || (every > 0 && day - State.Kabarma.LastDay >= every);
        if (!due || NightActive || Phase != Act.Free) return;
        int ks = _bellKs > 0 ? _bellKs : State.UnlockedKs;
        _bellKs = 0;
        StartKabarma(KabarmaKind.Full, ks);
    }

    /// <summary>Tam / kucuk / tatbikat Kabarma'sini aksamla baslatir (gucler: Maya Feneri, Tatbikat buradan cagirir).</summary>
    public bool StartKabarma(KabarmaKind kind, int ks, Vector3? at = null)
    {
        if (NightActive || (Phase != Act.Free && Phase != Act.Report)) { GD.Print($"KABARMA refused phase={Phase}"); return false; }
        if (Hud.MenuOpen) Hud.HideMenu();
        var c = at ?? OcakPos;
        float r = Data.Kabarma.Full.Small.Radius;
        int players = Net?.Players().Count() ?? 1;
        _pendingSpec = State.KabarmaSpecFor(kind, ks, Mathf.Max(1, players), c.X, c.Z,
            kind == KabarmaKind.Small ? p => new Vector2(p.X + 0.5f - c.X, p.Y + 0.5f - c.Z).Length() <= r : null);
        if (kind == KabarmaKind.Full) State.Kabarma.LastDay = State.Calendar.Day;
        _replay = true; // safakta rapor (GDD 14.1)
        _reportShown = false;
        _sinceDawn = 0;
        _shownOffer = null;
        _routed = false;
        BeginDusk();
        GD.Print($"KABARMA start kind={kind} ks={_pendingSpec.Ks} players={_pendingSpec.Players} families={string.Join(",", _pendingSpec.PlantedFamilies.Select(kv => kv.Key + ":" + kv.Value))}");
        return true;
    }

    /// <summary>BeginNight: bekleyen tanim varsa planli gece, yoksa ilk gece (senaryo).</summary>
    KabarmaNight StartNightFor()
    {
        var spec = _pendingSpec;
        _pendingSpec = null;
        if (spec == null) return State.StartNight();
        var n = State.StartKabarma(spec);
        n.DorukStarted += w => { Hud.Big(w.Slots.Any(s => s.Azman) ? "DORUK · AZMAN" : "DORUK", Palette.HoldingRed, 3f); GD.Print($"KABARMA doruk slots={w.Slots.Count} azman={w.Slots.Any(s => s.Azman)}"); };
        n.Ended += e => GD.Print($"KABARMA end reason={e} ks={n.Ks} kills={n.Kills} maya={n.Maya:0} kazan={n.Kazan?.Hp:0}/{n.Kazan?.MaxHp:0} highest={State.Kabarma.HighestCleared}");
        ShowKazan(n);
        GD.Print($"KABARMA plan ks={n.Ks} waves={n.Plan.Waves.Count} slots={n.Plan.TotalSlots} hpx={n.Plan.HpMult:0.00} maya={n.Plan.MayaMult:0.0}");
        return n;
    }

    void ShowKazan(KabarmaNight n)
    {
        if (n.Kazan == null) return;
        _kazanLabel ??= new Label3D
        {
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 40, OutlineSize = 12, PixelSize = 0.007f,
            Modulate = Palette.MayaTeal, OutlineModulate = Palette.NightDeep, NoDepthTest = true,
        };
        if (_kazanLabel.GetParent() == null) AddChild(_kazanLabel);
        _kazanLabel.GlobalPosition = OcakPos + Vector3.Up * 2.6f;
        _kazanLabel.Visible = true;
        UpdateKazanLabel();
    }

    void UpdateKazanLabel()
    {
        var k = Night?.Kazan;
        if (_kazanLabel == null || k == null) return;
        _kazanLabel.Text = $"Maya Kazanı {k.Hp:0}/{k.MaxHp:0}";
        _kazanLabel.Modulate = k.Hp < k.MaxHp * 0.3f ? Palette.HoldingRed : Palette.MayaTeal;
    }

    /// <summary>Kabaran Kazan'a vurdu (Zipzip.Kabaran.cs).</summary>
    public void OnKazanHit(float dmg, Vector3 from)
    {
        var n = Night;
        if (n == null) return;
        bool toppled = n.DamageKazan(dmg);
        UpdateKazanLabel();
        Fx.Burst(OcakPos + Vector3.Up * 1.2f, Palette.MayaViolet, 6, 2.5f, 0.08f, 0.4f, true, -2f);
        if (toppled)
        {
            Hud.Big("KAZAN DEVRİLDİ", Palette.HoldingRed, 3.5f);
            Sfx.Play("hit", OcakPos, 2f);
        }
    }

    /// <summary>Planli gecenin kare isi: kuyruktan dogurma, elit etkileri, bozgun, dalga sonu.</summary>
    void TickKabarma(KabarmaNight n, float dt)
    {
        if (!NetClient)
        {
            int active = Kabarans.Live.Count() + _nightSwarms.Count(s => IsInstanceValid(s) && !s.Dead);
            foreach (var (slot, wave) in n.TakeDue(active)) SpawnKabaran(n, slot, wave);
        }
        _nightSwarms.RemoveAll(s => !IsInstanceValid(s) || s.Dead);
        TickElites(n, dt);
        // Bozgun (GDD 14.4): butun oyuncular yerde. Tek oyuncuda: oyuncu yere dusunce bir kez.
        bool allDown = Player.Down && (Net == null || Net.Players().All(p => p.down));
        if (allDown && !_routed) { _routed = true; if (n.Rout()) Hud.Big("BOZGUN", Palette.HoldingRed, 3f); UpdateKazanLabel(); GD.Print($"KABARMA rout kazan={n.Kazan?.Hp:0}"); }
        if (!allDown) _routed = false;
        if (n.PendingSpawns == 0 && !Kabarans.Live.Any() && _nightSwarms.Count == 0) n.WaveCleared();
    }

    /// <summary>Tapu sinirindan (Ocak'in tapu yaricapi; kucukte fener yaricapi) giris.</summary>
    Vector3 EntryPoint(KabarmaNight n)
    {
        var spec = n.Plan.Spec;
        var c = new Vector3(spec.CenterX, 0, spec.CenterZ);
        float r = spec.Kind == KabarmaKind.Small ? spec.Radius : Mathf.Max(18f, State.Research.ClaimRadius);
        float a = (float)GD.RandRange(0, Mathf.Tau);
        float rr = r + (float)GD.RandRange(2.0, 6.0);
        return World.Ground(c + new Vector3(Mathf.Cos(a) * rr, 0, Mathf.Sin(a) * rr));
    }

    void SpawnKabaran(KabarmaNight n, WaveSlot slot, int wave)
    {
        var p = EntryPoint(n);
        if (slot.Azman && Azman?.SpawnSlot(slot, n.Ks, p) == true) return; // savas: combat/AzmanSystem.cs
        if (slot.Azman) slot = new WaveSlot { Species = slot.Species, Elite = "dev" }; // yedek: Azman tanimi yoksa Dev elit
        var def = Data.Creature(slot.Species);
        if (def.Swarm != null)
        {
            if (Wildlife == null) return;
            foreach (var node in Wildlife.Spawn(def, p, count: 1))
                if (node is WildSwarm s)
                {
                    s.State.Hp *= n.Plan.HpMult;
                    s.SnapSwell(true);
                    _nightSwarms.Add(s);
                }
            return;
        }
        var st = n.NewKabaran(slot, wave, p.X, p.Z);
        var z = new Zipzip(def, this, 1f, 1f, swollen: true) { Name = $"Kabaran_{def.Id}_{st.Id}" };
        z.BindKabaran(st, Data.Kabarma.Full);
        AddChild(z);
        z.GlobalPosition = p;
        Fx.Burst(p + Vector3.Up * 0.5f, string.IsNullOrEmpty(slot.Elite) ? Palette.MayaViolet : Palette.HoldingRed, 14, 4f, 0.12f, 0.6f, true, -2f);
    }

    /// <summary>Yabani suru gecede oldurulunce (Wildlife.Strike "Down"): Maya ve Oz.</summary>
    void OnNightSwarmActed(string species, string what, float x, float z)
    {
        if (what != "Down" || Night == null || Night.Phase == NightPhase.Dawn || !FullNight) return;
        var def = Data.Creature(species);
        if (def.Swarm == null) return;
        var oz = Night.OnKill(def, x, z);
        DropOz(oz, new Vector3(x, World.HeightAt(x, z) + 0.6f, z));
        Night.CreditKill(0);
        RefreshOffer();
    }

    /// <summary>Elit etkileri (GDD 14.3): Sifaci yakindakileri iyilestirir, Miknatis oyuncuyu ceker.</summary>
    void TickElites(KabarmaNight n, float dt)
    {
        _eliteTick -= dt;
        if (_eliteTick > 0) return;
        _eliteTick = 0.5f;
        var elites = Data.Kabarma.Full.Elites;
        foreach (var k in Kabarans.Live.ToList())
        {
            var e = elites.FirstOrDefault(x => x.Id == k.State.Elite);
            if (e == null) continue;
            if (e.HealRadius > 0)
                foreach (var o in Kabarans.Near(k.Center, e.HealRadius))
                    if (o is Zipzip oz && o != k) oz.Hp = Mathf.Min(oz.MaxHp, oz.Hp + oz.MaxHp * e.HealPerSecond * 0.5f);
            if (e.PullRadius > 0 && !Player.Down)
            {
                var d = k.Center - Player.GlobalPosition; d.Y = 0;
                if (d.Length() < e.PullRadius && d.Length() > 1.5f) Player.GlobalPosition += d.Normalized() * 0.35f;
            }
        }
    }

    /// <summary>Plandan dogan Kabaran oldu (OnZipzipKilled'dan): oldurene yaz, Bolunen ve Patlayan elitleri.</summary>
    void OnKabaranKilled(Zipzip z)
    {
        var n = Night;
        if (n?.Plan == null) return;
        n.CreditKill(z.State.LastHitBy);
        var e = Data.Kabarma.Full.Elites.FirstOrDefault(x => x.Id == z.State.Elite);
        if (e == null) return;
        if (e.DeathBlast > 0 && Player.GlobalPosition.DistanceTo(z.GlobalPosition) < e.DeathBlast)
            Player.Hurt(Mathf.Round(z.State.Damage), z.GlobalPosition);
        if (e.DeathBlast > 0) Fx.Burst(z.Center, Palette.Accent, 30, 7f, 0.14f, 0.7f, true, 1f);
        for (int i = 0; i < e.SplitInto; i++)
        {
            var st = n.NewKabaran(new WaveSlot { Species = z.Def.Id }, z.State.Wave, z.GlobalPosition.X, z.GlobalPosition.Z);
            st.MaxHp = st.Hp = z.MaxHp * 0.35f; st.Scale = 0.7f;
            var c = new Zipzip(z.Def, this, 1f, 1f, swollen: true) { Name = $"Kabaran_{z.Def.Id}_{st.Id}" };
            c.BindKabaran(st, Data.Kabarma.Full);
            CallDeferred(Node.MethodName.AddChild, c);
            var off = new Vector3(i == 0 ? 0.8f : -0.8f, 0, 0.4f);
            c.Position = World.Ground(z.GlobalPosition + off);
        }
    }

    /// <summary>Safak: gece suruleri ve Kazan etiketi.</summary>
    void EndKabarmaView()
    {
        foreach (var s in _nightSwarms) if (IsInstanceValid(s)) s.QueueFree();
        _nightSwarms.Clear();
        Kabarans.Clear();
        if (_kazanLabel != null) _kazanLabel.Visible = false;
    }
}
