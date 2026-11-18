using Godot;

namespace Kabarma.Game;

/// <summary>
/// Pano girisi: isaretcinin yeri, son kullanilan aygit (glifler: klavye kare tus, kol yuvarlak dugme; SANAT-SES 8.1 "ipucu
/// ikonlari son kullanilan aygita gore degisir"), envanterde fare (tikla, surukle-birak, Shift+tik) ve carkin iptali
/// (Esc, sag tik). Hud, Player'dan sonra eklendigi icin _Input'u ondan once alir: envanterde tiklamayi burada tuketmek
/// Player'in fareyi yeniden kilitlemesini onler. Girdi akisinin Controls'u birlesince aygit oradan okunur.
/// </summary>
public partial class Hud
{
    Vector2 _pointer;
    bool _pointerSeen;
    bool _padLast;

    public override void _Input(InputEvent e)
    {
        if (e is InputEventJoypadButton { Pressed: true } || e is InputEventJoypadMotion jm && Mathf.Abs(jm.AxisValue) > 0.5f) _padLast = true;
        else if (e is InputEventKey { Pressed: true } || e is InputEventMouseButton { Pressed: true }) _padLast = false;
        if (e is InputEventMouse m) { _pointer = m.Position; _pointerSeen = true; }

        if (WheelOpen)
        {
            if (e is InputEventKey { Pressed: true, Echo: false, PhysicalKeycode: Key.Escape } || e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right })
            {
                CancelWheel();
                GetViewport().SetInputAsHandled();
            }
            return;
        }
        if (!InventoryOpen)
        {
            // Envanter kapaliyken sag alttaki uretim sirasina tiklanmaz (fare kilitli); yalniz envanterde iptal edilir.
            return;
        }
        if (e is InputEventKey k && CraftKey(k)) { GetViewport().SetInputAsHandled(); return; } // Uretim arama kutusu
        if (e is InputEventMouseButton mb)
        {
            if (mb.Pressed) InventoryPress(mb);
            else InventoryRelease(mb);
            GetViewport().SetInputAsHandled(); // Player tiklamayla fareyi kilitlemesin
        }
        else if (e is InputEventMouseMotion mm) InventoryMotion(mm);
    }

    public override void _Notification(int what)
    {
        // Pencere odagi geri gelince Player fareyi kilitler; envanter aciksa imlec geri gelsin.
        if (what == NotificationApplicationFocusIn && InventoryOpen && _d?.Bot == null)
            Callable.From(() => { if (InventoryOpen) Input.MouseMode = Input.MouseModeEnum.Visible; }).CallDeferred();
    }
}
