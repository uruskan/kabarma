namespace Kabarma.Sim;

public enum RivalryVerdict
{
    Allowed,
    /// <summary>Kural 2: gunduz tapu dokunulmaz.</summary>
    DayProtected,
    /// <summary>Kural 7: cevrimdisi koruma.</summary>
    Offline,
    /// <summary>Kural 6: Kazan'i devrilen takimin tapusu mühürlü.</summary>
    Sealed,
    /// <summary>Kural 6: Ocak yikilmaz.</summary>
    Indestructible,
    /// <summary>Kural 3: acik arazide oyuncu savasi kapali.</summary>
    PvpClosed,
    /// <summary>Kendi takimina vurulmaz.</summary>
    Friendly,
    /// <summary>Cekisme dunyasi degil.</summary>
    NotRivalry,
}

/// <summary>
/// Cekisme dunyasinin on kurali (GDD 22.2), Sim'de ve testli. Yetki host'ta: bu fonksiyonlar karar verir, host uygular.
/// Gece = Calendar.IsNight (Kabarma herkese ayni anda gelir, kural 4): tapu korumasi gece askiya alinir.
/// </summary>
public static class RivalryRules
{
    // ------------------------------------------------------------------ kural 1, 10: kurulum

    /// <summary>Takim sayisi, oyuncu sayisi ve Ocak aralari uygun mu. Donen: null ya da neden.</summary>
    public static string? ValidateSetup(RivalryFile d, RivalryState s)
    {
        int maxTeams = s.Dedicated ? d.DedicatedMaxTeams : d.ListenMaxTeams;
        int maxPlayers = s.Dedicated ? d.DedicatedMaxPlayers : d.ListenMaxPlayers;
        if (s.Teams.Count < d.TeamsMin) return $"en az {d.TeamsMin} takim";
        if (s.Teams.Count > Math.Min(d.TeamsMax, maxTeams)) return $"en cok {Math.Min(d.TeamsMax, maxTeams)} takim";
        int players = 0;
        foreach (var t in s.Teams)
        {
            if (t.Players.Count < 1) return $"{t.Id}: oyuncusuz takim";
            if (t.Players.Count > d.PlayersPerTeamMax) return $"{t.Id}: en cok {d.PlayersPerTeamMax} oyuncu";
            players += t.Players.Count;
        }
        if (players > maxPlayers) return $"en cok {maxPlayers} oyuncu";
        var all = s.Players().ToList();
        if (all.Count != all.Distinct().Count()) return "bir oyuncu iki takimda";
        for (int i = 0; i < s.Teams.Count; i++)
            for (int j = i + 1; j < s.Teams.Count; j++)
                if (OcakDistance(s.Teams[i], s.Teams[j]) < d.OcakMinDistance) return $"{s.Teams[i].Id}-{s.Teams[j].Id}: Ocaklar {d.OcakMinDistance:0} m'den yakin";
        return null;
    }

    static IEnumerable<string> Players(this RivalryState s) => s.Teams.SelectMany(t => t.Players);

    public static float OcakDistance(RivalryTeam a, RivalryTeam b) => MathF.Sqrt((a.OcakX - b.OcakX) * (a.OcakX - b.OcakX) + (a.OcakZ - b.OcakZ) * (a.OcakZ - b.OcakZ));

    /// <summary>Yeni takimi ekler (Ocak aralari ve sayilari sonra ValidateSetup ile sinanir).</summary>
    public static RivalryTeam AddTeam(RivalryFile d, RivalryState s, string id, string name, float ocakX, float ocakZ, float plotRadius, params string[] players)
    {
        var t = new RivalryTeam { Id = id, Name = name, OcakX = ocakX, OcakZ = ocakZ, PlotRadius = plotRadius > 0 ? plotRadius : d.PlotRadiusFallback, Players = players.ToList() };
        s.Teams.Add(t);
        return t;
    }

    // ------------------------------------------------------------------ tapu sorgulari

    /// <summary>Nokta hangi takimin tapusunda (yoksa null).</summary>
    public static RivalryTeam? PlotAt(RivalryState s, float x, float z)
    {
        foreach (var t in s.Teams)
            if ((x - t.OcakX) * (x - t.OcakX) + (z - t.OcakZ) * (z - t.OcakZ) <= t.PlotRadius * t.PlotRadius) return t;
        return null;
    }

