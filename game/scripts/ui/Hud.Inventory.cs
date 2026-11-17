using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;
using Kabarma.Sim;
using T = Kabarma.Game.UiTheme;

namespace Kabarma.Game;

/// <summary>
/// Envanter (Tab; GDD 5, 3.3; SANAT-SES 8.1 ve 8.2; M1 karari 20, Rust yerlesimi): solda kusam yuvalari (M1'de kusam yok:
/// bos ve kilitli), yemek yuvalari, Kredi ve yumurta; ortada 20 yuvali canta izgarasi ve altinda kemer; sagda secili
/// esyanin ayrintisi ya da acik istasyonun / rampanin paneli (Hud.Menu.cs). Ust ortada sekmeler: Canta ve Defter.
/// Fare: tikla sec, surukle-birak (cantadan kemere, kemer icinde, kemerden disari), Shift+tik ya da sag tik hizli aktarim.
/// Klavye ve kol: yon tuslari / d-pad secer, Enter / A ye ya da kemere tak, 1-8 secileni o kemer yuvasina koyar,
/// Q / omuz tuslari sayfa degistirir, Tab / E / Esc / X kapatir. Ikonlar yer tutucu (esya tonunda kare ve adi).
/// </summary>
public partial class Hud
{
    bool _invOpen;
    int _invSel;
    int _invPage; // 0 Canta, 1 Defter
    int _invFocusBelt = -1;
    float _invRefreshT;
    bool _joyL, _joyR;
    PanelContainer _defterPanel;
    VBoxContainer _defterPage;

    public bool InventoryOpen => _invOpen;
    /// <summary>Acik sayfa: 0 Canta, 1 Defter.</summary>
    public int InventoryPage => _invPage;
    /// <summary>Secili canta yuvasi.</summary>
    public int InventorySelected => _invSel;

    /// <summary>Canta izgarasinin sutun sayisi: yuva sayisini tam bolen ilk secenek (ui.json; 20 -> 5).</summary>
    public int InventoryColumns
    {
        get => UiScale.BagColumns(T.Data, _d.State.Backpack.Slots);
    }

    void BuildInventory(Control root)
    {
        _defterPanel = new PanelContainer { Name = "DefterPage", Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore, Theme = T.Theme };
        _defterPage = new VBoxContainer();
        _defterPanel.AddChild(_defterPage);
        root.AddChild(_defterPanel);
        root.MoveChild(_report, root.GetChildCount() - 1); // rapor her seyin ustunde
    }

    // ---------- Acma, kapama ----------

    public void OpenInventory(int page = 0)
    {
        if (_defterPanel == null) return;
        bool was = _invOpen;
        _invOpen = true;
        _invPage = StationOpen ? 0 : Mathf.Clamp(page, 0, 1);
        var inv = _d.State.Backpack;
        _invSel = Mathf.Clamp(_invSel, 0, Math.Max(0, inv.Slots - 1));
        _invFocusBelt = -1;
        _drag = null;
        if (_d.Bot == null)
        {
            _d.Player.MoveOverride = Vector3.Zero; // panel acikken yurume ve ziplama yok
            Input.MouseMode = Input.MouseModeEnum.Visible; // surukle-birak icin imlec
        }
        _d.Player.LookLocked = true;
        if (!was) Sfx.Play("place", null, -16f);
        RefreshInventoryPage();
    }

    public void CloseInventory()
    {
        if (_menu != null) _menu.Visible = false; // acik istasyon paneli de kapanir
        if (!InventoryOpen) return;
        _invOpen = false;
        _drag = null;
        _defterPanel.Visible = false;
        if (_d.Bot == null)
        {
            _d.Player.MoveOverride = null;
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }
        _d.Player.LookLocked = false;
    }

    public void SetInventoryPage(int page)
    {
        if (StationOpen) page = 0;
        _invPage = Mathf.Clamp(page, 0, 1);
        Sfx.Play("place", null, -18f);
        RefreshInventoryPage();
    }

