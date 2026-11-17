using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Envanter (GDD 5, 3.3; SANAT-SES 8.2 "envanter ve kusam"): sirt cantasi izgarasi, 8 yuvali kemer ve 3 yemek yuvasi.
/// Ayni panelin ikinci sayfasi Defter (Hud.Defter.cs). Klavye ve oyun koluyla: yon tuslari / d-pad secer,
/// Enter / A ye ya da kemere tak, 1-8 secileni o kemer yuvasina koyar, Q / tekerlek / omuz tuslari sayfa degistirir,
/// Tab / E / Esc / B kapatir. Ikonlar yer tutucu (renkli harf karesi): modelden ikon cekme varlik hattinda.
/// </summary>
public partial class Hud
{
    PanelContainer _inv;
    Label _invTabBag, _invTabDefter, _invFoot, _invMoney;
    Control _invBagPage;
    VBoxContainer _defterPage;
    GridContainer _invGrid;
    HBoxContainer _invBeltRow, _invFoodRow;
    VBoxContainer _invInfo;
    readonly List<PanelContainer> _invCells = new();
    readonly List<PanelContainer> _invBeltCells = new();
    readonly List<PanelContainer> _invFoodCells = new();
    int _invSel;
    int _invPage; // 0 Canta, 1 Defter
    float _invRefreshT;
    bool _joyL, _joyR;

    public bool InventoryOpen => _inv != null && _inv.Visible;
    /// <summary>Acik sayfa: 0 Canta, 1 Defter.</summary>
    public int InventoryPage => _invPage;
    /// <summary>Secili canta yuvasi.</summary>
    public int InventorySelected => _invSel;
    /// <summary>Izgaranin sutun sayisi (bot girdi kipinde yon tuslariyla gezer; entegrasyon).</summary>
    public int InventoryColumns => _invGrid?.Columns ?? 1;

    const int CellW = 92, CellH = 78;

    void BuildInventory(Control root)
    {
        _inv = new PanelContainer { Visible = false };
        _inv.AddThemeStyleboxOverride("panel", Box(new Color(Palette.NightDeep, 0.94f), 18, Palette.Warning, 3));
        var v = new VBoxContainer { CustomMinimumSize = new Vector2(940, 0) };
        v.AddThemeConstantOverride("separation", 8);

        // Sekmeler
        var tabs = new HBoxContainer();
        tabs.AddThemeConstantOverride("separation", 22);
        _invTabBag = L("ÇANTA", 26, Palette.Warning, true);
        _invTabDefter = L("DEFTER", 26, Palette.Fog, true);
        var tabHint = L("Q: sayfa", 14, Palette.Fog, true);
        tabHint.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _invMoney = L("", 20, Palette.Warning, true);
        tabs.AddChild(_invTabBag); tabs.AddChild(_invTabDefter); tabs.AddChild(tabHint); tabs.AddChild(_invMoney);
        v.AddChild(tabs);
        v.AddChild(new HSeparator());

        // Canta sayfasi: solda izgara, sagda secilenin bilgisi; altta kemer ve yemek yuvalari
        var bag = new VBoxContainer();
        bag.AddThemeConstantOverride("separation", 10);
        var top = new HBoxContainer();
        top.AddThemeConstantOverride("separation", 16);
        _invGrid = new GridContainer { Columns = 6 };
        _invGrid.AddThemeConstantOverride("h_separation", 6);
        _invGrid.AddThemeConstantOverride("v_separation", 6);
        top.AddChild(_invGrid);
        _invInfo = new VBoxContainer { CustomMinimumSize = new Vector2(300, 0) };
        _invInfo.AddThemeConstantOverride("separation", 4);
        top.AddChild(_invInfo);
        bag.AddChild(top);

        var lower = new HBoxContainer();
        lower.AddThemeConstantOverride("separation", 26);
        var beltBox = new VBoxContainer();
        beltBox.AddChild(L("KEMER", 15, Palette.Fog, true));
        _invBeltRow = new HBoxContainer();
        _invBeltRow.AddThemeConstantOverride("separation", 5);
        for (int i = 0; i < Kabarma.Sim.Belt.Slots; i++) { var c = SmallCell(); _invBeltRow.AddChild(c); _invBeltCells.Add(c); }
        beltBox.AddChild(_invBeltRow);
        lower.AddChild(beltBox);
        var foodBox = new VBoxContainer();
        foodBox.AddChild(L("YEMEK YUVALARI", 15, Palette.Fog, true));
        _invFoodRow = new HBoxContainer();
        _invFoodRow.AddThemeConstantOverride("separation", 5);
        for (int i = 0; i < _d.State.Food.Slots; i++) { var c = SmallCell(); c.CustomMinimumSize = new Vector2(104, 62); _invFoodRow.AddChild(c); _invFoodCells.Add(c); } // yuva sayisi tool.json "foodSlots" (ekonomi; entegrasyon)
        foodBox.AddChild(_invFoodRow);
        lower.AddChild(foodBox);
        bag.AddChild(lower);
        _invBagPage = bag;
        v.AddChild(bag);

        _defterPage = new VBoxContainer { Visible = false };
        _defterPage.AddThemeConstantOverride("separation", 6);
        v.AddChild(_defterPage);

        v.AddChild(new HSeparator());
        _invFoot = L("", 15, Palette.Fog, true);
        _invFoot.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _invFoot.CustomMinimumSize = new Vector2(900, 0);
        v.AddChild(_invFoot);
        _inv.AddChild(v);
        root.AddChild(_inv);
    }

