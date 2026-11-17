using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>Director: oyuncu girisi (menu tuslari, baslik, kemer, cark, kart, E). Bot varsa tetik bottan gelir.</summary>
public partial class Director
{
    bool _menuClosedThisFrame;

    /// <summary>Karenin girisi. Tetik durumunu verir; menu acikken tetik yok.</summary>
    void ReadInput(out bool fire, out bool firePressed)
    {
        // Giris
        fire = Input.IsActionPressed("fire");
        firePressed = Input.IsActionJustPressed("fire");
        if (Bot != null) { fire = Bot.Fire; firePressed = Bot.FirePressed; Bot.FirePressed = false; }
        if (Hud.MenuOpen)
        {
            for (int i = 0; i < 9; i++) if (Input.IsActionJustPressed($"head_{i + 1}")) Hud.ActivateMenu(i);
            if (Input.IsActionJustPressed("interact") || Input.IsActionJustPressed("ui_cancel") || Input.IsActionJustPressed("bag")) { Hud.HideMenu(); _menuClosedThisFrame = true; }
            fire = false; firePressed = false;
        }
        else
        {
            bool alt = Input.IsKeyPressed(Key.Alt);
            for (int i = 0; i < 8; i++)
                if (Input.IsActionJustPressed($"head_{i + 1}")) { if (alt) UseBelt(i); else Capa.Select((Capa.Head)i); }
            if (Input.IsActionJustPressed("bag")) OpenBagMenu();
            // Baslik carki: Q basili tut, fareyle sec, birak
            if (Input.IsActionJustPressed("head_wheel")) { Hud.OpenWheel((int)Capa.Current); Player.LookLocked = true; }
            if (Hud.WheelOpen && Input.IsActionJustReleased("head_wheel")) { int sel = Hud.CloseWheel(); Player.LookLocked = false; if (sel >= 0) Capa.Select((Capa.Head)sel); }
            if (Capa.Current == Capa.Head.Insa)
            {
                if (Input.IsActionJustPressed("build_next")) Capa.CyclePlaceable(1);
                if (Input.IsActionJustPressed("build_prev")) Capa.CyclePlaceable(-1);
                if (Input.IsActionJustPressed("build_rotate")) Capa.RotateGhost();
            }
            else if (!Hud.WheelOpen)
            {
                int n = Capa.HeadNames.Length;
                if (Input.IsActionJustPressed("build_next")) Capa.Select((Capa.Head)(((int)Capa.Current + 1) % n));
                if (Input.IsActionJustPressed("build_prev")) Capa.Select((Capa.Head)(((int)Capa.Current + n - 1) % n));
            }
        }
        if (Input.IsActionJustPressed("card_1")) ChooseCard(0);
        if (Input.IsActionJustPressed("card_2")) ChooseCard(1);
        if (Input.IsActionJustPressed("card_3")) ChooseCard(2);
        if (Input.IsActionJustPressed("interact") && !Hud.MenuOpen && !_menuClosedThisFrame) Interact();
        _menuClosedThisFrame = false;
    }

    public void Interact()
    {
        if (InteractEconomy()) return; // Director.Economy.cs: El Arabasi, Sandik, Uyku Tulumu
        var st = Stations.Where(s => s.InReach(Player.GlobalPosition)).OrderBy(s => s.GlobalPosition.DistanceTo(Player.GlobalPosition)).FirstOrDefault();
        if (st != null) { OpenStationMenu(st); return; }
        if (Pod.IsRamp && new Vector2(Player.GlobalPosition.X - Pod.PadCenter.X, Player.GlobalPosition.Z - Pod.PadCenter.Z).Length() < 4.5f) { OpenSellMenu(); return; }
        if (Gidak.Visible && !Gidak.Swollen && Gidak.GlobalPosition.DistanceTo(Player.GlobalPosition) < 3f && State.Backpack.Remove("wheat", 1))
            Gidak.Feed();
    }
}
