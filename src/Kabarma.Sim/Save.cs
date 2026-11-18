using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Kabarma.Sim;

// Kayit sozlesmesi (TEKNIK 9.1). Bolum arayuzu, isaretler, JSON yardimcisi, kayit defteri ve bellekteki kayit koku.
// Dosya bicimi SaveCodec.cs, dosya islemleri (gecici dosya, yedek, boyut siniri) SaveStore.cs.

/// <summary>Bolumun gittigi dosya: dunya kaydi ya da dunyalar arasi tasinan karakter (GDD 22.1).</summary>
public enum SaveScope { World, Character }

/// <summary>Yukleme sirasi: once dunyada var olanlar, sonra sistem durumu, en son ilerleme sayaclari.</summary>
public static class SaveOrder
{
    /// <summary>Dunyada var olanlarin kurulmasi (yapilar, yerlestirilebilirler, yaratiklar, arazi farki).</summary>
    public const int Spawn = 100;
    /// <summary>Sistem durumu (tarla, agil, istasyon kuyrugu, canta, takvim, gece).</summary>
    public const int State = 200;
    /// <summary>Ilerleme sayaclari (senaryo, Defter, Known, kredi): en son, baskasinin yuklemesi sayaci oynatmasin.</summary>
    public const int Progress = 300;
}

/// <summary>
/// Kaydin bir bolumu. Kimlik kalicidir (kucuk harf, noktali). Surum 1'den baslar; arttiginda her eski surum icin
/// Migrate adimi ve birim testi zorunludur. Read durumu sessizce yerine koyar (olay yaymaz, sayac ilerletmez).
/// </summary>
public interface ISaveSection
{
    string Id { get; }
    int Version { get; }
    SaveScope Scope => SaveScope.World;
    int Order => SaveOrder.State;
    byte[] Write();
    /// <summary>Veri bu bolumun guncel surumune goc ettirilmis gelir.</summary>
    void Read(byte[] data);
    /// <summary>Tek adim: <paramref name="from"/> surumundeki veriyi from+1 surumune cevirir.</summary>
    byte[] Migrate(int from, byte[] data) => throw new SaveException($"'{Id}' bolumu v{from} icin goc adimi yok");
    /// <summary>Butun bolumler okunduktan sonra (sirayla): baglantilar ve gorunumler.</summary>
    void AfterRestore() { }
}

/// <summary>
/// GameState uzerinde bir ozelligi kayit bolumu yapar: get bolumun DTO'sunu uretir, set uygular (sessizce). Ozellik
/// public olmak zorunda degil. Her akis kendi partial dosyasinda yazar; GameState.SaveSections yansimayla bulur.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class SaveSectionAttribute : Attribute
{
    public string Id { get; }
    public int Version { get; }
    public int Order { get; set; } = SaveOrder.State;
    public SaveScope Scope { get; set; } = SaveScope.World;
    /// <summary>Bu bolumun kapsadigi GameState ozellikleri (SaveContractTests her ozelligin kapsandigini denetler).</summary>
    public string[] Covers { get; set; } = Array.Empty<string>();
    public SaveSectionAttribute(string id, int version) { Id = id; Version = version; }
}

/// <summary>
/// JSON bolumunun goc adimi: GameState'te static void Ad(JsonObject o) ya da static JsonNode Ad(JsonNode o).
/// <c>From</c> surumundeki nesneyi From+1'e cevirir.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class SaveMigrationAttribute : Attribute
{
    public string Id { get; }
    public int From { get; }
    public SaveMigrationAttribute(string id, int from) { Id = id; From = from; }
}

public sealed class SaveException : Exception
{
    public SaveException(string message, Exception? inner = null) : base(message, inner) { }
}

/// <summary>Kayit JSON'u: kararli (ayni durum ayni bayt), sikistirmasiz, float gidis-donus kayipsiz.</summary>
public static class SaveJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        IncludeFields = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };

    public static byte[] Bytes<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value, Options);
    public static T From<T>(byte[] data) => JsonSerializer.Deserialize<T>(data, Options) ?? throw new SaveException($"{typeof(T).Name} okunamadi");
    public static object From(byte[] data, Type t) => JsonSerializer.Deserialize(data, t, Options) ?? throw new SaveException($"{t.Name} okunamadi");
}

