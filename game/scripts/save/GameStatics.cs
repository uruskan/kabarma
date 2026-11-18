using Godot;

namespace Kabarma.Game;

/// <summary>
/// Godot katmanindaki oyun statiklerinin tek sifirlama yeri (TEKNIK 9; durum_denetimi.md "Godot statikleri"). Yeni oyun
/// ve yuklemeden once Main cagirir: ayni surecte ikinci oyun eskisinin olaylarini, yaratik listesini ya da ses
/// dugumlerini gormez. Onbellekler (ikon, model, shader, yazi tipi) ve komut satiri (Main.Args, Sfx.Muted) kalir.
/// Yeni bir statik durum eklersen buraya bir satir ekle (TEKNIK 9 zaten yeni statik istemez).
/// </summary>
public static class GameStatics
{
    public static void Reset(SceneTree tree)
    {
        Events.Clear();
        Zipzip.All.Clear();
        Sfx.ResetForNewGame();
        Fx.Root = null;
        Engine.TimeScale = 1.0;
        if (tree != null) tree.Paused = false;
    }
}