    PanelContainer SmallCell()
    {
        var c = new PanelContainer { CustomMinimumSize = new Vector2(78, 62) };
        c.AddThemeStyleboxOverride("panel", CellStyle(false, false));
        return c;
    }

    StyleBoxFlat CellStyle(bool selected, bool filled)
    {
        var sb = Box(new Color(filled ? Palette.DarkGrey : Palette.NightDeep, filled ? 0.9f : 0.6f), 10,
                     selected ? Palette.Warning : new Color(Palette.OffWhite, 0.18f), selected ? 3 : 1);
        sb.ContentMarginLeft = sb.ContentMarginRight = 5; sb.ContentMarginTop = sb.ContentMarginBottom = 4;
        return sb;
    }

    /// <summary>Yer tutucu ikon rengi: esya kimliginden (palet tonlari; yeni renk yok).</summary>
    static Color ItemColor(string id)
    {
        Color[] tones = { Palette.Ground, Palette.Plant, Palette.Accent, Palette.CoopBlue, Palette.Wood, Palette.MayaViolet, Palette.Rock, Palette.TurnipTop, Palette.SkyNoon };
        int h = 0; foreach (char ch in id) h = h * 31 + ch;
        return tones[Mathf.PosMod(h, tones.Length)];
    }

    static string Initials(string name)
    {
        var parts = name.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? $"{parts[0][0]}{parts[1][0]}" : name.Length >= 2 ? name[..2] : name;
    }

    // ---------- Acma, kapama ----------

    public void OpenInventory(int page = 0)
    {
        if (_inv == null) return;
        _invPage = Mathf.Clamp(page, 0, 1);
        _inv.Visible = true;
        var inv = _d.State.Backpack;
        _invSel = Mathf.Clamp(_invSel, 0, System.Math.Max(0, inv.Slots - 1));
        if (_d.Bot == null) _d.Player.MoveOverride = Vector3.Zero; // panel acikken yurume ve ziplama yok
        _d.Player.LookLocked = true;
        Sfx.Play("place", null, -16f);
        RebuildInventory();
    }

    public void CloseInventory()
    {
        if (!InventoryOpen) return;
        _inv.Visible = false;
        if (_d.Bot == null) _d.Player.MoveOverride = null;
        _d.Player.LookLocked = false;
    }

