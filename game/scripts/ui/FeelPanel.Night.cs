using Godot;

namespace Kabarma.Game;

/// <summary>
/// F1 paneli, gecehud satiri (URETIM 6 M1 Takvim: "Kucuk Kabarma F1 panelinden yeniden baslatilir, his denemesi icin").
/// Panel acilinca dugme odagi alir: Enter / oyun kolu A ile de basilir (fare tiklamasi da calisir).
/// </summary>
public partial class FeelPanel
{
    /// <summary>Feel.cs _Ready'de tek satirla eklenir.</summary>
    public Button NightRestartButton()
    {
        var b = new Button
        {
            Name = "KucukKabarmaButton",
            Text = "Küçük Kabarma'yı yeniden başlat",
            ActionMode = BaseButton.ActionModeEnum.Press,
            FocusMode = Control.FocusModeEnum.All,
        };
        b.Pressed += () => (GetParent() as Director)?.RestartKucukKabarma();
        b.VisibilityChanged += () => { if (b.IsVisibleInTree()) b.CallDeferred(Control.MethodName.GrabFocus); };
        return b;
    }
}