    // ------------------------------------------------------------------ kural 2, 3, 4, 6, 7: hasar karari

    /// <summary>
    /// Bir hedefe (oyuncu, hayvan, yapi) vurulabilir mi. attackerTeam: saldiranin takimi (bos: yabani/Kabaran). night: Calendar.IsNight.
    /// Tapu icindeki her sey gunduz dokunulmaz; gece (Kabarma) tapu korumasi askida, ama cevrimdisi ve muhurlu tapu yine korunur.
    /// Yabani yaratik ve Kabaran hedefi (attackerTeam bos) cevrimdisi/muhur disinda gece vurabilir.
    /// </summary>
    public static RivalryVerdict CanHit(RivalryFile d, RivalryState s, bool night, float x, float z, string attackerTeam, string? shape = null, bool targetIsPlayer = false)
    {
        if (!s.Enabled) return RivalryVerdict.NotRivalry;
        var plot = PlotAt(s, x, z);
        if (plot == null)
        {
            // Acik arazi: yalniz oyuncu-oyuncu ve ayar acikken.
            if (targetIsPlayer && attackerTeam.Length > 0) return s.OpenPvp ? RivalryVerdict.Allowed : RivalryVerdict.PvpClosed;
            return RivalryVerdict.Allowed;
        }
        if (attackerTeam == plot.Id) return RivalryVerdict.Friendly;
        if (shape != null && d.IndestructibleShapes.Contains(shape)) return RivalryVerdict.Indestructible;
        if (plot.Offline) return RivalryVerdict.Offline;
        if (plot.KazanToppled) return RivalryVerdict.Sealed;
        if (!night) return RivalryVerdict.DayProtected;
        return RivalryVerdict.Allowed;
    }

    /// <summary>Kural 7: cevrimdisi takimin ekinleri sismez. Cevrimici takimlarda ve tapu disinda sisme olagan.</summary>
    public static bool SwellAllowed(RivalryState s, float x, float z)
    {
        if (!s.Enabled) return true;
        var plot = PlotAt(s, x, z);
        return plot == null || !plot.Offline;
    }

    /// <summary>Kural 7: cevrimici oyuncu kumesi verilir; kimsesi kalmayan takim muhurlenir, biri gelince acilir. Degisen takim sayisi.</summary>
    public static int UpdatePresence(RivalryState s, IReadOnlySet<string> online)
    {
        int changed = 0;
        foreach (var t in s.Teams)
        {
            bool off = !t.Players.Any(online.Contains);
            if (off != t.Offline) { t.Offline = off; changed++; }
        }
        return changed;
    }

    // ------------------------------------------------------------------ kural 3: oldurme yagmasi

    /// <summary>Acik arazide olen oyuncunun cantasindan rastgele PvpLootStacks yigin katile gecer. Donen: tasinanlar (id, adet).</summary>
    public static List<(string id, int count)> PvpLoot(RivalryFile d, Inventory victim, Inventory killer, Rng rng)
    {
        var moved = new List<(string, int)>();
        for (int i = 0; i < d.PvpLootStacks && victim.Stacks.Count > 0; i++)
        {
            var st = victim.Stacks[rng.Range(0, victim.Stacks.Count)];
            if (!victim.Remove(st.id, st.count, st.stars)) continue;
            int put = killer.Add(st.id, st.count, st.stars);
            if (put < st.count) victim.Add(st.id, st.count - put, st.stars); // sigmayan geri
            if (put > 0) moved.Add((st.id, put));
        }
        return moved;
    }

    // ------------------------------------------------------------------ kural 5: yagmalanabilirler

    /// <summary>"swollen_crop" ve "open_silo" yagmalanir; "warehouse" ve "animal" yagmalanmaz. Yagma yalniz gece ve yabanci takima.</summary>
    public static bool Lootable(RivalryFile d, RivalryState s, bool night, string kind, float x, float z, string looterTeam)
    {
        if (!s.Enabled || !night || !d.LootableKinds.Contains(kind)) return false;
        var plot = PlotAt(s, x, z);
        if (plot != null && plot.Id == looterTeam) return false;
        return plot == null || (!plot.Offline && !plot.KazanToppled) || kind == "open_silo";
    }