/// <summary>DTO tabanli bolum: capture DTO uretir, apply uygular. Goc adimlari JsonObject uzerinde.</summary>
public sealed class JsonSaveSection<T> : ISaveSection
{
    readonly Func<T> _capture;
    readonly Action<T> _apply;
    readonly Dictionary<int, Func<JsonNode, JsonNode>> _steps = new();
    readonly Action? _after;

    public string Id { get; }
    public int Version { get; }
    public SaveScope Scope { get; }
    public int Order { get; }

    public JsonSaveSection(string id, int version, Func<T> capture, Action<T> apply, int order = SaveOrder.State,
        SaveScope scope = SaveScope.World, Action? afterRestore = null)
    {
        SaveSection.CheckId(id);
        if (version < 1) throw new ArgumentOutOfRangeException(nameof(version));
        Id = id; Version = version; Order = order; Scope = scope;
        _capture = capture; _apply = apply; _after = afterRestore;
    }

    /// <summary>from -> from+1 adimi (yerinde degistirip ayni dugumu dondurmek serbest).</summary>
    public JsonSaveSection<T> Step(int from, Func<JsonNode, JsonNode> step) { _steps[from] = step; return this; }
    public JsonSaveSection<T> Step(int from, Action<JsonObject> step) => Step(from, n => { step(n.AsObject()); return n; });

    public bool HasStep(int from) => _steps.ContainsKey(from);
    public byte[] Write() => SaveJson.Bytes(_capture());
    public void Read(byte[] data) => _apply(SaveJson.From<T>(data));
    public void AfterRestore() => _after?.Invoke();

    public byte[] Migrate(int from, byte[] data)
    {
        if (!_steps.TryGetValue(from, out var step)) throw new SaveException($"'{Id}' bolumu v{from} icin goc adimi yok");
        var node = JsonNode.Parse(data) ?? throw new SaveException($"'{Id}' v{from} bos");
        return JsonSerializer.SerializeToUtf8Bytes(step(node), SaveJson.Options);
    }
}

public static class SaveSection
{
    /// <summary>Godot katmaninin bagladigi bolum: <c>Director.Save.Add(SaveSection.Json("pen.gates", 1, Capture, Apply))</c>.</summary>
    public static JsonSaveSection<T> Json<T>(string id, int version, Func<T> capture, Action<T> apply, int order = SaveOrder.State,
        SaveScope scope = SaveScope.World, Action? afterRestore = null) => new(id, version, capture, apply, order, scope, afterRestore);

    public static void CheckId(string id)
    {
        if (string.IsNullOrEmpty(id) || id.Length > 64 || !id.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '.' or '_'))
            throw new ArgumentException($"gecersiz bolum kimligi '{id}' (kucuk harf, rakam, '.', '_')");
    }

    /// <summary>[SaveSection] isaretli ozellikleri ve [SaveMigration] adimlarini bulur (her tip icin; GameState kullanir).</summary>
    public static List<ISaveSection> FromAttributes(object owner)
    {
        var t = owner.GetType();
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        var steps = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .SelectMany(m => m.GetCustomAttributes<SaveMigrationAttribute>().Select(a => (a, m))).ToList();
        var list = new List<ISaveSection>();
        foreach (var p in t.GetProperties(all).OrderBy(p => p.Name, StringComparer.Ordinal))
        {
            var a = p.GetCustomAttribute<SaveSectionAttribute>();
            if (a == null) continue;
            if (p.GetMethod == null || p.SetMethod == null) throw new SaveException($"{t.Name}.{p.Name}: [SaveSection] ozelligi get ve set ister");
            var s = new PropertySection(owner, p, a);
            foreach (var (ma, m) in steps.Where(x => x.a.Id == a.Id)) s.AddStep(ma.From, m);
            list.Add(s);
        }
        return list;
    }

    sealed class PropertySection : ISaveSection
    {
        readonly object _owner;
        readonly PropertyInfo _p;
        readonly Dictionary<int, MethodInfo> _steps = new();
        public string Id { get; }
        public int Version { get; }
        public SaveScope Scope { get; }
        public int Order { get; }
        public string[] Covers { get; }

        public PropertySection(object owner, PropertyInfo p, SaveSectionAttribute a)
        {
            CheckId(a.Id);
            _owner = owner; _p = p;
            Id = a.Id; Version = a.Version; Scope = a.Scope; Order = a.Order; Covers = a.Covers;
        }

        public void AddStep(int from, MethodInfo m) => _steps[from] = m;
        public byte[] Write() => JsonSerializer.SerializeToUtf8Bytes(_p.GetValue(_owner), _p.PropertyType, SaveJson.Options);
        public void Read(byte[] data) => _p.SetValue(_owner, SaveJson.From(data, _p.PropertyType));

        public byte[] Migrate(int from, byte[] data)
        {
            if (!_steps.TryGetValue(from, out var m)) throw new SaveException($"'{Id}' bolumu v{from} icin goc adimi yok");
            var node = JsonNode.Parse(data) ?? throw new SaveException($"'{Id}' v{from} bos");
            var r = m.Invoke(null, new object[] { m.GetParameters()[0].ParameterType == typeof(JsonObject) ? node.AsObject() : node });
            return JsonSerializer.SerializeToUtf8Bytes(r as JsonNode ?? node, SaveJson.Options);
        }

        public bool HasStep(int from) => _steps.ContainsKey(from);
    }

    /// <summary>Bolumun bu surumden bir adimi var mi (sozlesme testi; bilinmeyen bolum turunde false).</summary>
    public static bool HasStep(ISaveSection s, int from) => s switch
    {
        PropertySection p => p.HasStep(from),
        _ when s.GetType().IsGenericType && s.GetType().GetGenericTypeDefinition() == typeof(JsonSaveSection<>)
            => (bool)s.GetType().GetMethod("HasStep")!.Invoke(s, new object[] { from })!,
        _ => TryMigrates(s, from),
    };

    static bool TryMigrates(ISaveSection s, int from)
    {
        try { s.Migrate(from, Array.Empty<byte>()); return true; }
        catch (SaveException) { return false; }
        catch { return true; } // bos veriyle patladi ama adim var
    }

    /// <summary>[SaveSection] ozelliklerinin Covers listesi (sozlesme testi).</summary>
    public static IEnumerable<string> CoveredBy(ISaveSection s) => s is PropertySection p ? p.Covers : Array.Empty<string>();
}

