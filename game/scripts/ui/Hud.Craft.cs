using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;
using Kabarma.Sim;
using T = Kabarma.Game.UiTheme;

namespace Kabarma.Game;

/// <summary>
/// Uretim paneli (GDD 5; SANAT-SES 8.1 Rust dili; URETIM 6 M1 karari 23). Envanterin ucuncu sayfasi (Tab, sonra UretIM
/// sekmesi ya da Q). Solda arama ve kategori, ortada butun tarifler (istasyon tarifi uzaktaysa satir "TEZGAH GEREKIR"
/// der, eksik malzeme kirmizi), sagda secili tarifin malzemesi (eksigin kaynagi ve "Pusulada goster"), suresi, adedi ve
/// URET dugmesi. Istasyonun E menusu ayni paneli o istasyonun tarifleriyle acar (satirlar veri sirasiyla).
/// Satirlar menu yolundan da secilir: 1-9 satiri uretir, yon tuslari / d-pad satiri tasir, Enter / A secilen adedi uretir
/// (Director.ReadInput; bot ve girdi akisi bunlari kullanir). Kuyruk sag altta (envanter kapaliyken de).
/// </summary>
public partial class Hud
{
    public const int CraftPage = 2;
    string _craftStation;              // null: butun tarifler; dolu: istasyon menusu (E)
    string _craftCategory = "all";
    string _craftSearch = "";
    bool _searchFocus;
    int _craftSel, _craftCount = 1, _craftScroll;

    /// <summary>Uretim sayfasi acik.</summary>
    public bool CraftOpen => InventoryOpen && _invPage == CraftPage;
    /// <summary>Arama kutusuna yaziliyor: oyun eylemleri yutulur (Director.ReadInput).</summary>
    public bool CraftTyping => CraftOpen && _searchFocus;
    /// <summary>Panelin istasyonu (E ile acildiysa); yoksa null.</summary>
    public string CraftStation => CraftOpen ? _craftStation : null;
    /// <summary>Sinama: gorunen satirlarin tarif kimlikleri ve secili satir.</summary>
    public IReadOnlyList<string> CraftRowIds => CraftRows().Select(r => r.Id).ToList();
    public int CraftSelected => _craftSel;
    /// <summary>Sinama / goruntu: satiri tarif kimligiyle secer (gorunurse).</summary>
    public void CraftSelect(string id)
    {
        int i = CraftRowIds.ToList().IndexOf(id);
        if (i < 0) return;
        CraftCursor(i);
        _d.SyncMenuCursor(i); // menu imleci her karede satiri surer (Director.ReadInput)
    }

    /// <summary>Uretim panelini acar; station verilirse o istasyonun tarifleriyle (E menusu).</summary>
    public void OpenCraft(string station = null)
    {
        _craftStation = station;
        _craftSel = 0; _craftCount = 1; _craftScroll = 0;
        _searchFocus = false;
        if (station != null) { _craftCategory = "all"; _craftSearch = ""; }
        OpenInventory(CraftPage);
    }

    /// <summary>Gorunen tarifler: istasyon kipinde o istasyonunkiler (veri sirasi); yoksa kategori ve aramaya gore hepsi.</summary>
    List<RecipeDef> CraftRows()
    {
        var all = _d.Data.Tool.Recipes;
        if (_craftStation != null) return all.Where(r => r.Station == _craftStation).ToList();
        IEnumerable<RecipeDef> q = all.Where(r => r.Station != "paketleme"); // kasa Paketleme'nin kendi menusunden
        if (_craftCategory == ResearchCategory) q = q.Where(ResearchRow); // zincir: arastirilabilir satirlar (Hud.Craft.Research.cs)
        else
        if (_craftCategory != "all") q = q.Where(r => r.Category == _craftCategory);
        if (_craftSearch.Length > 0)
        {
            var tr = CultureInfo.GetCultureInfo("tr-TR");
            string s = _craftSearch.ToLower(tr);
            q = q.Where(r => r.Name.ToLower(tr).Contains(s) || ItemNames.Of(r.Station ?? "").ToLower(tr).Contains(s));
        }
        return q.ToList();
    }

    bool Unlocked(RecipeDef r) => CraftRules.IsHand(r) ? _d.State.Craft.IsUnlocked(r) : !_d.State.Stations.TryGetValue(r.Station, out var sq) || sq.IsUnlocked(r);

