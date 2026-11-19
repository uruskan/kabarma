namespace Kabarma.Sim;

// Azmanlar (ICERIK §4.2): duz, serilestirilebilir durum (TEKNIK §9) ve kurallar. Can ve durumlar ortak savas
// govdesinde (CombatBody, Combat.cs). Godot katmani (game/scripts/combat/AzmanSystem.cs) konumu yazar, komutlari oynatir.

/// <summary>Bir Azman'in dovus durumu.</summary>
public sealed class AzmanFight
{
    /// <summary>Kimlik (GameState.Ids): ag ve kayit bununla eslesir.</summary>
    public ulong Id { get; set; }
    public string AzmanId { get; set; } = "";
    public int Ks { get; set; }
    public int TeamSize { get; set; } = 1;
    public CombatBody Body { get; set; } = new() { Team = CombatTeam.Wild };
    /// <summary>Konum ve yon (Godot katmani her kare yazar; kayit ve istemci kuklasi icin).</summary>
    public float X { get; set; }
    public float Z { get; set; }
    public float Yaw { get; set; }
    public float AttackT { get; set; }
    public int AttackIndex { get; set; }
    public float TelegraphT { get; set; }
    public string Pending { get; set; } = "";
    /// <summary>Suren saldiri (hucum, sokme) ve kalan suresi; bos = yuruyor.</summary>
    public string Acting { get; set; } = "";
    public float ActT { get; set; }
    /// <summary>Sirtta kalan kucuk Homur ve gecilen esik sayisi.</summary>
    public int Riders { get; set; }
    public int RiderStep { get; set; }
    /// <summary>Bolunme (Azman Hamur): nesil (0 ana), ana dovus, gecilen esik.</summary>
    public int Gen { get; set; }
    public ulong Parent { get; set; }
    public int SplitStep { get; set; }
    /// <summary>Surubasi: canli cagrilmis yaratik sayisi (Godot bildirir). Kizgin: yanan lekeler.</summary>
    public int Summoned { get; set; }
    public List<AzmanFire> Fires { get; set; } = new();
    public float Time { get; set; }
    public bool Defeated { get; set; }
}

/// <summary>Yangin lekesi (Azman Kizgin): merkez, kalan sure (sn).</summary>
public sealed class AzmanFire
{
    public float X { get; set; }
    public float Z { get; set; }
    public float T { get; set; }
}

/// <summary>Azmanlarin kalici kaydi: suren dovusler ve yenilgi sayilari.</summary>
public sealed class AzmanProgress
{
    public List<AzmanFight> Active { get; set; } = new();
    public Dictionary<string, int> Defeats { get; set; } = new();

    public AzmanFight? Get(ulong id) => Active.FirstOrDefault(f => f.Id == id);
}

public enum AzmanCue { Telegraph, Attack, DropRiders, Defeated, Split }

public readonly record struct AzmanCommand(AzmanCue Cue, string Attack = "", int Count = 0);

public static class AzmanRules
{
    /// <summary>ICERIK §4.2: her Every. KS'nin Doruk asamasinda Azman gelir.</summary>
    public static bool ShouldAppear(AzmanFile f, int ks) => f.Every > 0 && ks >= f.Every && ks % f.Every == 0;

    /// <summary>Can carpani: KS ve ekip boyu.</summary>
    public static float HpMult(AzmanFile f, int ks, int team) =>
        (1f + f.HpPerKs * Math.Max(0, ks - f.Every)) * (1f + f.TeamHpPerExtra * (Math.Max(1, team) - 1));

