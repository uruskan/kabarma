using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Director, dolap (M4 dolap; ICERIK §9, GDD 23): uye tipleri ve kozmetik. Kural Sim'de (Wardrobe, karakter dosyasinda);
/// burada baglanti: uye pasifleri oyuna (hiz, can, suzulme, XP, gelir, katalog, mutasyon, uretim hizi, Tirpan hasari),
/// kozmetigin oyuncuya uygulanmasi (CosmeticView), is emri odulleri (GrantMade -> Unlock), dolap menusu (K), jest (J)
/// ve ortak oyunda gorunumun senkronu ("looks" bolumu; NetAvatar okur).
/// Baglanan pasifler: Member liveKeys (members.json). Digerleri (Coban, Asci, Nine, Robot, Holding Firarisi, Makinist kurma hizi,
/// Pehlivan itme) secilir ama etkisi ilgili sistem Wardrobe.Effect(anahtar) satirini ekleyince calisir.
/// </summary>
public partial class Director
{
    readonly Dictionary<byte, string> _looks = new();
    string _lastLook = "";

    /// <summary>Ortak oyunda bir oyuncunun takili kozmetikleri ("slot=kimlik;..."); bos: bilinmiyor.</summary>
    public string LookOf(byte peer) => _looks.GetValueOrDefault(peer, "");

    void InitDolap()
    {
        var w = State.Wardrobe;
        State.Mastery.XpScale = () => w.Mult("mastery_gain");
        State.Cargo.IncomeMult = () => w.Mult("income");
        State.Catalog.PriceMult = () => 1f + w.Effect("catalog");
        State.Orders.GrantMade += (_, g) => { if (g.Kind is "member" or "cosmetic") w.Unlock(g.Kind, g.Id); };
        foreach (var key in State.Orders.Granted.ToList())
        {
            int i = key.IndexOf(':');
            if (i > 0 && key[..i] is "member" or "cosmetic") w.Unlock(key[..i], key[(i + 1)..]);
        }
        w.Changed += OnWardrobeChanged;
        w.Unlocked += (kind, id) =>
        {
            var name = kind == "member" ? Data.Members.Member(id)?.Name : Data.Cosmetics.Get(id)?.Name;
            if (!string.IsNullOrEmpty(name)) Hud?.Hint($"Dolap: {name} açıldı (K)", 3.5f);
        };
        AddChild(new DolapInput(this) { Name = "DolapInput" });
        Callable.From(InitDolapNet).CallDeferred();
        Callable.From(OnWardrobeChanged).CallDeferred(); // karakter dosyasi yuklendikten sonra
    }

    /// <summary>Uye pasifleri ve gorunum (Wardrobe degisince, yuklemede, yemek etkisi degisince).</summary>
    public void ApplyMember()
    {
        var w = State.Wardrobe;
        if (Player == null) return;
        Player.MemberSpeed = w.Effect("speed");
        Player.MaxHp = 100f + w.Effect("max_hp");
        Player.Glide = w.Flag("glide");
        State.Stamina.DrainMult = w.Flag("sprint_free") ? 0f : Mathf.Max(0.1f, 1f + State.Food.Effect("sprintCost")); // Kacak: depar dayaniklilik harcamaz (yemek etkisi: Director.Asi.cs)
        Data.Crops.Genetics.MutationMult = w.Mult("mutation");   // Arici: mutasyon sansi x1,5
    }

    void OnWardrobeChanged()
    {
        ApplyMember();
        ApplyLook();
        var look = State.Wardrobe.Look();
        if (look != _lastLook) { _lastLook = look; SendLook(look); }
    }

    /// <summary>Takili kozmetikleri yerel oyuncuya uygular (model yuklu degilse sonra yine denenir).</summary>
    public void ApplyLook()
    {
        var skel = Player?.Skeleton;
        if (skel == null) { Callable.From(ApplyLook).CallDeferred(); return; }
        var eq = Wardrobe.ParseLook(State.Wardrobe.Look());
        CosmeticView.Apply(skel, eq, Data, Player.Visor, Player.CapaNode);
    }

    // ---------------- ag ----------------

    void InitDolapNet()
    {
        var net = Net;
        if (net == null) return;
        net.Register("looks", false,
            (wr, _) => { wr.U8((byte)_looks.Count); foreach (var kv in _looks.OrderBy(k => k.Key)) { wr.U8(kv.Key); wr.Str(kv.Value); } },
            r => { int n = r.U8(); var keep = _looks.GetValueOrDefault(Net.LocalPeer); _looks.Clear(); for (int i = 0; i < n; i++) { byte p = r.U8(); _looks[p] = r.Str(); } });
        net.On("look", (actor, r) => _looks[actor.Peer] = r.Str());
        SendLook(State.Wardrobe.Look());
    }

    void SendLook(string look)
    {
        var net = Net;
        if (net == null) return;
        if (net.IsHost) _looks[0] = look;
        else net.Forward("look", wr => wr.Str(look));
    }

