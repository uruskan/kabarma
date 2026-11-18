namespace Kabarma.Sim;

/// <summary>
/// Guc agindaki bir parca (GDD 11.1): uretec, aku, direk, salter, tuketici (lamba ya da guc isteyen istasyon), sinyal
/// kaynagi (dugme, basinc plakasi). Duz ve serilestirilebilir (TEKNIK 9): Godot dugumu tutmaz, kimlikle baglanir.
/// "Sonuc" alanlari her Tick'te yeniden hesaplanir.
/// </summary>
public sealed class PowerNode
{
    public int Id { get; set; }
    /// <summary>power.json parca kimligi ya da guc isteyen istasyonun yerlestirilebilir kimligi.</summary>
    public string Def { get; set; } = "";
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    /// <summary>Parcanin donusu (radyan; X/Y/Z soketin konumu).</summary>
    public float Yaw { get; set; }
    /// <summary>Tuketici onceligi 1 (en onemli) - 5; yetmeyince 5'ten baslayarak kapanir.</summary>
    public int Priority { get; set; } = 3;
    /// <summary>Elle: salter kapali (iletir) / lamba acik / tuketici acik.</summary>
    public bool On { get; set; } = true;
    /// <summary>Tuketici su an guc istiyor mu (istasyon: is suruyor). Lamba icin Tick hesaplar.</summary>
    public bool Wants { get; set; } = true;
    /// <summary>Hayvan Carki'nda hayvan / Basinc Plakasi'nda biri var (Godot her kare yazar).</summary>
    public bool Occupied { get; set; }
    /// <summary>Dugme: sinyalin kalan suresi (sn).</summary>
    public float PressLeft { get; set; }
    /// <summary>Aku: icindeki enerji (Wh).</summary>
    public float StoredWh { get; set; }

    // ---- sonuc (Tick) ----
    /// <summary>Tuketici beslendi / uretec uretti / ag canli.</summary>
    public bool Powered { get; set; }
    /// <summary>Uretec: bu tikteki uretim (W). Aku: + bosalma, - dolma (W).</summary>
    public float OutputW { get; set; }
    /// <summary>Sinyal kaynagi: cikis; alici: girisindeki en buyuk deger (0..100).</summary>
    public int Signal { get; set; }
    /// <summary>Alicinin sinyal teli var mi (varsa sinyal surer, yoksa elle).</summary>
    public bool SignalWired { get; set; }
    /// <summary>Tuketici yetmedigi icin kapandi (asiri yuk).</summary>
    public bool Shed { get; set; }
    /// <summary>Ait oldugu ag (Nets dizini); bagsiz -1.</summary>
    public int Net { get; set; } = -1;
}

/// <summary>Iki soket arasindaki kablo (GDD 11.1) ya da sinyal teli (11.2; A kaynak, B alici).</summary>
public sealed class PowerLink
{
    public int A { get; set; }
    public int B { get; set; }
    public bool Signal { get; set; }
    public float Length { get; set; }
}

/// <summary>Bir agin bu tikteki dengesi.</summary>
public sealed class PowerNetStats
{
    public float SupplyW { get; set; }
    public float DemandW { get; set; }
    public float ServedW { get; set; }
    /// <summary>+ akuden cekilen, - akuye dolan (W).</summary>
    public float BatteryW { get; set; }
    public float StoredWh { get; set; }
    public float CapacityWh { get; set; }
    public int Shed { get; set; }
    /// <summary>Talep uretim + akuyu asti, en az bir tuketici kapandi.</summary>
    public bool Overloaded => Shed > 0;
}

