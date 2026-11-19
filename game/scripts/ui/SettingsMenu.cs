using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Kabarma.Game;

/// <summary>
/// Ayarlar sayfasi (M6 arayuz; SANAT-SES §8.3): ana menu ve oyun ici Esc menusu ayni sayfayi gosterir. Her satir tiklaninca bir sonraki
/// secenege gecer ve hemen uygulanir: tam ekran, ana ses, arayuz boyu, renk korlugu suzgeci, ekran sarsintisi, altyazi ve boyu;
/// tus atamasi ve ses kanallari oyun icindeki panellerini acar (ana menude kapali). Degerler UserSettings / UiScaler / Controls'ta.
/// </summary>
public partial class RustMenu
{
    /// <summary>Oyun icindeyken tus atama panelini acar (ana menude null: satir kapali).</summary>
    protected virtual System.Action OpenControlsPanel => null;

    static string OnOff(bool b) => b ? "açık" : "kapalı";

    /// <summary>Sinama: ayar satirlarinin sirasi (ilk kelime onekleri).</summary>
    public static readonly string[] SettingsRows =
        { "Tam ekran", "Ana ses", "Arayüz boyu", "Renk körlüğü", "Ekran sarsıntısı", "Altyazı", "Altyazı boyu", "Tuş atamaları" };

    /// <summary>Sinama ve disaridan: ayarlar sayfasini acar.</summary>
    public void OpenSettingsPage() => ShowSettings();

    protected void ShowSettings()
    {
        ClearContent();
        Header("Ayarlar", "Her satır tıklanınca bir sonraki seçeneğe geçer; değişiklik hemen uygulanır ve saklanır.");
        var us = UserSettings.Current;
        var acc = UiTheme.Data.Access;

        bool full = DisplayServer.WindowGetMode() is DisplayServer.WindowMode.Fullscreen or DisplayServer.WindowMode.ExclusiveFullscreen;
        Row($"Tam ekran: {OnOff(full)}", () =>
        {
            DisplayServer.WindowSetMode(full ? DisplayServer.WindowMode.Windowed : DisplayServer.WindowMode.Fullscreen);
            ShowSettings();
        });

        int bus = AudioServer.GetBusIndex("Master");
        float db = bus >= 0 ? AudioServer.GetBusVolumeDb(bus) : 0f;
        int pct = (int)Mathf.Round(Mathf.DbToLinear(db) * 100f);
        Row($"Ana ses: %{pct}   (tıkla: −%10)", () =>
        {
            if (bus < 0) return;
            float lin = Mathf.DbToLinear(AudioServer.GetBusVolumeDb(bus)) - 0.1f;
            if (lin < 0.05f) lin = 1f;
            AudioServer.SetBusVolumeDb(bus, Mathf.LinearToDb(lin));
            ShowSettings();
        });

        var scaler = UiScaler.Instance;
        if (scaler != null && acc.UiScales.Count > 0)
        {
            int cur = Nearest(acc.UiScales, scaler.Preference);
            Row($"Arayüz boyu: %{Mathf.RoundToInt(acc.UiScales[cur] * 100)}", () => { scaler.SetPreference(acc.UiScales[(cur + 1) % acc.UiScales.Count]); ShowSettings(); });
        }

        Row($"Renk körlüğü: {acc.ColorBlindModes[Mathf.Clamp(us.ColorBlind, 0, acc.ColorBlindModes.Count - 1)]}",
            () => { us.SetColorBlind((us.ColorBlind + 1) % acc.ColorBlindModes.Count); ShowSettings(); });
        Row($"Ekran sarsıntısı: {OnOff(us.Shake)}", () => { us.SetShake(!us.Shake); ShowSettings(); });
        Row($"Altyazı: {OnOff(us.Subtitles)}", () => { us.SetSubtitles(!us.Subtitles); ShowSettings(); });
        if (acc.SubtitleScales.Count > 0)
            Row($"Altyazı boyu: %{Mathf.RoundToInt(us.SubtitleScale * 100)}", () => { us.SetSubtitleSize((us.SubtitleSize + 1) % acc.SubtitleScales.Count); ShowSettings(); }, us.Subtitles);

        var open = OpenControlsPanel;
        Row(open != null ? "Tuş atamaları…" : "Tuş atamaları (oyun içinde)", () => open?.Invoke(), open != null);
    }

    static int Nearest(List<float> list, float v)
    {
        int best = 0;
        for (int i = 1; i < list.Count; i++) if (Mathf.Abs(list[i] - v) < Mathf.Abs(list[best] - v)) best = i;
        return best;
    }
}