    /// <summary>Menu yolu: satir i'yi secer ve secili adedi uretir (1-9, Enter / A).</summary>
    void CraftActivate(int i)
    {
        var rows = CraftRows();
        if (i < 0 || i >= rows.Count) return;
        if (i != _craftSel) { _craftSel = i; _craftCount = 1; }
        _d.Craft(rows[i], _craftCount);
    }

    void CraftCursor(int sel)
    {
        int n = CraftRows().Count;
        if (n == 0) return;
        sel = Mathf.Clamp(sel, 0, n - 1);
        if (sel != _craftSel) { _craftSel = sel; _craftCount = 1; }
    }

    void CraftCountStep(int d)
    {
        var rows = CraftRows();
        if (_craftSel < 0 || _craftSel >= rows.Count) return;
        int max = Math.Max(1, Math.Min(_d.Data.Tool.CraftCountMax, CraftRules.Affordable(rows[_craftSel], _d.StoreFor(rows[_craftSel]), _d.Data.Tool.CraftCountMax)));
        _craftCount = Mathf.Clamp(_craftCount + d, 1, max);
        Sfx.Play("place", null, -24f);
    }

    /// <summary>Arama kutusu: harf ekler, Backspace siler, Enter / Esc birakir.</summary>
    bool CraftKey(InputEventKey k)
    {
        if (!CraftTyping || !k.Pressed) return false;
        if (k.Keycode is Key.Escape or Key.Enter or Key.KpEnter) { _searchFocus = false; return true; }
        if (k.Keycode == Key.Backspace) { if (_craftSearch.Length > 0) _craftSearch = _craftSearch[..^1]; _craftSel = 0; return true; }
        if (k.Unicode >= 32 && _craftSearch.Length < 24) { _craftSearch += char.ConvertFromUtf32((int)k.Unicode); _craftSel = 0; _craftScroll = 0; }
        return true;
    }

    // ---------- Yerlesim (tasarim pikseli; cizim ve tiklama ayni hesabi kullanir) ----------

    const float CLeftX = 70, CLeftW = 290, CMidX = 380, CMidW = 570, CRightX = 970, CRightW = 560, CTop = 96, CBottom = 770, CRowH = 58, CRowGap = 4;
    int CraftVisibleRows => (int)((CBottom - (CTop + 38)) / (CRowH + CRowGap));
    Rect2 CraftRowRect(int i) { _o = CanvasFrame; return Area(CMidX, CTop + 38 + (i - _craftScroll) * (CRowH + CRowGap), CMidW, CRowH); }
    Rect2 CraftSearchRect() { _o = CanvasFrame; return Area(CLeftX, CTop + 38, CLeftW, 40); }
    Rect2 CraftCategoryRect(int i) { _o = CanvasFrame; return Area(CLeftX, CTop + 96 + i * 45, CLeftW, 40); }

    // ---------- Cizim ----------