/// <summary>
/// Guc agi ve temel sinyal (GDD 11.1, 11.2; ICERIK 12 "M2 bedelleri"). Parcalar ve kablolar bir graf; her tikte
/// salterlerle bolunmus baglantili bilesenler (ag) bulunur, her agda uretim + aku talebi karsilar, yetmezse tuketiciler
/// oncelik sirasina gore kapanir; artan uretim akuyu doldurur. Sinyal: kaynak -> alici, 0..100, alici girislerinin en
/// buyugu. Duz durum: Nodes, Links, NextId, Clock (TEKNIK 9); Nets ve dugumlerin sonuc alanlari turetilir.
/// </summary>
public sealed class PowerNet
{
    public int NextId { get; set; } = 1;
    /// <summary>Oyun saniyesi (ruzgar evresi).</summary>
    public double Clock { get; set; }
    public List<PowerNode> Nodes { get; set; } = new();
    public List<PowerLink> Links { get; set; } = new();
    /// <summary>Son tikin aglari (turetilir).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public List<PowerNetStats> Nets { get; } = new();

    public PowerNode? Get(int id) => Nodes.Find(n => n.Id == id);

    public PowerNode Add(PowerFile f, string def, float x, float y, float z)
    {
        var d = f.Def(def) ?? throw new ArgumentException($"guc parcasi degil: {def}");
        var n = new PowerNode { Id = NextId++, Def = def, X = x, Y = y, Z = z, Priority = f.PriorityDefault };
        if (d.Kind == "consumer" && f.Consumers.ContainsKey(def)) n.Wants = false; // istasyon: is baslayinca ister
        Nodes.Add(n);
        return n;
    }

    /// <summary>Parcayi ve kablolarini kaldirir. Donen: sokulen kablo sayisi (Kablo iadesi).</summary>
    public int Remove(int id)
    {
        int freed = Links.RemoveAll(l => l.A == id || l.B == id);
        Nodes.RemoveAll(n => n.Id == id);
        return freed;
    }

    public void Move(int id, float x, float y, float z)
    {
        var n = Get(id);
        if (n == null) return;
        n.X = x; n.Y = y; n.Z = z;
    }