    public void SetInventoryPage(int page)
    {
        _invPage = Mathf.Clamp(page, 0, 1);
        Sfx.Play("place", null, -18f);
        RebuildInventory();
    }

    // ---------- Oyuncunun eylemleri (bot da bunlari cagirir) ----------

    /// <summary>Secili yuvadaki esyanin birincil eylemi: yemekse ye, degilse kemere tak ya da cikar.</summary>
    public void InventoryAccept()
    {
        if (!InventoryOpen || _invPage != 0) return;
        var id = SelectedItem();
        if (id == null) { Sfx.Play("hit", null, -14f); return; }
        var food = _d.Data.Tool.Foods.FirstOrDefault(f => f.Id == id);
        if (food != null) _d.EatFood(food);
        else { bool on = _d.State.Belt.Toggle(id); Hint(on ? $"{ItemNames.Of(id)} kemerde" : $"{ItemNames.Of(id)} kemerden çıktı", 1f); }
        RebuildInventory();
    }

    /// <summary>1-8: secili esyayi o kemer yuvasina koyar (bos yuva secildiyse kemer yuvasini bosaltir).</summary>
    public void InventoryNumber(int slot)
    {
        if (!InventoryOpen || _invPage != 0 || slot < 0 || slot >= Kabarma.Sim.Belt.Slots) return;
        var id = SelectedItem();
        _d.State.Belt.Assign(slot, id);
        FlashBelt(slot);
        Sfx.Play("place", null, -12f);
        RebuildInventory();
    }

    /// <summary>Esyanin ilk durdugu yuvayi secer (oyuncu yon tuslariyla oraya gider).</summary>
    public bool InventorySelect(string id)
    {
        var c = _d.State.Backpack.Contents;
        for (int i = 0; i < c.Count; i++) if (c[i].id == id) { _invSel = i; RebuildInventory(); return true; }
        return false;
    }

    public void InventoryMove(int dx, int dy)
    {
        int n = _d.State.Backpack.Slots, cols = _invGrid.Columns;
        int x = _invSel % cols + dx, y = _invSel / cols + dy;
        int rows = (n + cols - 1) / cols;
        x = Mathf.PosMod(x, cols); y = Mathf.PosMod(y, rows);
        _invSel = Mathf.Clamp(y * cols + x, 0, n - 1);
        Sfx.Play("place", null, -24f);
        RebuildInventory();
    }

    string SelectedItem()
    {
        var c = _d.State.Backpack.Contents;
        return _invSel >= 0 && _invSel < c.Count ? c[_invSel].id : null;
    }

    // ---------- Kare ----------

    void UpdateInventory(Vector2 vp, float dt)
    {
        _inv.Position = (vp - _inv.Size) / 2f;
        _crosshair.Visible = false;
        _headTag.Visible = false;

        static bool JP(params string[] actions) => actions.Any(a => InputMap.HasAction(a) && Input.IsActionJustPressed(a));
        bool joyL = Input.IsJoyButtonPressed(0, JoyButton.LeftShoulder), joyR = Input.IsJoyButtonPressed(0, JoyButton.RightShoulder);
        bool page = JP("head_wheel", "build_next", "build_prev", "ui_page_up", "ui_page_down") || (joyL && !_joyL) || (joyR && !_joyR);
        _joyL = joyL; _joyR = joyR;
        if (page) SetInventoryPage(1 - _invPage);
        else if (_invPage == 0)
        {
            if (JP("ui_left", "move_left")) InventoryMove(-1, 0);
            else if (JP("ui_right", "move_right")) InventoryMove(1, 0);
            else if (JP("ui_up", "move_forward")) InventoryMove(0, -1);
            else if (JP("ui_down", "move_back")) InventoryMove(0, 1);
            else if (JP("ui_accept")) InventoryAccept();
        }
        _invRefreshT -= dt;
        if (_invRefreshT <= 0) { _invRefreshT = 0.5f; RebuildInventory(); } // yemek sureleri, uretimden gelen esya
    }