    /// <summary>Gece baslarken: her takimin ihracata hazir mallari Acik Silo'ya girer (ambarda kalan ve hayvan girmez).</summary>
    public static void MarkExportReady(RivalryTeam t, string item, int count)
    {
        if (count <= 0) return;
        t.ExportReady[item] = t.ExportReady.GetValueOrDefault(item) + count;
    }

    /// <summary>Gece sonunda (safak): Maya'nin siloPayi + ihracata hazir mallar Acik Silo'ya, kalan sezon puanina.</summary>
    public static void Dawn(RivalryFile d, RivalryState s)
    {
        foreach (var t in s.Teams)
        {
            int pool = (int)MathF.Floor(Math.Max(0f, t.NightMaya));
            int silo = (int)MathF.Floor(pool * d.SiloMayaShare);
            s.SiloMaya += silo;
            t.CollectedMaya += pool - silo;
            foreach (var (item, n) in t.ExportReady) s.Silo[item] = s.Silo.GetValueOrDefault(item) + n;
            t.Exports += t.ExportReady.Values.Sum() * d.ExportMayaValue;
            t.ExportReady.Clear();
            t.NightMaya = 0;
            t.KazanToppled = false; // muhur safakta kalkar
        }
        // Enkaz plani safakta da kalir (kural 6): gunduz tek tusla kurulur.
    }

    /// <summary>Acik Silo'dan yagma: yabanci takim sandigi kendi cantasina bosaltir. Donen: (esya adedi, Maya).</summary>
    public static (int items, int maya) LootSilo(RivalryState s, string looterTeam, Inventory bag)
    {
        int items = 0;
        foreach (var k in s.Silo.Keys.ToList())
        {
            int put = bag.Add(k, s.Silo[k]);
            items += put;
            if (put >= s.Silo[k]) s.Silo.Remove(k); else s.Silo[k] -= put;
        }
        int maya = s.SiloMaya;
        if (maya > 0)
        {
            int put = bag.Add(MayaVault.Item, maya);
            s.SiloMaya -= put;
            maya = put;
            if (s.Team(looterTeam) is { } t) t.CollectedMaya += put;
        }
        return (items, maya);
    }

    // ------------------------------------------------------------------ kural 6: yapi hasari, enkaz plani

    /// <summary>
    /// Gecede yapi parcasina hasar. Karar CanHit'ten; parca yikilirsa enkaz planina cevrilir (yikilan zincir de, collapse).
    /// Donen: karar; yikilanlar destroyed'a eklenir.
    /// </summary>
    public static RivalryVerdict DamagePiece(RivalryFile d, RivalryState s, StructureGraph g, int pieceId, float dmg, bool night, string attackerTeam, List<StructurePiece>? destroyed = null)
    {
        var p = g.Get(pieceId);
        if (p == null) return RivalryVerdict.NotRivalry;
        var v = CanHit(d, s, night, p.Pos[0], p.Pos[2], attackerTeam, p.Shape);
        if (v != RivalryVerdict.Allowed) return v;
        p.Hp -= dmg;
        if (p.Hp > 0) return v;
        var owner = PlotAt(s, p.Pos[0], p.Pos[2]);
        var fell = g.Remove(pieceId);
        fell.Insert(0, p);
        foreach (var q in fell)
        {
            AddRuin(s, q, owner?.Id ?? "");
            destroyed?.Add(q);
        }
        return v;
    }

    /// <summary>Yikilan parcanin enkaz planini yazar (Godot katmani da grafi kendi yonettiginde cagirir).</summary>
    public static RuinPlan AddRuin(RivalryState s, StructurePiece q, string team)
    {
        var r = new RuinPlan
        {
            Id = s.NextRuin++, Team = team, Shape = q.Shape, Material = q.Material, Units = q.Units,
            Pos = (float[])q.Pos.Clone(), Yaw = q.Yaw, CenterY = q.CenterY, Grounded = q.Grounded, Horizontal = q.Horizontal,
        };
        s.Ruins.Add(r);
        return r;
    }

    /// <summary>Enkazin bedeli: malzeme bedelinin RebuildCostFraction'i, asagi degil yukari yuvarlanmaz (esya basina asagi, en az 1).</summary>
    public static Dictionary<string, int> RebuildCost(RivalryFile d, BuildFile b, RuinPlan r)
    {
        var res = new Dictionary<string, int>();
        foreach (var kv in StructureCosts.Cost(b, r.Material, r.Units))
        {
            int n = (int)MathF.Floor(kv.Value * d.RebuildCostFraction + 1e-4f);
            res[kv.Key] = Math.Max(1, n);
        }
        return res;
    }