    public static float Distance(PowerNode a, PowerNode b)
    {
        float dx = a.X - b.X, dy = a.Y - b.Y, dz = a.Z - b.Z;
        return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    public int UsedSockets(int id) => Links.Count(l => !l.Signal && (l.A == id || l.B == id));

    /// <summary>
    /// Kablo cekilebilir mi. Sinyal telinde yon kendiliginden: kaynak (dugme, plaka) -> alici (salter, lamba). Donen null:
    /// cekilebilir; degilse oyuncuya yazilacak neden. <paramref name="from"/> ve <paramref name="to"/> sinyalde
    /// kaynak/aliciya cevrilir.
    /// </summary>
    public string? CanConnect(PowerFile f, int a, int b, bool signal)
    {
        var na = Get(a); var nb = Get(b);
        if (na == null || nb == null) return "Parça yok";
        if (a == b) return "Aynı parça";
        var da = f.Def(na.Def)!; var db = f.Def(nb.Def)!;
        if (Distance(na, nb) > f.CableMaxLength) return $"Kablo en fazla {f.CableMaxLength:0} m";
        if (signal)
        {
            if (da.IsSignalSource == db.IsSignalSource) return "Sinyal teli: Düğme ya da Basınç Plakası → Şalter ya da Lamba";
            var (src, dst) = da.IsSignalSource ? (da, db) : (db, da);
            if (!dst.SignalIn) return $"{dst.Id}: sinyal girişi yok";
            int s = da.IsSignalSource ? a : b, r = da.IsSignalSource ? b : a;
            if (Links.Any(l => l.Signal && l.A == s && l.B == r)) return "Zaten bağlı";
            return null;
        }
        if (da.Sockets <= 0 || db.Sockets <= 0) return "Güç soketi yok (sinyal teli için R)";
        if (Links.Any(l => !l.Signal && ((l.A == a && l.B == b) || (l.A == b && l.B == a)))) return "Zaten bağlı";
        if (UsedSockets(a) >= da.Sockets) return $"Soket dolu ({da.Sockets})";
        if (UsedSockets(b) >= db.Sockets) return $"Soket dolu ({db.Sockets})";
        return null;
    }

    /// <summary>Kabloyu ceker (once CanConnect). Cekildiyse kablo, degilse null.</summary>
    public PowerLink? Connect(PowerFile f, int a, int b, bool signal)
    {
        if (CanConnect(f, a, b, signal) != null) return null;
        if (signal && !f.Def(Get(a)!.Def)!.IsSignalSource) (a, b) = (b, a);
        var l = new PowerLink { A = a, B = b, Signal = signal, Length = Distance(Get(a)!, Get(b)!) };
        Links.Add(l);
        return l;
    }

    /// <summary>Bir parcanin butun kablolarini soker. Donen: sokulen sayi.</summary>
    public int Disconnect(int id) => Links.RemoveAll(l => l.A == id || l.B == id);

    public void Press(PowerFile f, int id)
    {
        var n = Get(id);
        if (n != null && f.Def(n.Def)?.Kind == "button") n.PressLeft = f.ButtonSeconds;
    }

    /// <summary>Ruzgar katsayisi 0..1 (power.json wind).</summary>
    public float WindFactor(PowerFile f)
    {
        var w = f.Wind;
        double t = Clock;
        double s = 0.6 * Math.Sin(2 * Math.PI * t / Math.Max(1, w.PeriodA)) + 0.4 * Math.Sin(2 * Math.PI * t / Math.Max(1, w.PeriodB));
        float k = (float)(0.5 + 0.5 * s);
        return Math.Clamp(w.Min + (1f - w.Min) * k, 0f, 1f);
    }

    /// <summary>Gercek saniyeyi oyun saatine cevirir (calendar.json: bir gun = gunduz + gece dakikasi = 24 saat).</summary>
    public static float GameHours(float dt, CalendarFile c)
    {
        float dayMinutes = (c.DaylightMinutes + c.NightMinutes) * Math.Max(0.01f, c.LengthScale);
        return dayMinutes <= 0 ? 0f : dt * 24f / (dayMinutes * 60f);
    }

    /// <summary>Agi bir adim hesaplar. <paramref name="dt"/> saniye (sinyal, ruzgar), <paramref name="dtHours"/> oyun saati (Wh).</summary>
    public void Tick(PowerFile f, float dt, float dtHours)
    {
        Clock += dt;
        var defs = new Dictionary<int, PowerDeviceDef>();
        foreach (var n in Nodes) defs[n.Id] = f.Def(n.Def) ?? new PowerDeviceDef { Id = n.Def, Kind = "pole" };

        // 1) Sinyal: kaynaklar, sonra alicilar.
        foreach (var n in Nodes)
        {
            var d = defs[n.Id];
            if (d.Kind == "button") { n.PressLeft = Math.Max(0f, n.PressLeft - dt); n.Signal = n.PressLeft > 0 ? f.SignalMax : 0; }
            else if (d.Kind == "plate") n.Signal = n.Occupied ? f.SignalMax : 0;
            else { n.Signal = 0; n.SignalWired = false; }
        }
        foreach (var l in Links.Where(l => l.Signal))
        {
            var s = Get(l.A); var r = Get(l.B);
            if (s == null || r == null) continue;
            r.SignalWired = true;
            r.Signal = Math.Max(r.Signal, s.Signal);
        }
        // Salter ve lamba: sinyal teli varsa sinyal surer, yoksa elle (On).
        bool Conducts(PowerNode n) => defs[n.Id].Kind != "switch" || (n.SignalWired ? n.Signal > 0 : n.On);
        foreach (var n in Nodes)
        {
            var d = defs[n.Id];
            if (d.Kind == "consumer" && !f.Consumers.ContainsKey(n.Def)) n.Wants = n.On && (!n.SignalWired || n.Signal > 0);
        }

        // 2) Aglar: acik salterin kablolari birlestirmez.
        var parent = Nodes.ToDictionary(n => n.Id, n => n.Id);
        int Find(int x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }
        foreach (var l in Links.Where(l => !l.Signal))
        {
            var a = Get(l.A); var b = Get(l.B);
            if (a == null || b == null || !Conducts(a) || !Conducts(b)) continue;
            int ra = Find(a.Id), rb = Find(b.Id);
            if (ra != rb) parent[ra] = rb;
        }
        var linked = new HashSet<int>(Links.Where(l => !l.Signal).SelectMany(l => new[] { l.A, l.B }));
        var groups = Nodes.Where(n => linked.Contains(n.Id) && Conducts(n)).GroupBy(n => Find(n.Id)).OrderBy(g => g.Min(n => n.Id)).ToList();
        Nets.Clear();
        foreach (var n in Nodes) { n.Net = -1; n.Powered = false; n.Shed = false; n.OutputW = 0; }
        float wind = WindFactor(f);

        foreach (var g in groups)
        {
            var st = new PowerNetStats();
            int netIndex = Nets.Count;
            Nets.Add(st);
            var members = g.ToList();
            foreach (var n in members) n.Net = netIndex;
            // Uretim
            foreach (var n in members)
            {
                var d = defs[n.Id];
                if (d.Kind != "generator") continue;
                float w = d.Source switch { "wind" => d.Watts * wind, "animal" => n.Occupied ? d.Watts : 0f, _ => d.Watts };
                n.OutputW = w;
                n.Powered = w > 0;
                st.SupplyW += w;
            }
            // Aku
            var bats = members.Where(n => defs[n.Id].Kind == "battery").ToList();
            float batMax = 0;
            foreach (var b in bats)
            {
                var d = defs[b.Id];
                st.StoredWh += b.StoredWh; st.CapacityWh += d.CapacityWh;
                batMax += dtHours > 0 ? Math.Min(d.Watts, b.StoredWh / dtHours) : (b.StoredWh > 0 ? d.Watts : 0);
            }
            // Talep: oncelik 1 once; ayni oncelikte eski parca once. Sigmayan kapanir, kucugu sigarsa beslenir.
            var want = members.Where(n => defs[n.Id].Kind == "consumer" && n.Wants).OrderBy(n => n.Priority).ThenBy(n => n.Id).ToList();
            float left = st.SupplyW + batMax;
            foreach (var n in want)
            {
                float w = defs[n.Id].Watts;
                st.DemandW += w;
                if (w <= left + 1e-4f) { left -= w; n.Powered = true; st.ServedW += w; }
                else { n.Shed = true; st.Shed++; }
            }
            // Aku dengesi
            float net = st.ServedW - st.SupplyW;
            if (net > 0 && bats.Count > 0)
            {
                float total = bats.Sum(b => b.StoredWh);
                foreach (var b in bats)
                {
                    float share = total > 0 ? net * b.StoredWh / total : net / bats.Count;
                    b.StoredWh = Math.Max(0f, b.StoredWh - share * dtHours);
                    b.OutputW = share;
                }
                st.BatteryW = net;
            }
            else if (net < 0 && bats.Count > 0)
            {
                float surplus = -net;
                foreach (var b in bats)
                {
                    var d = defs[b.Id];
                    float room = dtHours > 0 ? (d.CapacityWh - b.StoredWh) / dtHours : 0f;
                    float w = Math.Max(0f, Math.Min(Math.Min(d.Watts, room), surplus));
                    b.StoredWh = Math.Min(d.CapacityWh, b.StoredWh + w * dtHours);
                    b.OutputW = -w;
                    surplus -= w;
                    st.BatteryW -= w;
                }
            }
            st.StoredWh = bats.Sum(b => b.StoredWh);
            bool live = st.SupplyW > 0 || st.StoredWh > 0;
            foreach (var n in members)
            {
                var k = defs[n.Id].Kind;
                if (k is "pole" or "switch") n.Powered = live;
                else if (k == "battery") n.Powered = n.StoredWh > 0;
            }
        }
    }

    /// <summary>Parcanin aginin dengesi; bagsizsa null.</summary>
    public PowerNetStats? StatsOf(int id)
    {
        var n = Get(id);
        return n == null || n.Net < 0 || n.Net >= Nets.Count ? null : Nets[n.Net];
    }
}