    void RefreshInventoryPage()
    {
        _defterPanel.Visible = _invOpen && _invPage == 1;
        if (_defterPanel.Visible) RebuildDefter(); // Hud.Defter.cs
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
        else ToggleBelt(id);
    }

    void ToggleBelt(string id)
    {
        bool on = _d.State.Belt.Toggle(id);
        Hint(on ? $"{ItemNames.Of(id)} kemerde" : $"{ItemNames.Of(id)} kemerden çıktı", 1f);
        Sfx.Play("place", null, -12f);
    }

    /// <summary>1-8: secili esyayi o kemer yuvasina koyar (bos yuva secildiyse kemer yuvasini bosaltir).</summary>
    public void InventoryNumber(int slot)
    {
        if (!InventoryOpen || _invPage != 0 || slot < 0 || slot >= Kabarma.Sim.Belt.Slots) return;
        var id = SelectedItem();
        _d.State.Belt.Assign(slot, id);
        FlashBelt(slot);
        Sfx.Play("place", null, -12f);
    }

    /// <summary>Esyanin ilk durdugu yuvayi secer (oyuncu yon tuslariyla oraya gider).</summary>
    public bool InventorySelect(string id)
    {
        var c = _d.State.Backpack.Contents;
        for (int i = 0; i < c.Count; i++) if (c[i].id == id) { _invSel = i; _invFocusBelt = -1; return true; }
        return false;
    }

    public void InventoryMove(int dx, int dy)
    {
        int n = _d.State.Backpack.Slots, cols = InventoryColumns;
        int x = _invSel % cols + dx, y = _invSel / cols + dy;
        int rows = (n + cols - 1) / cols;
        x = Mathf.PosMod(x, cols); y = Mathf.PosMod(y, rows);
        _invSel = Mathf.Clamp(y * cols + x, 0, n - 1);
        _invFocusBelt = -1;
        Sfx.Play("place", null, -24f);
    }

    /// <summary>
    /// Hizli aktarim (Shift+tik, sag tik): cantadaki esya kemere girer ya da cikar; kemerdeki esya kemerden cikar.
    /// Acik bir kap (Sandik) birlesince cantadan kaba aktarim buraya eklenir.
    /// </summary>
    public void InventoryQuickMove(bool fromBelt, int index)
    {
        if (fromBelt)
        {
            if (_d.State.Belt.At(index) is string bid) { _d.State.Belt.Assign(index, null); Hint($"{ItemNames.Of(bid)} kemerden çıktı", 1f); Sfx.Play("place", null, -12f); }
            return;
        }
        var c = _d.State.Backpack.Contents;
        if (index >= 0 && index < c.Count) ToggleBelt(c[index].id);
    }

    string SelectedItem()
    {
        var c = _d.State.Backpack.Contents;
        return _invSel >= 0 && _invSel < c.Count ? c[_invSel].id : null;
    }

    // ---------- Kare ----------