    /// <summary>
    /// Enkaz planini tek tusla kurar (kural 6): bedelin %50'si cantadan duser, parca tam canla grafa girer. Gece kurulmaz
    /// (yalniz tapu sahibi gunduz). Donen: null (basardi) ya da neden.
    /// </summary>
    public static string? Rebuild(RivalryFile d, RivalryState s, StructureGraph g, BuildFile b, int ruinId, string team, bool night, Inventory bag)
    {
        var r = s.Ruins.FirstOrDefault(x => x.Id == ruinId);
        if (r == null) return "enkaz yok";
        if (r.Team != team) return "baska takimin enkazi";
        if (night) return "gece kurulmaz";
        var cost = RebuildCost(d, b, r);
        foreach (var kv in cost) if (bag.Count(kv.Key) < kv.Value) return $"eksik: {kv.Key}";
        var p = new StructurePiece
        {
            Shape = r.Shape, Material = r.Material, Units = r.Units, Pos = (float[])r.Pos.Clone(), Yaw = r.Yaw,
            CenterY = r.CenterY, Grounded = r.Grounded, Horizontal = r.Horizontal, Hp = g.Mat(new StructurePiece { Material = r.Material }).Hp,
        };
        // Destegi yeniden kurmak Godot katmaninin isi (yuva kurali); Sim yalniz parcayi ve odemeyi bilir.
        foreach (var kv in cost) bag.Remove(kv.Key, kv.Value);
        g.Add(p);
        s.Ruins.Remove(r);
        return null;
    }

    /// <summary>Kazan devrildi (kural 6): takimin gecesi biter, tapusu safaga dek muhurlu.</summary>
    public static void ToppleKazan(RivalryState s, string teamId)
    {
        if (s.Team(teamId) is { } t) t.KazanToppled = true;
    }

    // ------------------------------------------------------------------ kural 8: sezon

    /// <summary>Sezon secimi: seasonOptions icinden olmali.</summary>
    public static bool SetSeason(RivalryFile d, RivalryState s, int days, int today)
    {
        if (!d.SeasonOptions.Contains(days)) return false;
        s.Season = new RivalrySeason { Days = days, StartDay = today };
        return true;
    }

    /// <summary>Sezon gunu bitti mi: dondurur ve kazanani yazar. Donen: bu cagrida dondu.</summary>
    public static bool TickSeason(RivalryState s, int today)
    {
        var se = s.Season;
        if (se.Frozen || se.Days <= 0 || today < se.StartDay + se.Days) return false;
        se.Frozen = true;
        se.Winner = Leaderboard(s).FirstOrDefault()?.Id ?? "";
        return true;
    }

    /// <summary>Puana gore sirali takimlar (esitlikte kimlik).</summary>
    public static List<RivalryTeam> Leaderboard(RivalryState s) => s.Teams.OrderByDescending(t => t.Score).ThenBy(t => t.Id, StringComparer.Ordinal).ToList();

    /// <summary>Puan yazimi: sezon donduysa kimse puan alamaz.</summary>
    public static void AddMaya(RivalryState s, string teamId, float nightMaya)
    {
        if (s.Season.Frozen || s.Team(teamId) is not { } t) return;
        t.NightMaya += Math.Max(0f, nightMaya);
    }

    // ------------------------------------------------------------------ kural 9

    /// <summary>Holding kamplari yalniz ayar acikken kurulur (kural 9); Cekisme disinda Holding olagan.</summary>
    public static bool HoldingAllowed(RivalryState s) => !s.Enabled || s.HoldingCamps;

    /// <summary>Yeni Cekisme dunyasi: ayar varsayilanlari (kural 3, 8, 9).</summary>
    public static RivalryState New(RivalryFile d, bool dedicated, int today)
    {
        var s = new RivalryState { Enabled = true, OpenPvp = d.OpenPvpDefault, HoldingCamps = d.HoldingCampsDefault, Dedicated = dedicated };
        s.Season = new RivalrySeason { Days = d.SeasonDefault, StartDay = today };
        return s;
    }
}
