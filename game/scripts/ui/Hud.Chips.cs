using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;
using T = Kabarma.Game.UiTheme;

namespace Kabarma.Game;

/// <summary>
/// Defter'in Cipler sekmesi (ICERIK 6.3, 6.3.1; GDD 4): her baslik ve yuvalari, altta Cip Deposu. Yukari/asagi satir secer;
/// Enter: baslik satirinda baslik secer (aktif baslik), dolu yuvada cipi depoya alir, depodaki cipte aktif basligin ilk bos
/// yuvasina takar. Kabarma gecesi kilitli. Cip ikonlari yer tutucu: nadirlik renginde kare (kredisiz).
/// </summary>
public partial class Hud
{
    enum ChipRowKind { Head, Slot, Storage }
    record ChipEntry(ChipRowKind Kind, string Head, int Slot, int ChipId);

    string _chipHead = "tirpan_isini";

    static readonly Dictionary<string, string> ChipHeadNames = new()
    {
        ["tirpan_isini"] = "Tırpan Işını", ["civi_tabancasi"] = "Çivi Tabancası",
        ["kazma"] = "Kazma", ["bicme"] = "Biçme", ["cekim"] = "Çekim", ["toprak"] = "Toprak", ["sulama"] = "Sulama", ["insa_plani"] = "İnşa Planı",
    };

    /// <summary>Sekmede gosterilen basliklar: acik silah basliklari ve is basliklari.</summary>
    List<string> ChipHeads()
    {
        var s = _d.State;
        var list = new List<string>();
        if (s.Capa.IsUnlocked(_d.Data.Tool.Weapon.Id)) list.Add(_d.Data.Tool.Weapon.Id);
        if (s.Capa.IsUnlocked(_d.Data.Tool.Nailgun.Id)) list.Add(_d.Data.Tool.Nailgun.Id);
        list.AddRange(s.Chips.Def.WorkHeads);
        return list;
    }

    List<ChipEntry> ChipEntries()
    {
        var b = _d.State.Chips;
        var res = new List<ChipEntry>();
        foreach (var h in ChipHeads())
        {
            res.Add(new ChipEntry(ChipRowKind.Head, h, -1, 0));
            var slots = b.SlotsOf(h);
            for (int i = 0; i < slots.Count; i++) res.Add(new ChipEntry(ChipRowKind.Slot, h, i, slots[i]));
        }
        foreach (var c in b.InStorage.OrderByDescending(c => b.Def.Rarities.FindIndex(r => r.Id == c.Rarity)).ThenBy(c => c.Id))
            res.Add(new ChipEntry(ChipRowKind.Storage, "", -1, c.Id));
        return res;
    }

    static string Pct(float v) => v >= 1f && v == Mathf.Floor(v) ? $"+{v:0}" : $"+%{Mathf.Round(v * 100f):0}";

    /// <summary>Cipin tek satirlik etkisi: ana etki ve (Efsane ise) ek etki.</summary>
    string ChipEffectText(ChipInstance c)
    {
        var b = _d.State.Chips;
        var def = b.TypeOf(c.Type);
        if (def == null) return "";
        string main = $"{Pct(b.Contribution(c, def.Stat) - (c.Rarity == "legendary" && def.Legendary.Stat == def.Stat ? def.Legendary.Value : 0f))} {def.Text}";
        return c.Rarity == "legendary" ? $"{main} · {def.Legendary.Text}" : main;
    }

    string ChipLabel(ChipInstance c) => $"{_d.State.Chips.TypeOf(c.Type)?.Name} · {_d.State.Chips.RarityOf(c.Rarity)?.Name}";