    /// <summary>Azman doger: inis kalkani, ilk saldiri araligi, sirttakiler. Dovus Active'e eklenir.</summary>
    public static AzmanFight Spawn(AzmanProgress p, AzmanFile f, AzmanDef def, int ks, int team, ulong id, float x = 0, float z = 0)
    {
        float hp = def.Hp * HpMult(f, ks, team);
        var fight = new AzmanFight
        {
            Id = id, AzmanId = def.Id, Ks = ks, TeamSize = Math.Max(1, team), X = x, Z = z,
            Body = CombatBody.Of(hp, CombatTeam.Wild, def.Id, def.Armor, def.Mass),
            AttackT = def.FirstAttack + f.ArriveSeconds,
            Riders = def.Riders?.Count ?? 0,
        };
        fight.Body.ShieldT = f.ArriveSeconds;
        p.Active.Add(fight);
        return fight;
    }

    /// <summary>Isabet (Combat.Apply): sirttakiler esikte duser, can biterse yenilir. Sonuc doner.</summary>
    public static CombatResult Damage(AzmanFight fight, CombatFile combat, AzmanDef def, in CombatHit hit, List<AzmanCommand>? cmds = null, List<CombatArea>? areas = null, float fromX = float.NaN, float fromZ = float.NaN)
    {
        if (fight.Defeated) return default;
        var h = hit;
        // Azman Kiskac: zirh yalniz arkadan delinir (on ve yan hasar kisilir). Kaynak konumu bilinmiyorsa (alan etkisi) carpan yok.
        if (def.Rear != null && !float.IsNaN(fromX) && !FromRear(fight, def, fromX, fromZ)) h = h with { Amount = h.Amount * def.Rear.FrontMult };
        var r = Combat.Apply(combat, fight.Body, h, areas);
        After(fight, def, cmds);
        return r;
    }

    /// <summary>Kaynak Azman'in arka yayinda mi (Yaw: Z ekseninden donus; on = (sin Yaw, cos Yaw)).</summary>
    public static bool FromRear(AzmanFight f, AzmanDef def, float fromX, float fromZ)
    {
        float dx = fromX - f.X, dz = fromZ - f.Z, len = MathF.Sqrt(dx * dx + dz * dz);
        if (len < 0.001f) return false;
        float cos = (dx * MathF.Sin(f.Yaw) + dz * MathF.Cos(f.Yaw)) / len; // kaynagin yone gore acisi
        float arc = (def.Rear?.RearArcDeg ?? 180f) * MathF.PI / 180f;
        return cos <= -MathF.Cos(arc * 0.5f); // arkadaysa on yonle arasi acisi 180 - arc/2'den buyuk
    }

    static void After(AzmanFight fight, AzmanDef def, List<AzmanCommand>? cmds)
    {
        if (fight.Defeated) return;
        if (fight.Body.Dead)
        {
            fight.Defeated = true;
            if (fight.Riders > 0) { cmds?.Add(new AzmanCommand(AzmanCue.DropRiders, Count: fight.Riders)); fight.Riders = 0; }
            cmds?.Add(new AzmanCommand(AzmanCue.Defeated));
            return;
        }
        var sp = def.Split;
        if (sp != null)
            while (fight.SplitStep < sp.At.Count && fight.Body.Fraction <= sp.At[fight.SplitStep])
            {
                fight.SplitStep++;
                cmds?.Add(new AzmanCommand(AzmanCue.Split, Count: sp.Pieces));
            }
        var rd = def.Riders;
        if (rd == null || rd.At.Count == 0) return;
        while (fight.RiderStep < rd.At.Count && fight.Body.Fraction <= rd.At[fight.RiderStep])
        {
            fight.RiderStep++;
            // Son esik kalanlarin hepsini indirir; ondan oncekiler esit pay.
            int n = fight.RiderStep >= rd.At.Count ? fight.Riders : Math.Min(fight.Riders, (int)Math.Ceiling(rd.Count / (float)rd.At.Count));
            if (n <= 0) continue;
            fight.Riders -= n;
            cmds?.Add(new AzmanCommand(AzmanCue.DropRiders, Count: n));
        }
    }