    void UpdateInventoryFrame(float dt)
    {
        if (!InventoryOpen) return;
        // Baska bir sey fareyi kilitlediyse (odak geri geldi) imlec geri gelsin.
        if (_d.Bot == null && Input.MouseMode != Input.MouseModeEnum.Visible) Input.MouseMode = Input.MouseModeEnum.Visible;

        static bool JP(params string[] actions) => actions.Any(a => InputMap.HasAction(a) && Input.IsActionJustPressed(a));
        bool joyL = Input.IsJoyButtonPressed(0, JoyButton.LeftShoulder), joyR = Input.IsJoyButtonPressed(0, JoyButton.RightShoulder);
        bool page = JP("head_wheel", "build_next", "build_prev", "ui_page_up", "ui_page_down") || (joyL && !_joyL) || (joyR && !_joyR);
        _joyL = joyL; _joyR = joyR;
        if (StationOpen)
        {
            int n = _menuItems.Count;
            if (n > 0)
            {
                if (JP("ui_down", "move_back")) _menuRow = (_menuRow + 1) % n;
                else if (JP("ui_up", "move_forward")) _menuRow = (_menuRow + n - 1) % n;
                else if (JP("ui_accept")) ActivateMenu(_menuRow);
            }
        }
        else if (page) SetInventoryPage(1 - _invPage);
        else if (_invPage == 0)
        {
            if (JP("ui_left", "move_left")) InventoryMove(-1, 0);
            else if (JP("ui_right", "move_right")) InventoryMove(1, 0);
            else if (JP("ui_up", "move_forward")) InventoryMove(0, -1);
            else if (JP("ui_down", "move_back")) InventoryMove(0, 1);
            else if (JP("ui_accept")) InventoryAccept();
        }
        _invRefreshT -= dt;
        if (_invRefreshT <= 0) { _invRefreshT = 0.5f; if (_invPage == 1) RebuildDefter(); } // gun saati, ilerleme
        if (_defterPanel.Visible)
        {
            _o = TopFrame;
            var at = At(0, 0);
            _defterPanel.Position = new Vector2(Mathf.Round((ScreenSize.X - _defterPanel.Size.X) / 2f), at.Y + T.Px(84));
        }
    }

    // ---------- Geometri (tiklama ve sinama ayni hesabi kullanir) ----------

    float BagX0 => (T.DesignW - ((InventoryColumns - 1) * T.Data.Inventory.Pitch + T.Data.Inventory.Slot)) / 2f;
    float BagY0
    {
        get
        {
            var iv = T.Data.Inventory;
            int rows = (_d.State.Backpack.Slots + InventoryColumns - 1) / InventoryColumns;
            return T.Data.Belt.Y - 31 - ((rows - 1) * iv.Pitch + iv.Slot);
        }
    }
    float BagX(int i) => BagX0 + i % InventoryColumns * T.Data.Inventory.Pitch;
    float BagY(int i) => BagY0 + i / InventoryColumns * T.Data.Inventory.Pitch;
    float RightX => Math.Max(1040f, BagX0 + InventoryColumns * T.Data.Inventory.Pitch + 18);
    const float LeftX = 70, ColW = 475;

    /// <summary>Canta yuvasinin ekrandaki alani (sinama ve tiklama).</summary>
    public Rect2 BagSlotRect(int i)
    {
        _o = CanvasFrame;
        float s = T.Data.Inventory.Slot;
        return Area(BagX(i), BagY(i), s, s);
    }

    Rect2 TabRect(int page) { _o = TopFrame; return Area(T.DesignW / 2f - 160 + page * 165, 0, 155, 54); }

    /// <summary>Isaretcinin altindaki yuva: (kemer mi, sira); yoksa null.</summary>
    (bool belt, int index)? SlotAt(Vector2 screen)
    {
        if (!InventoryOpen || _invPage != 0) return null;
        for (int i = 0; i < _d.State.Backpack.Slots; i++) if (BagSlotRect(i).HasPoint(screen)) return (false, i);
        for (int i = 0; i < Kabarma.Sim.Belt.Slots; i++) if (BeltSlotRect(i).HasPoint(screen)) return (true, i);
        return null;
    }

    // ---------- Cizim ----------