/// <summary>Kaydin bir bolumunun ham verisi.</summary>
public sealed class SaveBlob
{
    public int Version { get; set; }
    public byte[] Data { get; set; } = Array.Empty<byte>();
}

/// <summary>Bildirim (TEKNIK 9): menunun ve insanin okudugu kisim; world.kbs icinde de durur.</summary>
public sealed class SaveManifest
{
    public int Format { get; set; } = 1;
    /// <summary>Oyunun surumu (TEKNIK 12 version.txt; yoksa "dev").</summary>
    public string Game { get; set; } = "dev";
    public string Scope { get; set; } = "world";
    public string Name { get; set; } = "";
    public ulong Seed { get; set; }
    /// <summary>Oyun gunu (takvim).</summary>
    public int Day { get; set; } = 1;
    public List<string> Crew { get; set; } = new();
    /// <summary>ISO 8601, UTC.</summary>
    public string SavedUtc { get; set; } = "";
    public double PlaySeconds { get; set; }
    /// <summary>Kaydin nedeni: elle, otomatik, safak, cikis.</summary>
    public string Reason { get; set; } = "";
    public List<SaveSectionInfo> Sections { get; set; } = new();
}

public sealed class SaveSectionInfo
{
    public string Id { get; set; } = "";
    public int Version { get; set; }
    public int Bytes { get; set; }
}

/// <summary>Bellekteki kayit: bildirim + bolumler. Dosyaya SaveCodec yazar; ortak oyunda dogrudan gonderilir.</summary>
public sealed class SaveGame
{
    public SaveManifest Manifest { get; set; } = new();
    public SortedDictionary<string, SaveBlob> Sections { get; } = new(StringComparer.Ordinal);
}

/// <summary>Yuklemenin dokumu: test ve gunluk icin.</summary>
public sealed class RestoreReport
{
    public List<string> Loaded { get; } = new();
    /// <summary>Kayitta olmayan bolumler: yeni oyun degeriyle kaldi.</summary>
    public List<string> Missing { get; } = new();
    /// <summary>Kayitta olup burada kaydi olmayan bolumler: silinmeden tasinir.</summary>
    public List<string> Unknown { get; } = new();
    public List<(string id, int from, int to)> Migrated { get; } = new();
    public override string ToString() =>
        $"yuklendi {Loaded.Count}, eksik {Missing.Count} [{string.Join(",", Missing)}], bilinmeyen {Unknown.Count} [{string.Join(",", Unknown)}], goc {Migrated.Count}";
}

