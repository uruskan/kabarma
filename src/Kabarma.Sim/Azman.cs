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
    public float Time { get; set; }
    public bool Defeated { get; set; }
}

/// <summary>Azmanlarin kalici kaydi: suren dovusler ve yenilgi sayilari.</summary>
public sealed class AzmanProgress
{
    public List<AzmanFight> Active { get; set; } = new();
    public Dictionary<string, int> Defeats { get; set; } = new();

    public AzmanFight? Get(ulong id) => Active.FirstOrDefault(f => f.Id == id);
}

public enum AzmanCue { Telegraph, Attack, DropRiders, Defeated }

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
    public static CombatResult Damage(AzmanFight fight, CombatFile combat, AzmanDef def, in CombatHit hit, List<AzmanCommand>? cmds = null, List<CombatArea>? areas = null)
    {
        if (fight.Defeated) return default;
        var r = Combat.Apply(combat, fight.Body, hit, areas);
        After(fight, def, cmds);
        return r;
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
        if (Combat.Tick(combat, fight.Body, dt, areas) > 0) After(fight, def, cmds);
        if (fight.Defeated || fight.Body.ShieldT > 0 || !Status.CanAct(fight.Body)) return cmds;
        if (fight.ActT > 0) { fight.ActT = Math.Max(0, fight.ActT - dt); if (fight.ActT <= 0) fight.Acting = ""; return cmds; }

        if (fight.TelegraphT > 0)
        {
            fight.TelegraphT -= dt;
            if (fight.TelegraphT <= 0)
            {
                fight.TelegraphT = 0;
                fight.Acting = fight.Pending;
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