    /// <summary>
    /// Zaman: durumlar (yanik, zehir; Combat.Tick), inis kalkani, saldiri sirasi ve sezdirme. Kilit ve sersemken
    /// saldiri yok. Godot katmani suren saldiriyi (Acting) bitirince EndAct cagirir.
    /// </summary>
    public static List<AzmanCommand> Tick(AzmanFight fight, CombatFile combat, AzmanDef def, float dt, List<CombatArea>? areas = null)
    {
        var cmds = new List<AzmanCommand>();
        if (fight.Defeated) return cmds;
        fight.Time += dt;
        if (fight.Fires.Count > 0) { foreach (var fr in fight.Fires) fr.T -= dt; fight.Fires.RemoveAll(fr => fr.T <= 0); }
        if (Combat.Tick(combat, fight.Body, dt, areas) > 0) After(fight, def, cmds);
        if (fight.Defeated || fight.Body.ShieldT > 0 || !Status.CanAct(fight.Body)) return cmds;
        if (fight.ActT > 0) { fight.ActT = Math.Max(0, fight.ActT - dt); if (fight.ActT <= 0) fight.Acting = ""; return cmds; }

        if (fight.TelegraphT > 0)
        {
            fight.TelegraphT -= dt;
            if (fight.TelegraphT <= 0)
            {
                fight.TelegraphT = 0;
                fight.Acting = ActSeconds(def, fight.Pending) > 0 ? fight.Pending : ""; // anlik saldirilar (rip, ignite, howl) Acting kurmaz
                fight.ActT = ActSeconds(def, fight.Pending);
                cmds.Add(new AzmanCommand(AzmanCue.Attack, fight.Pending));
                fight.Pending = "";
            }
            return cmds;
        }
        if (def.Attacks.Count == 0) return cmds;
        fight.AttackT -= dt;
        if (fight.AttackT <= 0)
        {
            fight.Pending = def.Attacks[fight.AttackIndex % def.Attacks.Count];
            fight.AttackIndex++;
            fight.AttackT = def.AttackEvery;
            fight.TelegraphT = Math.Max(0.01f, def.Telegraph);
            cmds.Add(new AzmanCommand(AzmanCue.Telegraph, fight.Pending));
        }
        return cmds;
    }

    /// <summary>Saldirinin suresi: hucum Charge.Seconds, sokme Uproot.Pull (firlatma sokmenin sonunda).</summary>
    public static float ActSeconds(AzmanDef def, string attack) => attack switch
    {
        "charge" => def.Charge?.Seconds ?? 0,
        "uproot" => def.Uproot?.Pull ?? 0,
        _ => 0,
    };

    /// <summary>Saldiri erken bitti (hucum yapiya ya da arena disina vardi).</summary>
    public static void EndAct(AzmanFight fight) { fight.Acting = ""; fight.ActT = 0; }

    /// <summary>Hucum yapiya toslayinca Azman sersemler (sur yikar ama durur).</summary>
    public static void WallStun(AzmanFight fight, AzmanDef def)
    {
        EndAct(fight);
        if (def.Charge != null && def.Charge.WallStun > 0) Status.AddStun(fight.Body, def.Charge.WallStun);
    }

    // ---- M4 (ata4): Kizgin yangini, Surubasi sürüsü, Hamur bolunmesi ----

    /// <summary>Yangin lekeleri: verilen noktalara (Godot: oyuncu cevresi ve tarla yataklari) Patches adede kadar leke.</summary>
    public static int Ignite(AzmanFight f, AzmanDef def, IEnumerable<(float x, float z)> at)
    {
        var d = def.Ignite; if (d == null) return 0;
        int n = 0;
        foreach (var (x, z) in at)
        {
            if (n >= d.Patches) break;
            f.Fires.Add(new AzmanFire { X = x, Z = z, T = d.Seconds });
            n++;
        }
        return n;
    }