    void PaintCraft()
    {
        _o = CanvasFrame;
        var rows = CraftRows();
        if (_craftSel >= rows.Count) _craftSel = Math.Max(0, rows.Count - 1);
        int vis = CraftVisibleRows;
        if (_craftSel < _craftScroll) _craftScroll = _craftSel;
        if (_craftSel >= _craftScroll + vis) _craftScroll = _craftSel - vis + 1;
        var near = _d.NearStations();

        // Sol: arama ve kategori (istasyon kipinde istasyonun adi ve "butun tarifler")
        if (_craftStation != null)
        {
            Section(ItemNames.Of(_craftStation), CLeftX, CTop);
            var st = _d.NearStation(_craftStation);
            Paragraph(st != null ? "İstasyonun tarifleri. Bu istasyonun yanında üretilir; malzeme çantadan, yakıt varsa çantadan." : "İstasyondan uzaklaştın: tarifler yanına dönünce üretilir.", CLeftX, CTop + 40, CLeftW, 15, T.A(T.Ink, 0.8f), 4);
            Button("BÜTÜN TARİFLER", CLeftX, CTop + 120, CLeftW, 40, 18);
            PaintStationExtras(); // zincir: Masa, guc, kuyruk (Hud.Craft.Research.cs)
        }
        else
        {
            Section("Üretim", CLeftX, CTop);
            var sr = CraftSearchRect();
            _p.DrawRect(sr, _searchFocus ? T.Selected : T.Panel);
            if (_searchFocus) Frame(CLeftX, CTop + 38, CLeftW, 40, T.SelectedEdge, 1.5f);
            string shown = _craftSearch.Length > 0 ? _craftSearch + (_searchFocus ? "_" : "") : _searchFocus ? "_" : "ARA…";
            Text(shown, CLeftX + 12, CTop + 47, 18, _craftSearch.Length > 0 || _searchFocus ? T.Ink : T.Muted, _craftSearch.Length == 0, true);
            var cats = _d.Data.Tool.CraftCategories;
            for (int i = 0; i < cats.Count; i++)
            {
                int n = cats[i].Id == ResearchCategory ? _d.Data.Tool.Recipes.Count(ResearchRow) : cats[i].Id == "all" ? _d.Data.Tool.Recipes.Count(r => r.Station != "paketleme") : _d.Data.Tool.Recipes.Count(r => r.Category == cats[i].Id && r.Station != "paketleme");
                Button($"{T.Upper(cats[i].Name)}  ({n})", CLeftX, CTop + 96 + i * 45, CLeftW, 40, 17, null, true, true, _craftCategory == cats[i].Id);
            }
            PaintVeri(CTop + 96 + cats.Count * 45 + 8); // zincir: Veri ve Ocak basamagi
        }

        // Orta: tarifler
        Section(_craftStation != null ? "Tarifler" : $"Tarifler  {rows.Count}", CMidX, CTop);
        if (rows.Count == 0) { Rect(CMidX, CTop + 38, CMidW, 50, T.Panel); Text("Bu aramada tarif yok.", CMidX + 14, CTop + 52, 17, T.Muted); }
        for (int i = _craftScroll; i < Math.Min(rows.Count, _craftScroll + vis); i++) PaintCraftRow(rows[i], i, near);
        if (rows.Count > vis) TextIn($"{_craftScroll + 1}–{Math.Min(rows.Count, _craftScroll + vis)} / {rows.Count}", CMidX, CBottom + 2, CMidW, 13, T.Muted, HorizontalAlignment.Right, true, true);

        // Sag: secili tarif
        if (_craftSel >= 0 && _craftSel < rows.Count) PaintCraftDetail(rows[_craftSel], near);
    }

    void PaintCraftRow(RecipeDef r, int i, HashSet<string> near)
    {
        var area = CraftRowRect(i);
        float y = CTop + 38 + (i - _craftScroll) * (CRowH + CRowGap);
        bool sel = i == _craftSel;
        bool unlocked = Unlocked(r);
        string needSt = CraftRules.NeedStation(r, near);
        var miss = CraftRules.Missing(r, 1, _d.StoreFor(r)); // zincir: istasyon tarifinde canta + tapudaki Sandiklar
        bool ok = unlocked && needSt == null && miss.Count == 0;
        _p.DrawRect(area, sel ? T.Selected : Under(area) ? T.PanelHover : T.Panel);
        if (sel) Frame(CMidX, y, CMidW, CRowH, T.SelectedEdge, 1.5f);
        if (i < 9) Keycap((i + 1).ToString(CultureInfo.InvariantCulture), CMidX + 8, y + (CRowH - 22) / 2f, 22, ok ? T.Ink : T.Muted);
        ItemIcon(r.Id, CMidX + 38, y + 3, CRowH - 6, CRowH - 6, ok ? 1f : 0.5f, false);
        float tx = CMidX + 38 + CRowH + 4, tw = CMidW - (tx - CMidX) - 110;
        Text(Fit(T.Upper(r.Name) + (r.Out > 1 ? $" ×{r.Out}" : ""), tw, 19, true, true), tx, y + 6, 19, ok ? T.Ink : T.A(T.Ink, 0.75f), true, true);
        // Ikinci satir: malzeme (eksik kirmizi) ya da neden yapilamaz. Secili satirin mavisinde kirmizi okunmaz: orada
        // eksik parlak beyaz, yetenler soluk (eksigin ayrintisi sag panelde kirmizi).
        var bad = sel ? T.Ink : T.Red;
        var good = sel ? T.A(T.Ink, 0.55f) : T.A(T.Ink, 0.8f);
        float cx = tx;
        if (!unlocked) Text(Fit(_d.LockText(r), tw, 15), cx, y + 33, 15, _d.ResearchLocked(r) ? T.A(T.Ink, 0.85f) : T.Muted); // zincir: arastirma / Ocak basamagi / kesif
        else
        {
            foreach (var (item, n) in r.In)
            {
                int have = _d.StoreFor(r).Count(item);
                string part = $"{n} {ItemNames.Of(item)}  ";
                if (cx + Measure(part, 15) > tx + tw) { Text("…", cx, y + 33, 15, T.Muted); break; }
                cx += Text(part, cx, y + 33, 15, have >= n ? good : bad);
            }
            if (r.In.Count == 0) Text("malzemesiz", cx, y + 33, 15, T.A(T.Ink, 0.7f));
        }
        // Sag: istasyon ya da sure
        if (needSt != null) TextIn(T.Upper($"{ItemNames.Of(needSt)} gerekir"), CMidX, y + 8, CMidW - 10, 14, bad, HorizontalAlignment.Right, true, true);
        else TextIn(CraftRules.IsHand(r) ? "ELDE" : T.Upper(ItemNames.Of(r.Station)), CMidX, y + 8, CMidW - 10, 13, T.Muted, HorizontalAlignment.Right, true, true);
        TextIn($"{r.Seconds:0.#} SN", CMidX, y + 32, CMidW - 10, 15, T.A(T.Ink, 0.8f), HorizontalAlignment.Right, true, true);
    }