    void PaintInventory()
    {
        // Sekmeler (ust orta)
        _o = TopFrame;
        for (int pg = 0; pg < 2; pg++)
        {
            Button(pg == 0 ? "ÇANTA" : "DEFTER", T.DesignW / 2f - 160 + pg * 165, 0, 155, 54, 28, null, !StationOpen || pg == 0, false, _invPage == pg);
        }
        string pk = _padLast ? "LB / RB" : "Q";
        Text($"{pk}: SAYFA", T.DesignW / 2f + 180, 18, T.Data.Type.Caption + 1, T.Muted, true, true);

        _o = CanvasFrame;
        string keys = _padLast
            ? "D-PAD: SEÇ   ·   A: KULLAN   ·   LB / RB: SAYFA   ·   X / B: KAPAT"
            : "YÖN TUŞLARI: SEÇ   ·   ENTER: KULLAN   ·   1–8: KEMER YUVASI   ·   SÜRÜKLE: KEMERE   ·   SHIFT+TIK: HIZLI AKTAR   ·   TAB / E / ESC: KAPAT";
        if (StationOpen) keys = _padLast ? "D-PAD: SATIR   ·   A: SIRAYA EKLE   ·   X / B: KAPAT" : "1–9 YA DA TIK: SIRAYA EKLE   ·   SÜRÜKLE: KEMERE   ·   E / ESC: KAPAT";
        CentreText(keys, T.DesignH - 20, T.Data.Type.Caption, T.A(T.Ink, 0.7f), true, true);
        if (_invPage != 0) return;

        PaintGear();
        PaintBag();
        if (StationOpen) PaintStation(); // Hud.Menu.cs
        else PaintDetail();
    }

    /// <summary>Sol sutun: kusam (M1'de yok: bos ve kilitli), yemek yuvalari, Kredi ve yumurta.</summary>
    void PaintGear()
    {
        var iv = T.Data.Inventory;
        float top = BagY0;
        Section("Kuşam", LeftX, top - 34);
        for (int i = 0; i < iv.GearSlots; i++)
        {
            float x = LeftX + i * (ColW + 5) / iv.GearSlots, s = (ColW + 5) / iv.GearSlots - 5;
            Rect(x, top, s, s, T.Locked);
            Lock(x + s / 2f, top + s / 2f + 3, s * 0.26f, T.A(T.Muted, 0.55f));
        }
        Text("M1'DE KUŞAM YOK", LeftX, top + 80, T.Data.Type.Caption, T.A(T.Muted, 0.8f), true, true);

        // Yemek yuvalari (GDD 3.3: uc yuva, ayni yemek iki yuvaya konmaz)
        float fy = top + 112;
        Section("Yemek", LeftX, fy);
        var active = _d.State.Food.Active;
        int slots = FoodBuffs.Slots;
        float fw = (ColW - (slots - 1) * 8) / slots;
        for (int i = 0; i < slots; i++)
        {
            float x = LeftX + i * (fw + 8), y = fy + 36;
            Rect(x, y, fw, 60, T.Panel);
            if (i < active.Count)
            {
                var (food, left) = active[i];
                Text(Fit(T.Upper(food.Name), fw - 14, 15, true, true), x + 7, y + 6, 15, T.Ink, true, true);
                Text($"{left / 60f:0.0} DK", x + 7, y + 26, 20, T.Green, true, true);
                float frac = Mathf.Clamp(left / Math.Max(1f, food.Seconds), 0, 1);
                Rect(x, y + 56, fw * frac, 4, T.Green);
            }
            else TextIn("BOŞ", x, y + 20, fw, 15, T.A(T.Muted, 0.6f), HorizontalAlignment.Center, true, true);
        }

        // Kredi ve yumurta
        float sy = BagY0 + 3 * T.Data.Inventory.Pitch;
        float bw = (ColW - 15) / 2f;
        var s0 = _d.State;
        Rect(LeftX, sy, bw, T.Data.Inventory.Slot, T.Panel);
        Text("KREDİ", LeftX + 10, sy + 7, T.Data.Type.Caption, T.Muted, true, true);
        Text(s0.Credits.ToString("N0", CultureInfo.GetCultureInfo("tr-TR")), LeftX + 10, sy + 28, 34, T.Short, true, true);
        Rect(LeftX + bw + 15, sy, bw, T.Data.Inventory.Slot, T.Panel);
        Text("YUMURTA", LeftX + bw + 25, sy + 7, T.Data.Type.Caption, T.Muted, true, true);
        Text(s0.Eggs.ToString(CultureInfo.InvariantCulture), LeftX + bw + 25, sy + 28, 34, T.Ink, true, true);
    }