    // ---------------- menu ----------------

    static readonly (string slot, string title)[] Slots =
    {
        ("kask", "Kask"), ("vizor", "Vizör yüzü"), ("tulum", "Tulum deseni"), ("canta", "Çanta süsü"), ("capa", "Çapa kaplaması"), ("jest", "Jest"),
    };

    public void OpenWardrobe()
    {
        var w = State.Wardrobe;
        var items = new List<Hud.MenuItem> { new($"Üye tipi: {w.ActiveDef?.Name}  ({w.Members.Count}/{Data.Members.Members.Count})", () => true, OpenMembers) };
        foreach (var (slot, title) in Slots)
        {
            string cur = Data.Cosmetics.Get(w.Equipped(slot))?.Name ?? "yok";
            var s = slot;
            int owned = Data.Cosmetics.Cosmetics.Count(c => c.Slot == s && w.HasCosmetic(c.Id));
            items.Add(new($"{title}: {cur}  ({owned}/{Data.Cosmetics.Cosmetics.Count(c => c.Slot == s)})", () => true, () => OpenSlot(s, title)));
        }
        items.Add(new("Jest oyna (J)", () => true, () => { Hud.HideMenu(); PlayGesture(); }));
        Hud.ShowMenu("Dolap", items, "{act:interact} / {act:ui_cancel}: kapat");
    }

    void OpenMembers()
    {
        var w = State.Wardrobe;
        var items = new List<Hud.MenuItem>();
        foreach (var m in Data.Members.Members)
        {
            var mm = m;
            bool has = w.HasMember(m.Id);
            string mark = w.Active == m.Id ? "● " : has ? "" : "🔒 ";
            string tail = has ? m.Passive + (m.LiveKeys.Count > 0 ? "" : " (etkisi sonra)") : m.Unlock;
            items.Add(new($"{mark}{m.Name}: {tail}", () => has, () => { w.SetMember(mm.Id); OpenMembers(); }));
        }
        Hud.ShowMenu("Dolap · Üye tipi", items, "Üye pasifi ve Kabarma'ya başladığı kart seçilince değişir  ·  {act:ui_cancel}: geri");
    }

    void OpenSlot(string slot, string title)
    {
        var w = State.Wardrobe;
        var items = new List<Hud.MenuItem>();
        if (slot is not "kask" and not "vizor" and not "tulum" and not "capa" and not "jest")
            items.Add(new("Çıkar (yok)", () => w.Equipped(slot) != "", () => { w.Unequip(slot); OpenSlot(slot, title); }));
        foreach (var c in Data.Cosmetics.Cosmetics.Where(c => c.Slot == slot))
        {
            var cc = c;
            bool has = w.HasCosmetic(c.Id);
            string mark = w.Equipped(slot) == c.Id ? "● " : has ? "" : "🔒 ";
            string how = has ? "" : c.Source switch { "catalog" => $"  (Katalog, {c.Price} Kredi)", "order" => "  (iş emri)", "wheel" => "  (Tohum Çarkı)", "hq" => "  (Genel Merkez)", _ => "" };
            items.Add(new($"{mark}{c.Name}{how}", () => has, () =>
            {
                w.Equip(cc.Id);
                if (slot == "jest") PlayGesture();
                OpenSlot(slot, title);
            }));
        }
        Hud.ShowMenu($"Dolap · {title}", items, "{act:ui_cancel}: geri");
    }

    /// <summary>Takili jesti oynatir (mevcut klip ya da poz; hareket edince kesilir).</summary>
    public void PlayGesture()
    {
        var c = Data.Cosmetics.Get(State.Wardrobe.Equipped("jest"));
        if (c == null || Player == null) return;
        string clip = c.Look.GetValueOrDefault("clip", "wave");
        float speed = float.TryParse(c.Look.GetValueOrDefault("speed", "1"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var s) ? s : 1f;
        float seconds = 2.6f / Mathf.Max(0.3f, speed);
        if (clip is "sleep" or "eat" or "plant") Player.PlayPose(clip, seconds);
        else Player.PlayAction(clip, seconds);
        Player.Visor?.Event("kutlama");
    }
}

/// <summary>Dolap kisayollari: K dolap menusu, J jest.</summary>
public partial class DolapInput : Node
{
    readonly Director _d;
    public DolapInput(Director d) { _d = d; }

    public override void _UnhandledKeyInput(InputEvent e)
    {
        if (e is not InputEventKey { Pressed: true, Echo: false } k || _d.Hud == null || _d.NightActive) return;
        if (k.Keycode == Key.K) { if (_d.Hud.MenuOpen) _d.Hud.HideMenu(); else _d.OpenWardrobe(); GetViewport().SetInputAsHandled(); }
        else if (k.Keycode == Key.J && !_d.Hud.MenuOpen) { _d.PlayGesture(); GetViewport().SetInputAsHandled(); }
    }
}