    void RebuildInventory()
    {
        if (_inv == null) return;
        var s = _d.State;
        _invTabBag.AddThemeColorOverride("font_color", _invPage == 0 ? Palette.Warning : new Color(Palette.Fog, 0.6f));
        _invTabDefter.AddThemeColorOverride("font_color", _invPage == 1 ? Palette.Warning : new Color(Palette.Fog, 0.6f));
        _invMoney.Text = $"{s.Credits} Kredi";
        _invBagPage.Visible = _invPage == 0;
        _defterPage.Visible = _invPage == 1;
        if (_invPage == 1)
        {
            RebuildDefter(); // Hud.Defter.cs
            _invFoot.Text = "Q / tekerlek / omuz tuşu: Çanta   ·   Tab / E / Esc: kapat";
            return;
        }

        var inv = s.Backpack;
        int n = inv.Slots;
        _invGrid.Columns = n <= 24 ? 6 : 8;
        while (_invCells.Count < n) { var c = new PanelContainer { CustomMinimumSize = new Vector2(CellW, CellH) }; _invGrid.AddChild(c); _invCells.Add(c); }
        _invSel = Mathf.Clamp(_invSel, 0, n - 1);
        for (int i = 0; i < _invCells.Count; i++)
        {
            var cell = _invCells[i];
            cell.Visible = i < n;
            foreach (var ch in cell.GetChildren()) ch.QueueFree();
            bool has = i < inv.Contents.Count;
            cell.AddThemeStyleboxOverride("panel", CellStyle(i == _invSel, has));
            if (has) cell.AddChild(ItemTile(inv.Contents[i].id, inv.Contents[i].count, s.Belt.Contains(inv.Contents[i].id)));
        }

        // Secilenin bilgisi
        foreach (var ch in _invInfo.GetChildren()) ch.QueueFree();
        var id = SelectedItem();
        if (id == null)
        {
            _invInfo.AddChild(L("Boş yuva", 22, Palette.Fog, true));
            _invInfo.AddChild(L($"Çanta {inv.UsedSlots}/{inv.Slots} yuva · yığın {inv.StackSize}", 16, Palette.Fog));
        }
        else
        {
            _invInfo.AddChild(L(ItemNames.Of(id), 26, Palette.OffWhite, true));
            _invInfo.AddChild(L($"Elde {inv.Count(id)}   ·   yığın {inv.StackSize}", 17, Palette.Fog, true));
            var food = s.Data.Tool.Foods.FirstOrDefault(f => f.Id == id);
            if (food != null)
            {
                _invInfo.AddChild(L($"+{food.MaxHp:0} can · +{food.Regen:0.#}/sn yenilenme · {food.Seconds / 60f:0.#} dk", 16, Palette.Plant, true));
                _invInfo.AddChild(L("Enter / A: ye", 17, Palette.Warning, true));
            }
            else _invInfo.AddChild(L(s.Belt.Contains(id) ? "Enter / A: kemerden çıkar" : "Enter / A: kemere tak", 17, Palette.Warning, true));
            _invInfo.AddChild(L("1–8: bu kemer yuvasına koy", 15, Palette.Fog, true));
            int bi = System.Array.IndexOf(s.Belt.Ids.ToArray(), id);
            if (bi >= 0) _invInfo.AddChild(L($"Kemerde: {bi + 1}. yuva (Alt+{bi + 1})", 15, Palette.MayaTeal, true));
        }
        _invInfo.AddChild(L($"Yumurta {s.Eggs}", 15, Palette.Fog));

        // Kemer
        for (int i = 0; i < _invBeltCells.Count; i++)
        {
            var cell = _invBeltCells[i];
            foreach (var ch in cell.GetChildren()) ch.QueueFree();
            var bid = s.Belt.At(i);
            cell.AddThemeStyleboxOverride("panel", CellStyle(bid != null && bid == id, bid != null));
            var col = new VBoxContainer();
            col.AddThemeConstantOverride("separation", -2);
            col.AddChild(L($"{i + 1}", 12, Palette.Warning, true));
            if (bid != null)
            {
                var bn = ItemNames.Of(bid);
                var nm = L(bn, bn.Length > 9 ? 10 : 12, Palette.OffWhite, true); nm.ClipText = true; nm.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis; nm.CustomMinimumSize = new Vector2(66, 0);
                col.AddChild(nm);
                col.AddChild(L(inv.Count(bid).ToString(), 14, Palette.Warning, true));
            }
            if (bid != null && ItemIcons.Has(bid)) { var ic = ItemIcons.Rect(bid, 52); ic.Modulate = new Color(1, 1, 1, 0.9f); cell.AddChild(ic); } // ikon yazinin arkasinda
            cell.AddChild(col);
        }

        // Yemek yuvalari (GDD 3.3: uc yuva, ayni yemek iki yuvaya konmaz)
        var active = s.Food.Active;
        for (int i = 0; i < _invFoodCells.Count; i++)
        {
            var cell = _invFoodCells[i];
            foreach (var ch in cell.GetChildren()) ch.QueueFree();
            bool has = i < active.Count;
            cell.AddThemeStyleboxOverride("panel", CellStyle(false, has));
            var col = new VBoxContainer();
            col.AddThemeConstantOverride("separation", -2);
            if (has)
            {
                var nm = L(active[i].food.Name, 13, Palette.OffWhite, true); nm.ClipText = true; nm.CustomMinimumSize = new Vector2(92, 0);
                col.AddChild(nm);
                col.AddChild(L($"{active[i].left / 60f:0.0} dk", 14, Palette.Plant, true));
                if (ItemIcons.Has(active[i].food.Id)) cell.AddChild(ItemIcons.Rect(active[i].food.Id, 52));
            }
            else col.AddChild(L("boş", 13, new Color(Palette.Fog, 0.5f), true));
            cell.AddChild(col);
        }

        _invFoot.Text = "Yön tuşları / d-pad: seç   ·   Enter / A: ye ya da kemere tak   ·   1–8: kemer yuvası   ·   Q: Defter   ·   Tab / E / Esc: kapat";
        IgnoreMouse(_inv);
    }