    /// <summary>Orta: canta izgarasi (secili yuva mavi), ustunde baslik ve doluluk.</summary>
    void PaintBag()
    {
        _o = CanvasFrame;
        var iv = T.Data.Inventory;
        var inv = _d.State.Backpack;
        Section($"Çanta  {inv.UsedSlots}/{inv.Slots}", BagX0, BagY0 - 34);
        for (int i = 0; i < inv.Slots; i++)
        {
            bool has = i < inv.Contents.Count;
            string id = has ? inv.Contents[i].id : null;
            int count = has ? inv.Contents[i].count : 0;
            bool dragged = _drag is { Moving: true, FromBelt: false } d && d.Index == i;
            SlotBox(BagX(i), BagY(i), iv.Slot, id, count, true, i == _invSel && _invFocusBelt < 0, 0, ItemCondition(id), StarsAt(i), dragged ? 0.35f : 1f);
            if (has && _d.State.Belt.Contains(id))
            {
                int bi = _d.State.Belt.Ids.ToList().IndexOf(id);
                // Kemerdeki esya: sol ustte kemer yuvasinin numarasi
                Rect(BagX(i) + 4, BagY(i) + 4, 16, 16, T.A(T.Selected, 0.9f));
                TextIn((bi + 1).ToString(CultureInfo.InvariantCulture), BagX(i) + 4, BagY(i) + 4, 16, 13, T.Ivory, HorizontalAlignment.Center, true, true);
            }
        }
    }

    /// <summary>Esyanin turu (ayrinti kutusunda bir satir).</summary>
    string ItemKind(string id)
    {
        var tool = _d.Data.Tool;
        if (tool.Foods.Any(f => f.Id == id)) return "Yemek. Yiyince üç yemek yuvasından birine girer ve güçlendirir.";
        if (id == tool.Nailgun.Ammo) return "Çivi Tabancası'nın cephanesi. Kabarma gecesi harcanmaz.";
        if (id.EndsWith("_seed")) return "Tohum. Toprak başlığıyla sürülmüş kareye ekilir.";
        if (tool.Recipes.Any(r => r.Id == id)) return "Üretim ürünü. Kargo Rampası'nda satılabilir.";
        return "Kaynak. İstasyonlarda üretimde kullanılır.";
    }