/// <summary>
/// Kayit defteri (ornek basina, statik degil): bolumleri toplar, kaydi alir ve geri yukler. Godot katmani
/// Director.Save uzerinden bolum ekler; Sim bolumleri GameState.SaveSections'tan gelir.
/// </summary>
public sealed class SaveRegistry
{
    readonly List<ISaveSection> _sections = new();
    /// <summary>Son yuklenen kayitta taninmayan bolumler: sonraki kayda aynen yazilir.</summary>
    readonly SortedDictionary<string, SaveBlob> _carried = new(StringComparer.Ordinal);

    public IReadOnlyList<ISaveSection> Sections => _sections;

    public SaveRegistry Add(ISaveSection s)
    {
        if (_sections.Any(x => x.Id == s.Id)) throw new SaveException($"'{s.Id}' bolumu iki kez kaydedildi");
        _sections.Add(s);
        return this;
    }

    public SaveRegistry AddRange(IEnumerable<ISaveSection> list) { foreach (var s in list) Add(s); return this; }

    public ISaveSection? Find(string id) => _sections.FirstOrDefault(s => s.Id == id);

    IEnumerable<ISaveSection> Ordered(SaveScope scope) =>
        _sections.Where(s => s.Scope == scope).OrderBy(s => s.Order).ThenBy(s => s.Id, StringComparer.Ordinal);

    /// <summary>Bu kapsamdaki butun bolumleri yazar. Bildirimin bolum listesi burada doldurulur.</summary>
    public SaveGame Capture(SaveScope scope, SaveManifest? manifest = null)
    {
        var g = new SaveGame { Manifest = manifest ?? new SaveManifest() };
        g.Manifest.Scope = scope == SaveScope.World ? "world" : "character";
        foreach (var s in Ordered(scope))
        {
            byte[] data;
            try { data = s.Write(); }
            catch (Exception e) when (e is not SaveException) { throw new SaveException($"'{s.Id}' bolumu yazilamadi: {e.Message}", e); }
            g.Sections[s.Id] = new SaveBlob { Version = s.Version, Data = data };
        }
        if (scope == SaveScope.World)
            foreach (var (id, b) in _carried) g.Sections.TryAdd(id, b);
        g.Manifest.Sections = g.Sections.Select(kv => new SaveSectionInfo { Id = kv.Key, Version = kv.Value.Version, Bytes = kv.Value.Data.Length }).ToList();
        return g;
    }

    /// <summary>
    /// Kaydi yerine koyar: sirayla (Spawn, State, Progress) her bolumu goc ettirip okur, sonra hepsinin AfterRestore'unu
    /// cagirir. Kayitta olmayan bolum yeni oyun degeriyle kalir. Gelecekteki surum yuklenmez (SaveException).
    /// </summary>
    public RestoreReport Restore(SaveGame g, SaveScope scope = SaveScope.World)
    {
        var r = new RestoreReport();
        var ordered = Ordered(scope).ToList();
        // Once butun goclari yap ve surumleri denetle: yarim yukleme olmasin.
        var ready = new Dictionary<string, byte[]>();
        foreach (var s in ordered)
        {
            if (!g.Sections.TryGetValue(s.Id, out var b)) { r.Missing.Add(s.Id); continue; }
            if (b.Version > s.Version) throw new SaveException($"'{s.Id}' bolumu v{b.Version}, bu surum en fazla v{s.Version} okur (kayit daha yeni bir oyundan)");
            var data = b.Data;
            for (int v = b.Version; v < s.Version; v++)
            {
                try { data = s.Migrate(v, data); }
                catch (Exception e) when (e is not SaveException) { throw new SaveException($"'{s.Id}' v{v}->v{v + 1} gocu basarisiz: {e.Message}", e); }
            }
            if (b.Version < s.Version) r.Migrated.Add((s.Id, b.Version, s.Version));
            ready[s.Id] = data;
        }
        foreach (var s in ordered)
        {
            if (!ready.TryGetValue(s.Id, out var data)) continue;
            try { s.Read(data); }
            catch (Exception e) when (e is not SaveException) { throw new SaveException($"'{s.Id}' bolumu okunamadi: {e.Message}", e); }
            r.Loaded.Add(s.Id);
        }
        if (scope == SaveScope.World)
        {
            _carried.Clear();
            foreach (var (id, b) in g.Sections)
                if (_sections.All(s => s.Id != id)) { _carried[id] = b; r.Unknown.Add(id); }
        }
        foreach (var s in ordered) s.AfterRestore();
        return r;
    }
}
