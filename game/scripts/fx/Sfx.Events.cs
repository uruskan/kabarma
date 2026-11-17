using Godot;

namespace Kabarma.Game;

/// <summary>
/// Sfx: oyun olaylarina (Events) bagli sesler. Burada yalnizca o anda baska bir ses calmayan olaylar baglidir;
/// kod yerinde zaten ses calan olaylar (toplama, satis, parca) cift calmasin diye baglanmaz. Her sesin calma noktasi
/// audio.json "play" alaninda.
/// </summary>
public static partial class Sfx
{
    static void WireEvents(Director d)
    {
        Events.StationPlaced += id => { if (id == "ocak") Play("ocak_yanma", d.Ocak?.GlobalPosition); }; // Ocak yanmasi ve tapu kubbesi
        Events.CardPicked += _ => Play("kart_secimi");                                                    // kart secimi
        Events.Dawn += () => Play("safak_sonme");                                                         // safakta sonme
        Events.Produced += (_, _) => Play("ui_onay");                                                     // onay: uretim bitti
        Events.StepChanged += (_, _) => Play("ui_onay");                                                  // onay: adim tamam
    }
}