    /// <summary>Sag sutun: secili esyanin ayrintisi (ad, tur, bilgi satirlari, eylemler).</summary>
    void PaintDetail()
    {
        _o = CanvasFrame;
        float x = RightX, y = BagY0 - 34;
        var s = _d.State;
        var inv = s.Backpack;
        var id = SelectedItem();
        if (id == null)
        {
            Section("Boş yuva", x, y);
            Rect(x, y + 34, ColW, 60, T.Panel);
            Text($"Çanta {inv.UsedSlots}/{inv.Slots} yuva dolu. Yığın en çok {inv.StackSize}.", x + 14, y + 52, 17, T.Ink);
            return;
        }
        Section(Fit(T.Upper(ItemNames.Of(id)), ColW, T.Data.Type.Section, true, true), x, y);
        // Tur ve ikon
        Rect(x, y + 34, ColW, 86, T.Panel);
        Paragraph(ItemKind(id), x + 14, y + 46, ColW - 120, 16, T.Ink, 3);
        ItemIcon(id, x + ColW - 88, y + 41, 74, 72);
        for (int st = 0, n = StarsAt(_invSel); st < n; st++) Star(x + ColW - 22 - st * 13, y + 50, 6, T.Star);

        // Bilgi satirlari (sol) ve eylemler (sag)
        float infoW = 300, actX = x + infoW + 10, actW = ColW - infoW - 10, ry = y + 126;
        var rows = new List<(string label, string value, bool good)>
        {
            ("ELDE", inv.Count(id).ToString(CultureInfo.InvariantCulture), true),
            ("YIĞIN", inv.StackSize.ToString(CultureInfo.InvariantCulture), true),
        };
        int bi = s.Belt.Ids.ToList().IndexOf(id);
        rows.Add(("KEMERDE", bi >= 0 ? $"{bi + 1}. YUVA" : "YOK", bi >= 0));
        var food = _d.Data.Tool.Foods.FirstOrDefault(f => f.Id == id);
        if (food != null)
        {
            rows.Add(("CAN", $"+{food.MaxHp:0}", true));
            rows.Add(("YENİLENME", $"+{food.Regen:0.#}/SN", true));
            rows.Add(("SÜRE", $"{food.Seconds / 60f:0.#} DK", true));
        }
        Rect(x, ry, infoW, 26 + rows.Count * 22 + 6, T.Panel);
        Rect(x, ry, infoW, 26, T.Panel);
        Text("BİLGİ", x + 8, ry + 4, 17, T.Ink, true, true);
        for (int i = 0; i < rows.Count; i++)
        {
            Text(rows[i].label, x + 10, ry + 30 + i * 22, 15, T.Ink, true, true);
            TextIn(rows[i].value, x, ry + 30 + i * 22, infoW - 10, 15, rows[i].good ? T.Green : T.Short, HorizontalAlignment.Right, true, true);
        }
        Rect(actX, ry, actW, 26, T.Panel);
        Text("EYLEMLER", actX + 8, ry + 4, 17, T.Ink, true, true);
        int k = 0;
        foreach (var (label, _) in DetailActions(id)) { Button(label, actX, ry + 30 + k * 40, actW, 36, 19); k++; }
        Paragraph("1–8: bu kemer yuvasına koy.  Kemerdeki yuva: sürükle ya da Shift+tık ile çıkar.", x, ry + 26 + Math.Max(rows.Count * 22 + 12, k * 40 + 8) + 8, ColW, 14, T.A(T.Ink, 0.7f), 2);
    }

    /// <summary>Secili esyanin eylemleri (dugme etiketi, yaptigi). Tiklama ve cizim ayni listeyi kullanir.</summary>
    List<(string label, Action act)> DetailActions(string id)
    {
        var l = new List<(string, Action)>();
        if (id == null) return l;
        var food = _d.Data.Tool.Foods.FirstOrDefault(f => f.Id == id);
        if (food != null) l.Add(("YE", () => _d.EatFood(food)));
        bool on = _d.State.Belt.Contains(id);
        l.Add((on ? "KEMERDEN ÇIKAR" : "KEMERE TAK", () => ToggleBelt(id)));
        return l;
    }

    Rect2 DetailActionRect(int k)
    {
        _o = CanvasFrame;
        float x = RightX, infoW = 300;
        return Area(x + infoW + 10, BagY0 - 34 + 126 + 30 + k * 40, ColW - infoW - 10, 36);
    }

    /// <summary>Surukleme hayaleti ve isaretcinin altindaki esyanin adi.</summary>
    void PaintDragAndTooltip()
    {
        if (!_pointerSeen || _invPage != 0) return;
        _o = CanvasFrame;
        var p = DesignPointer;
        if (_drag is { Moving: true } drag)
        {
            float s = T.Data.Inventory.Slot;
            ItemIcon(drag.Id, p.X - s / 2f, p.Y - s / 2f, s, s, 0.85f);
            return;
        }
        if (SlotAt(_pointer) is not (bool belt, int index)) return;
        string id = belt ? _d.State.Belt.At(index) : index < _d.State.Backpack.Contents.Count ? _d.State.Backpack.Contents[index].id : null;
        if (id == null) return;
        string name = ItemNames.Of(id);
        float w = Measure(name, 18) + 24;
        float x = Math.Min(p.X + 16, T.DesignW - 10 - w), y = Math.Min(p.Y + 20, T.DesignH - 36);
        Rect(x, y, w, 31, T.Tooltip);
        Text(name, x + 12, y + 5, 18, T.Ink);
    }

