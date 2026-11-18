using System;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Otomatik kayit (TEKNIK 9): save.json autosaveMinutes'te bir, her safakta (rapor kapaninca) ve cikista. Kayit o an
/// alinamiyorsa (gece, elde yuk) alinabildigi ilk ana kayar. Karakter ayri dosyaya yazilir. Director'un cocugu.
/// </summary>
public partial class SaveService : Node
{
    readonly Director _d;
    double _sinceSave;
    string _pending;   // alinamamis kaydin nedeni (safak, otomatik)
    float _retryT;

    /// <summary>Bu oyunda oynanan sure (yuklenince bildirimden devam eder).</summary>
    public double PlaySeconds { get; private set; }
    public int SavesWritten { get; private set; }
    public string LastError { get; private set; }

    public SaveService(Director d)
    {
        _d = d;
        ProcessMode = ProcessModeEnum.Pausable;
    }

    public bool Enabled => _d.SaveRoot != null;

    public override void _Ready()
    {
        Events.Dawn += () => _pending = "safak";
    }

    public void OnLoaded(SaveManifest m)
    {
        PlaySeconds = m.PlaySeconds;
        _sinceSave = 0;
        _pending = null;
    }

    public override void _Process(double delta)
    {
        PlaySeconds += delta / Math.Max(0.01, Engine.TimeScale); // oynanan gercek sure
        if (!Enabled) return;
        _sinceSave += delta / Math.Max(0.01, Engine.TimeScale);
        if (_sinceSave >= _d.Data.Save.AutosaveMinutes * 60.0) { _pending ??= "otomatik"; _sinceSave = 0; }
        if (_pending == null) return;
        _retryT -= (float)delta;
        if (_retryT > 0) return;
        _retryT = 1f;
        if (_d.CanSave(out _)) SaveNow(_pending);
    }

    /// <summary>Simdi yaz. Alinamiyorsa false ve neden (oyuncuya ipucu).</summary>
    public bool SaveNow(string reason, bool quiet = false)
    {
        if (!Enabled) { LastError = "kayit kapali"; return false; }
        if (!_d.CanSave(out var why)) { LastError = why; if (!quiet) _d.Hud?.Hint($"Kaydedilemedi: {why}", 2f); return false; }
        try
        {
            var store = _d.Store;
            var g = _d.CaptureWorld(reason);
            long bytes = store.Write(_d.SaveSlot, g);
            var ch = _d.Save.Capture(SaveScope.Character, new SaveManifest { SavedUtc = g.Manifest.SavedUtc, Reason = reason });
            store.WriteCharacter(ch);
            SavesWritten++;
            _pending = null;
            _sinceSave = 0;
            LastError = null;
            GD.Print($"SAVE ok slot={_d.SaveSlot} reason={reason} bytes={bytes} sections={g.Sections.Count} day={g.Manifest.Day}");
            if (!quiet) _d.Hud?.Hint(reason == "elle" ? "Kaydedildi" : "Otomatik kayıt", 1.5f);
            return true;
        }
        catch (Exception e)
        {
            LastError = e.Message;
            GD.PushError($"SAVE basarisiz: {e.Message}");
            if (!quiet) _d.Hud?.Hint("Kayıt yazılamadı", 2f);
            return false;
        }
    }
}