    /// <summary>Sag sutunun tiklanabilir alanlari (cizimle ayni hesap).</summary>
    Rect2 CraftDetailRect(string what, int k = 0)
    {
        _o = CanvasFrame;
        float y0 = CTop + 38;
        return what switch
        {
            "pin" => Area(CRightX + CRightW - 150, y0 + 130 + k * 64 + 30, 150, 28),
            "minus" => Area(CRightX, CBottom - 92, 46, 40),
            "plus" => Area(CRightX + 116, CBottom - 92, 46, 40),
            "max" => Area(CRightX + 168, CBottom - 92, 70, 40),
            "craft" => Area(CRightX + CRightW - 220, CBottom - 92, 220, 82),
            _ => new Rect2(),
        };
    }

    void PaintCraftDetail(RecipeDef r, HashSet<string> near)
    {
        float x = CRightX, w = CRightW, y0 = CTop + 38;
        Section(Fit(T.Upper(r.Name), w, T.Data.Type.Section, true, true), x, CTop);
        // Ust kutu: ikon, tur, nerede
        Rect(x, y0, w, 120, T.Panel);
        ItemIcon(r.Id, x + 10, y0 + 10, 100, 100);
        string where = CraftRules.IsHand(r) ? "Elde üretilir: her yerde." : $"{ItemNames.Of(r.Station)} yanında üretilir.";
        string what = _d.Data.Tool.IsPlaceable(r.Id) ? "Yerleştirilebilir: çantaya girer, kemerden seçip kur." : r.Load ? "Yük: istasyonun önüne çıkar." : _d.Data.Tool.IsFood(r.Id) ? "Yemek: kemerden seçip sol tıkla ye." : "Çantaya girer.";
        Paragraph($"{where} {what}", x + 124, y0 + 12, w - 136, 16, T.Ink, 3);
        Text($"SÜRE  {r.Seconds:0.#} SN   ·   ÜRÜN  ×{r.Out}", x + 124, y0 + 88, 15, T.A(T.Ink, 0.8f), true, true);

        // Malzemeler: her satir ad, eldeki / gereken; eksikse kirmizi, altinda kaynagi ve "Pusulada goster"
        Text("MALZEME", x, y0 + 130 - 26, 17, T.Ink, true, true);
        int k = 0;
        foreach (var (item, n) in r.In)
        {
            float y = y0 + 130 + k * 64;
            int have = _d.StoreFor(r).Count(item), need = n * _craftCount;
            bool okItem = have >= need;
            Rect(x, y, w, 60, T.Panel);
            ItemIcon(item, x + 4, y + 4, 52, 52, okItem ? 1f : 0.6f, false);
            Text(T.Upper(ItemNames.Of(item)), x + 64, y + 6, 18, okItem ? T.Ink : T.Red, true, true);
            TextIn($"{have} / {need}", x, y + 6, w - 12, 18, okItem ? T.Green : T.Red, HorizontalAlignment.Right, true, true);
            if (!okItem)
            {
                var src = CraftRules.Source(_d.Data.Tool, item);
                string line = src != null ? $"{ItemNames.Of(item)}: {src.Text}" : $"{ItemNames.Of(item)}: kaynağı bilinmiyor";
                Text(Fit(line, w - 64 - 160, 15), x + 64, y + 34, 15, T.Red);
                if (src != null)
                {
                    bool pinned = _d.PinnedSource == item;
                    Button(pinned ? "PUSULADA" : "PUSULADA GÖSTER", x + w - 150, y + 30, 150, 28, 14, null, true, false, pinned);
                }
            }
            k++;
        }
        if (r.In.Count == 0) { Rect(x, y0 + 130, w, 44, T.Panel); Text("Malzeme gerekmez.", x + 14, y0 + 142, 16, T.A(T.Ink, 0.8f)); }

        // Alt: adet ve URET
        string needSt = CraftRules.NeedStation(r, near);
        bool unlocked = Unlocked(r);
        bool can = unlocked && needSt == null && CraftRules.Missing(r, _craftCount, _d.StoreFor(r)).Count == 0;
        float by = CBottom - 92;
        Text("ADET", x, by - 24, 15, T.Muted, true, true);
        Button("−", x, by, 46, 40, 24);
        Rect(x + 50, by, 62, 40, T.Panel);
        TextIn(_craftCount.ToString(CultureInfo.InvariantCulture), x + 50, by + 6, 62, 24, T.Ink, HorizontalAlignment.Center, true, true);
        Button("+", x + 116, by, 46, 40, 24);
        Button("MAKS", x + 168, by, 70, 40, 16);
        Text($"TOPLAM {r.Seconds * _craftCount:0.#} SN", x, by + 46, 14, T.A(T.Ink, 0.7f), true, true);
        string label = !unlocked ? "KİLİTLİ" : needSt != null ? T.Upper($"{ItemNames.Of(needSt)} gerekir") : can ? "ÜRET" : "MALZEME EKSİK";
        if (_d.ResearchLocked(r)) (label, can) = ResearchButton(r); // zincir: "ARASTIR · 25 VERI" (Hud.Craft.Research.cs)
        Button(label, x + w - 220, by, 220, 82, label.Length > 10 ? 20 : 30, can ? T.Green.Darkened(0.35f) : T.Panel, can);
    }

