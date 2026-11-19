using Godot;

namespace Kabarma.Game;

/// <summary>Director, M6 arayuz: oyuncu ayarlari (UserSettings), renk korlugu suzgeci ve fotograf modu.</summary>
public partial class Director
{
    public PhotoMode Photo { get; private set; }
    public AccessOverlay Access { get; private set; }

    /// <summary>Director._Ready'den bir kez.</summary>
    void InitArayuz()
    {
        UserSettings.Load();
        Access = new AccessOverlay { Name = "AccessOverlay" };
        AddChild(Access);
        Photo = new PhotoMode(this);
        AddChild(Photo);
    }
}