    /// <summary>Sulama ya da yagmurlama: (x, z) cevresinde R icindeki lekelerin suresi seconds kadar kisalir; sonen leke sayisi doner.</summary>
    public static int Douse(AzmanFight f, float x, float z, float r, float seconds)
    {
        int out0 = 0;
        foreach (var fr in f.Fires)
        {
            float dx = fr.X - x, dz = fr.Z - z;
            if (dx * dx + dz * dz > r * r) continue;
            fr.T -= seconds;
            if (fr.T <= 0) out0++;
        }
        f.Fires.RemoveAll(fr => fr.T <= 0);
        return out0;
    }

    /// <summary>Konumdaki yangin hasari (saniyelik Dps × dt); lekenin yaricapi icindeyse.</summary>
    public static float FireDamage(AzmanFight f, AzmanDef def, float x, float z, float dt)
    {
        var d = def.Ignite; if (d == null) return 0;
        foreach (var fr in f.Fires)
        {
            float dx = fr.X - x, dz = fr.Z - z;
            if (dx * dx + dz * dz <= d.Radius * d.Radius) return d.Dps * dt;
        }
        return 0;
    }

    /// <summary>Surubasi uluyor: kac yaratik cagirabilir (Max - canli) ve sayac artar.</summary>
    public static int Howl(AzmanFight f, AzmanDef def)
    {
        var s = def.Summon; if (s == null) return 0;
        int n = Math.Clamp(s.Max - f.Summoned, 0, s.Count);
        f.Summoned += n;
        return n;
    }

    public static void SummonDied(AzmanFight f) => f.Summoned = Math.Max(0, f.Summoned - 1);

    /// <summary>Bolunme parcasi: ana dovusun kucuk parcasi (HpFrac × ana can); yenilirse ayri dovus gibi, son parca odulu verir.</summary>
    public static AzmanFight SpawnPiece(AzmanProgress p, AzmanFile file, AzmanDef def, AzmanFight parent, ulong id, float x, float z)
    {
        var sp = def.Split ?? new AzmanSplitDef();
        var piece = new AzmanFight
        {
            Id = id, AzmanId = def.Id, Ks = parent.Ks, TeamSize = parent.TeamSize, X = x, Z = z,
            Gen = parent.Gen + 1, Parent = parent.Id, SplitStep = sp.At.Count, // parca bir daha bolunmez (yalniz ana)
            Body = CombatBody.Of(parent.Body.MaxHp * sp.HpFrac, CombatTeam.Wild, def.Id, def.Armor, def.Mass),
            AttackT = file.ArriveSeconds,
        };
        piece.Body.ShieldT = 0.6f;
        p.Active.Add(piece);
        return piece;
    }

    /// <summary>Bu parca bu turun son canli parcasi mi (odul ve Defeated olayi yalniz onda).</summary>
    public static bool IsLastPiece(AzmanProgress p, AzmanFight f) => !p.Active.Any(x => x != f && x.AzmanId == f.AzmanId && !x.Defeated);

    /// <summary>Son olmayan parca yenildi: odul yok, yalniz dovus kapanir.</summary>
    public static void DropPiece(AzmanProgress p, AzmanFight f) => p.Active.Remove(f);

    /// <summary>Yenilgi: odul cantaya, yenilgi sayisi; dovus kapanir. Cantaya sigmayan adet doner (Godot yere birakir).</summary>
    public static int Reward(AzmanProgress p, AzmanFight fight, AzmanDef def, Inventory bag)
    {
        p.Defeats[def.Id] = p.Defeats.GetValueOrDefault(def.Id) + 1;
        p.Active.Remove(fight);
        int left = 0;
        foreach (var it in def.Reward.Items) left += it.Count - bag.Add(it.Item, it.Count);
        return left;
    }

    /// <summary>Safak (Kabarma bitti) ya da herkes yenildi: Azman cekilir, odul yok.</summary>
    public static void Dismiss(AzmanProgress p, AzmanFight fight) => p.Active.Remove(fight);
}
