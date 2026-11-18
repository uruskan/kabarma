using System.Collections.Generic;
using System.Globalization;
using Godot;
using T = Kabarma.Game.UiTheme;

namespace Kabarma.Game;

/// <summary>
/// Toplanan esya (SANAT-SES 8.1, M1 karari 20): sag altta, can ve dayanikliligin (ve cephanenin) ustunde birikir; en yenisi
/// altta. Ayni esya sure dolmadan yine gelirse satir birlesir ("+12 Tas"); her satir birkac saniye sonra soner.
/// Kaynak: cantanin Changed olayi (artis = yeni adet - onceki adet). Basvuru: Hollow-Universe GroundUi.Hud PaintFeed.
/// </summary>
public partial class Hud
{
    sealed class Pickup { public string Id; public int Amount; public double At; }

    readonly List<Pickup> _pickups = new();
    readonly Dictionary<string, int> _bagCounts = new();

    void SnapshotBackpack()
    {
        _bagCounts.Clear();
        foreach (var (id, _) in _d.State.Backpack.Contents) _bagCounts[id] = _d.State.Backpack.Count(id);
    }

    void OnBackpackChanged(string id, int count)
    {
        _bagCounts.TryGetValue(id, out int before);
        _bagCounts[id] = count;
        int delta = count - before;
        if (delta <= 0 || !IsInstanceValid(this)) return;
        var f = T.Data.Feed;
        double now = Time.GetTicksMsec() / 1000.0;
        var same = _pickups.Find(p => p.Id == id && now - p.At < f.Seconds);
        if (same != null) { same.Amount += delta; same.At = now; _pickups.Remove(same); _pickups.Add(same); }
        else _pickups.Add(new Pickup { Id = id, Amount = delta, At = now });
        while (_pickups.Count > f.Rows) _pickups.RemoveAt(0);
    }

    /// <summary>Son toplananlar (sinama icin): esya, adet.</summary>
    public IEnumerable<(string id, int amount)> Pickups { get { foreach (var p in _pickups) yield return (p.Id, p.Amount); } }

    void PaintFeed()
    {
        _o = RightFrame;
        var f = T.Data.Feed;
        var v = T.Data.Vitals;
        _pickups.RemoveAll(p => _now - p.At > f.Seconds + f.FadeSeconds);
        float bottom = v.Bottom - 3 * v.Pitch - (_d.Capa.Current == Capa.Head.Civi && _d.Capa.InHand ? 40 : 0) - QueueHeight; // uretim sirasi (Hud.Craft.cs) altta
        for (int i = _pickups.Count - 1, row = 0; i >= 0; i--, row++)
        {
            var p = _pickups[i];
            float a = Mathf.Clamp((float)((f.Seconds + f.FadeSeconds - (_now - p.At)) / f.FadeSeconds), 0, 1);
            float y = bottom - row * v.Pitch;
            Rect(v.X, y, v.Width, v.Height, T.A(T.HudSlot, a));
            ItemIcon(p.Id, v.X + 3, y + 3, v.Height - 6, v.Height - 6, a, false);
            Text(Fit(T.Upper(ItemNames.Of(p.Id)), v.Width - 90, 18, true, true), v.X + v.Height + 6, y + 5, 18, T.A(T.Ink, a), true, true);
            TextIn("+" + p.Amount.ToString(CultureInfo.InvariantCulture), v.X, y + 5, v.Width - 10, 18, T.A(T.Green, a), HorizontalAlignment.Right, true, true);
        }
    }
}