    // ---------- Fare ----------

    bool CraftPress(InputEventMouseButton mb)
    {
        if (!CraftOpen) return false;
        var at = mb.Position;
        if (mb.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
        {
            int n = CraftRows().Count, vis = CraftVisibleRows;
            _craftScroll = Mathf.Clamp(_craftScroll + (mb.ButtonIndex == MouseButton.WheelDown ? 1 : -1), 0, Math.Max(0, n - vis));
            _craftSel = Mathf.Clamp(_craftSel, _craftScroll, Math.Max(_craftScroll, Math.Min(n - 1, _craftScroll + vis - 1)));
            return true;
        }
        if (mb.ButtonIndex != MouseButton.Left) return true;
        _searchFocus = false;
        var rows = CraftRows();
        // Satirlar
        for (int i = _craftScroll; i < Math.Min(rows.Count, _craftScroll + CraftVisibleRows); i++)
            if (CraftRowRect(i).HasPoint(at))
            {
                if (i != _craftSel) { _craftSel = i; _craftCount = 1; }
                else if (mb.DoubleClick) _d.Craft(rows[i], _craftCount);
                _d.SyncMenuCursor(i);
                Sfx.Play("place", null, -24f);
                return true;
            }
        // Sol sutun
        if (_craftStation != null)
        {
            _o = CanvasFrame;
            if (Area(CLeftX, CTop + 120, CLeftW, 40).HasPoint(at)) { _craftStation = null; _craftSel = 0; _craftScroll = 0; return true; }
        }
        else
        {
            if (CraftSearchRect().HasPoint(at)) { _searchFocus = true; return true; }
            var cats = _d.Data.Tool.CraftCategories;
            for (int i = 0; i < cats.Count; i++)
                if (CraftCategoryRect(i).HasPoint(at)) { _craftCategory = cats[i].Id; _craftSel = 0; _craftScroll = 0; _craftCount = 1; Sfx.Play("place", null, -20f); return true; }
        }
        // Sag sutun
        if (_craftSel >= 0 && _craftSel < rows.Count)
        {
            var r = rows[_craftSel];
            if (CraftDetailRect("minus").HasPoint(at)) { CraftCountStep(-1); return true; }
            if (CraftDetailRect("plus").HasPoint(at)) { CraftCountStep(1); return true; }
            if (CraftDetailRect("max").HasPoint(at)) { CraftCountStep(_d.Data.Tool.CraftCountMax); return true; }
            if (CraftDetailRect("craft").HasPoint(at)) { _d.Craft(r, _craftCount); return true; }
            int k = 0;
            foreach (var (item, n) in r.In)
            {
                if (_d.StoreFor(r).Count(item) < n * _craftCount && CraftDetailRect("pin", k).HasPoint(at)) { _d.PinSource(item); return true; }
                k++;
            }
        }
        return false;
    }

    // ---------- Kuyruk (sag alt; envanter kapaliyken de) ----------

    /// <summary>Kuyruk satirlari: el isleri, sonra calisan istasyonlar. (baslik, ilerleme 0..1, kalan sn, ikon, iptal).</summary>
    List<(string title, float progress, float left, string icon, Action cancel)> QueueRows()
    {
        var l = new List<(string, float, float, string, Action)>();
        var jobs = _d.State.Craft.Jobs;
        for (int i = 0; i < jobs.Count; i++)
        {
            var j = jobs[i];
            int idx = i;
            float left = j.Recipe.Seconds * (j.Left - (i == 0 ? j.Progress : 0f));
            l.Add(($"{j.Recipe.Name} ×{j.Left}", i == 0 ? j.Progress : 0f, left, j.Recipe.Id, () => _d.CancelCraft(idx)));
        }
        foreach (var st in _d.Stations)
        {
            if (!IsInstanceValid(st) || st.Queue.Active == null || _d.Stations.FirstOrDefault(s => s.Queue == st.Queue) != st) continue;
            var q = st.Queue;
            var s = st;
            l.Add(($"{st.Def.Name}: {q.Active.Name} ×{q.Queued}", q.Progress, q.Active.Seconds * (q.Queued - q.Progress), q.Active.Id, () => _d.CancelStation(s)));
        }
        return l;
    }

    const float QRowH = 34, QW = 300;
    float QueueBottom
    {
        get
        {
            var v = T.Data.Vitals;
            return v.Bottom - 2 * v.Pitch - 8 - (_d.Capa.Current == Capa.Head.Civi && _d.Capa.InHand ? 40 : 0);
        }
    }

    /// <summary>Kuyrugun yuksekligi (tasarim pikseli): toplanan esya akisi bunun ustune cikar (Hud.Feed.cs).</summary>
    public float QueueHeight => QueueRows().Count is int n && n > 0 ? n * (QRowH + 3) + 24 : 0;

    Rect2 QueueCancelRect(int i)
    {
        _o = RightFrame;
        var v = T.Data.Vitals;
        float x = v.X + v.Width - QW, y = QueueBottom - (i + 1) * (QRowH + 3);
        return Area(x + QW - QRowH, y, QRowH, QRowH);
    }

    void PaintQueue(bool inventory)
    {
        var rows = QueueRows();
        if (rows.Count == 0) return;
        _o = RightFrame;
        var v = T.Data.Vitals;
        float x = v.X + v.Width - QW;
        for (int i = 0; i < rows.Count; i++)
        {
            var (title, prog, left, icon, _) = rows[i];
            float y = QueueBottom - (i + 1) * (QRowH + 3);
            Rect(x, y, QW, QRowH, inventory ? T.Panel : T.HudSlot);
            Rect(x, y + QRowH - 4, QW * Mathf.Clamp(prog, 0, 1), 4, T.Green);
            ItemIcon(icon, x + 2, y + 2, QRowH - 4, QRowH - 4, 1f, false);
            Text(Fit(T.Upper(title), QW - QRowH - 90, 15, true, true), x + QRowH + 4, y + 7, 15, T.Ink, true, true, !inventory);
            TextIn($"{Mathf.Ceil(left):0} SN", x, y + 7, QW - (inventory ? QRowH + 6 : 8), 15, T.A(T.Ink, 0.85f), HorizontalAlignment.Right, true, true, !inventory);
            if (inventory) Button("×", x + QW - QRowH, y, QRowH, QRowH, 22, T.A(T.Red, 0.55f));
        }
        float top = QueueBottom - rows.Count * (QRowH + 3) - 22;
        TextIn("ÜRETİM SIRASI", x, top, QW, 13, T.A(T.Ink, 0.75f), HorizontalAlignment.Right, true, true, !inventory);
    }

    bool QueuePress(InputEventMouseButton mb)
    {
        if (mb.ButtonIndex != MouseButton.Left) return false;
        var rows = QueueRows();
        for (int i = 0; i < rows.Count; i++) if (QueueCancelRect(i).HasPoint(mb.Position)) { rows[i].cancel(); return true; }
        return false;
    }
}