    Control ItemTile(string id, int count, bool onBelt)
    {
        var name = ItemNames.Of(id);
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 0);
        var row = new HBoxContainer();
        var icon = new PanelContainer { CustomMinimumSize = new Vector2(30, 30) };
        var isb = Box(ItemColor(id), 8); isb.ContentMarginLeft = isb.ContentMarginRight = 2; isb.ContentMarginTop = isb.ContentMarginBottom = 0;
        icon.AddThemeStyleboxOverride("panel", isb);
        var ini = L(Initials(name), 13, Palette.NightDeep, true); ini.HorizontalAlignment = HorizontalAlignment.Center; ini.VerticalAlignment = VerticalAlignment.Center;
        icon.AddChild(ini);
        if (ItemIcons.Has(id)) { icon.QueueFree(); row.AddChild(ItemIcons.Rect(id, 44)); } // SANAT-SES 8.1 ikonu; yoksa renkli harf karesi
        else row.AddChild(icon);
        var cnt = L(count.ToString(), 18, Palette.Warning, true);
        cnt.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        cnt.HorizontalAlignment = HorizontalAlignment.Right;
        row.AddChild(cnt);
        v.AddChild(row);
        var nm = L(name, name.Length > 11 ? 10 : 12, Palette.OffWhite, true); // uzun ad kucuk yazilir, sigmazsa "…"
        nm.ClipText = true;
        nm.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        nm.CustomMinimumSize = new Vector2(CellW - 12, 0);
        v.AddChild(nm);
        if (onBelt) v.AddChild(L("kemerde", 10, Palette.MayaTeal, true));
        return v;
    }
}
