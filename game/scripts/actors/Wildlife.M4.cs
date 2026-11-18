using System.Linq;
using Godot;

namespace Kabarma.Game;

/// <summary>
/// GECICI (yaratik4, M4): yeni biyomlar (Isil Sazlik, Kizil Kanyon, Ayaz Yaylasi, Gobek) biyom akisiyla gelene ve onun
/// "Biyom baglanma notu" main'e girene kadar 15 yeni tur biyom dogma satiri olmadan buradan, kimlige gore dogar:
///   --yaratik4            : 15 tur (Ayaz Kurdu suru halinde) oyuncunun cevresinde, kendi davranislariyla
///   --yaratik4=sergi      : hepsi oyuncunun onunde bir siraya dizilir, yerinde bekleme klibini oynar
///   --yaratik4=vrak,tosun : yalniz bu kimlikler
///   --yaratik4_shot=yol   : 4 sn sonra ekran goruntusu (progress/ altina) ve cikis
/// Biyom satirlari gelince bu dosya silinir (yaratik4'un isi).
/// </summary>
public partial class Wildlife
{
    static readonly string[] M4Species = { "vrak", "kiskac", "suluk_surusu", "tukurgen", "cizbiz", "kosar", "kivrik", "kizgin", "lesci",
        "dehliz", "tosun", "ayaz_kurdu", "paytak", "billur", "hamurcuk" };
    bool _m4Done;
    float _m4T;

    void TickM4Temp(float dt)
    {
        if (!Main.Args.TryGetValue("yaratik4", out var arg)) return;
        _m4T += dt;
        if (!_m4Done && _m4T > 1.5f)
        {
            _m4Done = true;
            bool show = arg == "sergi";
            var ids = arg is "1" or "" or "sergi" ? M4Species : arg.Split(',');
            var p = _d.Player;
            var fwd = -p.GlobalTransform.Basis.Z; fwd.Y = 0; fwd = fwd.LengthSquared() > 0.01f ? fwd.Normalized() : Vector3.Forward;
            var right = fwd.Cross(Vector3.Up);
            for (int i = 0; i < ids.Length; i++)
            {
                var def = _d.Data.Creatures.Creatures.FirstOrDefault(c => c.Id == ids[i]);
                if (def == null) { GD.PrintErr($"YARATIK4 bilinmeyen kimlik {ids[i]}"); continue; }
                Vector3 at;
                if (show) at = p.GlobalPosition + fwd * (9f + (i % 2) * 4f) + right * ((i - ids.Length / 2f) * 2.6f);
                else { float a = Mathf.Tau * i / ids.Length; at = p.GlobalPosition + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 18f; }
                int count = show ? 1 : Mathf.Max(1, def.Wild?.PackMin ?? 1);
                foreach (var n in Spawn(def, at, display: show, count: count))
                    if (show) n.LookAt(new Vector3(p.GlobalPosition.X, n.GlobalPosition.Y, p.GlobalPosition.Z), Vector3.Up, true);
            }
            GD.Print($"YARATIK4 gecici dogurucu: {ids.Length} tur{(show ? " (sergi)" : "")}");
        }
        if (_m4Done && Main.Args.TryGetValue("yaratik4_shot", out var shot) && _m4T > 5.5f)
        {
            Main.Args.Remove("yaratik4_shot");
            var img = GetViewport().GetTexture().GetImage();
            var path = ProjectSettings.GlobalizePath("res://").TrimEnd('/') + "/../" + shot.TrimStart('/');
            DirAccess.MakeDirRecursiveAbsolute(path.GetBaseDir());
            img.SavePng(path);
            GD.Print($"YARATIK4 shot {path}");
            GetTree().Quit(0);
        }
    }
}