    void BuildChipsPage()
    {
        var b = _d.State.Chips;
        var entries = ChipEntries();
        _defterSel = Mathf.Clamp(_defterSel, 0, Mathf.Max(0, entries.Count - 1));
        _defterPage.AddChild(UL($"Çapa Mk.{_d.State.Research.OcakTier} · başlık başına {b.Capacity()} yuva · Depo {b.Owned.Count}/{b.Def.StorageMax}" + (b.Locked ? " · KABARMA: KİLİTLİ" : ""), 16, T.Muted));

        // Mk yukselince satir sayisi artar: secili satiri ortalayan 17 satirlik pencere.
        const int Window = 17;
        int first = Mathf.Clamp(_defterSel - 8, 0, Mathf.Max(0, entries.Count - Window));
        if (first > 0) _defterPage.AddChild(UL($"↑ {first} satır daha", 14, T.Muted, true, true));
        bool storageHeader = false;
        for (int k = first; k < Mathf.Min(entries.Count, first + Window); k++)
        {
            var e = entries[k];
            bool sel = k == _defterSel;
            switch (e.Kind)
            {
                case ChipRowKind.Head:
                    _defterPage.AddChild(ChipRow(sel, e.Head == _chipHead, T.Muted, T.Upper(ChipHeadNames.GetValueOrDefault(e.Head, e.Head)) + (e.Head == _chipHead ? "   ·   AKTİF" : ""), "", true));
                    break;
                case ChipRowKind.Slot:
                {
                    var c = e.ChipId != 0 ? b.Get(e.ChipId) : null;
                    _defterPage.AddChild(c == null
                        ? ChipRow(sel, false, T.Muted, $"   Yuva {e.Slot + 1}: boş", "", false)
                        : ChipRow(sel, true, Color.FromHtml("#" + b.RarityOf(c.Rarity)!.Color), $"   Yuva {e.Slot + 1}: {ChipLabel(c)}", ChipEffectText(c), false));
                    break;
                }
                default:
                {
                    if (!storageHeader) { storageHeader = true; _defterPage.AddChild(DefterSection("ÇİP DEPOSU")); }
                    var c = b.Get(e.ChipId)!;
                    _defterPage.AddChild(ChipRow(sel, false, Color.FromHtml("#" + b.RarityOf(c.Rarity)!.Color), ChipLabel(c), ChipEffectText(c), false));
                    break;
                }
            }
        }
        if (first + Window < entries.Count) _defterPage.AddChild(UL($"↓ {entries.Count - first - Window} satır daha", 14, T.Muted, true, true));
        if (!entries.Any(x => x.Kind == ChipRowKind.Storage)) _defterPage.AddChild(UL("Depo boş. Çipler Atalardan, Kabarma ödülünden, Holding'den ve çarktan düşer; Maya Kesesi ile üretilir.", 15, T.Muted));
        _defterPage.AddChild(UL(_padLast ? "←→: SEKME   ·   ↑↓: SATIR   ·   A: SEÇ / TAK / ÇIKAR" : "←→: SEKME   ·   ↑↓: SATIR   ·   ENTER: SEÇ / TAK / ÇIKAR", 14, T.Muted, true, true));
    }

    /// <summary>Cip satiri: solda nadirlik renginde yer tutucu ikon karesi, ad, sagda etki.</summary>
    Control ChipRow(bool selected, bool filled, Color tone, string text, string right, bool head)
    {
        var row = new PanelContainer();
        row.AddThemeStyleboxOverride("panel", FlatBox(selected ? T.Selected : head ? T.Dialog : T.Panel, 10, head ? 3 : 4));
        var h = new HBoxContainer();
        h.AddThemeConstantOverride("separation", T.Px(10));
        if (!head)
        {
            var icon = new Panel { CustomMinimumSize = new Vector2(T.Px(14), T.Px(14)), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
            var sb = new StyleBoxFlat { BgColor = filled ? tone : new Color(0, 0, 0, 0) };
            if (!filled) { sb.BorderColor = T.Muted; sb.BorderWidthBottom = sb.BorderWidthTop = sb.BorderWidthLeft = sb.BorderWidthRight = Mathf.Max(1, T.Px(1.5f)); }
            icon.AddThemeStyleboxOverride("panel", sb);
            h.AddChild(icon);
        }
        var l = UL(text, head ? 17 : 18, head ? T.Short : filled ? tone.Lerp(T.Ink, 0.45f) : T.Ink, true, head);
        h.AddChild(l);
        var r = UL(right, 15, T.Muted);
        r.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        r.HorizontalAlignment = HorizontalAlignment.Right;
        r.ClipText = true;
        h.AddChild(r);
        row.AddChild(h);
        return row;
    }

    /// <summary>Enter: secili satirin eylemi.</summary>
    void ChipsAccept()
    {
        var b = _d.State.Chips;
        var entries = ChipEntries();
        if (_defterSel < 0 || _defterSel >= entries.Count) return;
        var e = entries[_defterSel];
        switch (e.Kind)
        {
            case ChipRowKind.Head:
                _chipHead = e.Head;
                Sfx.Play("place", null, -12f);
                break;
            case ChipRowKind.Slot:
                if (e.ChipId == 0) { Hint("Depodan bir çip seç, Enter ile tak", 1.5f); return; }
                ChipFeedback(b.Remove(e.Head, e.Slot), "Çip depoya alındı");
                break;
            default:
                ChipFeedback(b.Install(_chipHead, e.ChipId), $"{b.TypeOf(b.Get(e.ChipId)!.Type)?.Name} takıldı: {ChipHeadNames.GetValueOrDefault(_chipHead, _chipHead)}");
                break;
        }
        RebuildDefter();
    }

    void ChipFeedback(ChipResult r, string ok)
    {
        string msg = r switch
        {
            ChipResult.Ok => ok,
            ChipResult.Locked => "Kabarma gecesi çip değişmez",
            ChipResult.Incompatible => "Bu çip bu başlığa takılmaz",
            ChipResult.Duplicate => "Bu çip türü başlıkta zaten var",
            ChipResult.NoSlot => "Boş yuva yok (Çapa yükselince yuva artar)",
            _ => "Olmadı",
        };
        Hint(msg, 2f);
        Sfx.Play(r == ChipResult.Ok ? "place" : "hit", null, -12f);
    }
}