    // ---------- Fare (Hud.Pointer.cs cagirir) ----------

    sealed class DragState { public bool FromBelt; public int Index; public string Id; public Vector2 Origin; public bool Moving; }
    DragState _drag;

    void InventoryPress(InputEventMouseButton mb)
    {
        var at = mb.Position;
        // Once yuvalar (asil hedef), sonra panel dugmeleri, en son sekmeler.
        if (SlotAt(at) is not (bool belt, int index))
        {
            if (mb.ButtonIndex != MouseButton.Left) return;
            if (_invPage == 0 && StationOpen)
                for (int i = 0; i < _menuItems.Count; i++)
                    if (MenuRowRect(i).HasPoint(at)) { _menuRow = i; ActivateMenu(i); return; }
            if (_invPage == 0 && !StationOpen && SelectedItem() is string sid)
            {
                var acts = DetailActions(sid);
                for (int k = 0; k < acts.Count; k++) if (DetailActionRect(k).HasPoint(at)) { acts[k].act(); return; }
            }
            for (int pg = 0; pg < 2; pg++)
                if (TabRect(pg).HasPoint(at)) { if (!StationOpen) SetInventoryPage(pg); return; }
            return;
        }
        string id = belt ? _d.State.Belt.At(index) : index < _d.State.Backpack.Contents.Count ? _d.State.Backpack.Contents[index].id : null;
        if (mb.ButtonIndex == MouseButton.Right || (mb.ButtonIndex == MouseButton.Left && mb.ShiftPressed))
        {
            if (id != null) InventoryQuickMove(belt, index);
            return;
        }
        if (mb.ButtonIndex != MouseButton.Left) return;
        if (belt)
        {
            _invFocusBelt = index;
            if (id != null) InventorySelect(id);
            _invFocusBelt = index;
        }
        else { _invSel = index; _invFocusBelt = -1; }
        Sfx.Play("place", null, -24f);
        if (id != null) _drag = new DragState { FromBelt = belt, Index = index, Id = id, Origin = at };
    }

    void InventoryMotion(InputEventMouseMotion mm)
    {
        if (_drag is { Moving: false } d && d.Origin.DistanceTo(mm.Position) > T.Pxf(T.Data.Inventory.DragStart)) d.Moving = true;
    }

    /// <summary>
    /// Birakma: kemer yuvasina birakilan esya o yuvaya girer (kemer icinde yer degistirir); kemerden kemer disina
    /// birakilan esya kemerden cikar. Canta icinde siralama yok: Sim cantasi sikisik liste (konumlu yuva ekonomi akisinda).
    /// </summary>
    void InventoryRelease(InputEventMouseButton mb)
    {
        if (mb.ButtonIndex != MouseButton.Left || _drag is not DragState d) return;
        _drag = null;
        if (!d.Moving) return;
        var belt = _d.State.Belt;
        var target = SlotAt(mb.Position);
        if (target is (true, int k))
        {
            if (d.FromBelt)
            {
                if (k == d.Index) return;
                string other = belt.At(k);
                belt.Assign(k, d.Id);
                belt.Assign(d.Index, other);
            }
            else belt.Assign(k, d.Id);
            _invFocusBelt = k;
            FlashBelt(k);
            Sfx.Play("place", null, -10f);
            return;
        }
        if (d.FromBelt) { belt.Assign(d.Index, null); _invFocusBelt = -1; Sfx.Play("place", null, -14f); Hint($"{ItemNames.Of(d.Id)} kemerden çıktı", 1f); }
    }
}
