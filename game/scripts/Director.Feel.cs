using System;
using Godot;

namespace Kabarma.Game;

/// <summary>
/// Director: vurus hissi. His ayarlari (Feel, feel.json + oyuncu ayari), vurus duraklamasi ve girdi-tepki isareti.
/// Duraklama gercek saatle sayilir: eskiden Engine.TimeScale ile olceklenmis delta'dan dusuluyordu, bu yuzden
/// 0,08 olcekte istenenin ~12,5 kati suruyordu (PROGRESS 17 Kasim denetimi).
/// </summary>
public partial class Director
{
    FeelTuning _feel;
    /// <summary>His ayarlari. Ilk eriside yuklenir (sinamalarda oyuncu ayari okunmaz).</summary>
    public FeelTuning Feel => _feel ??= FeelTuning.Load();

    ulong _hitStopStartUsec, _hitStopEndUsec, _hitStopCheckUsec;
    bool _hitStopOn;
    float _hitStopReqMs;

    /// <summary>Son duraklamanin gercek suresi ve istenen suresi (ms): F1 paneli ve gecikme sinamasi.</summary>
    public float LastHitStopMs { get; private set; }
    public float LastHitStopRequestedMs { get; private set; }
    /// <summary>
    /// Duraklama bitti: istenen ms, olculen ms ve bitis anini kapsayan karenin suresi (ms). Duraklama ancak kare
    /// basinda kalkar; olculenin istenenden fazlasi en cok bu kare kadardir.
    /// </summary>
    public event Action<float, float, float> HitStopEnded;

    /// <summary>Vurus duraklamasi (GDD 4: 30-80 ms). Ust uste gelirse en gec biten gecerli; toplanmaz.</summary>
    public void HitStop(float seconds)
    {
        if (seconds <= 0) return;
        ulong now = Time.GetTicksUsec();
        ulong end = now + (ulong)Math.Round(seconds * 1_000_000.0);
        if (!_hitStopOn) { _hitStopOn = true; _hitStopStartUsec = now; _hitStopCheckUsec = now; _hitStopEndUsec = end; }
        else if (end > _hitStopEndUsec) _hitStopEndUsec = end;
        _hitStopReqMs = (_hitStopEndUsec - _hitStopStartUsec) / 1000f;
    }

    /// <summary>_Process'te kare basina bir kez: duraklama suruyor mu (gercek saat, TimeScale'den bagimsiz).</summary>
    bool HitStopActive()
    {
        if (!_hitStopOn) return false;
        ulong now = Time.GetTicksUsec();
        if (now < _hitStopEndUsec) { _hitStopCheckUsec = now; return true; }
        _hitStopOn = false;
        LastHitStopMs = (now - _hitStopStartUsec) / 1000f;
        LastHitStopRequestedMs = _hitStopReqMs;
        HitStopEnded?.Invoke(_hitStopReqMs, LastHitStopMs, (now - _hitStopCheckUsec) / 1000f);
        return false;
    }

    /// <summary>Girise baslik adiyla gorsel tepki verildi (F1 paneli, gecikme sinamasi).</summary>
    public void MarkReaction(string head) => FeelPanel?.MarkReaction(head);

    /// <summary>His ayarlarini oyuncuya baglar (geri tepme yayi, kamera sarsintisi); F1'de degisince yeniden.</summary>
    void WireFeel()
    {
        Feel.Changed += () => Player.ApplyFeel(Feel.Data);
        Player.ApplyFeel(Feel.Data);
    }
}
