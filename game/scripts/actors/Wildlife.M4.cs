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

    // --yaratik4_sira=homur (+ --yaratik4_dir=progress/m4/yaratik4/homur): turu oyuncunun onunde 3/4 acidan, yerinde, her klip 2.4 sn;
    // klip ortasinda ekran goruntusu (yurume, kosu, bekleme, saldiri, darbe), sonra cikis.
    static readonly string[] SiraSlots = { "walk", "run", "idle", "attack1", "hit" };
    WildCreature _siraC;
    Camera3D _siraCam;
    int _siraI = -1;
    float _siraT;

    void TickSira(float dt, string speciesId)
    {
        _m4T += dt;
        var p = _d.Player;
        if (_siraC == null && _m4T > 1.5f)
        {
            var def = _d.Data.Creatures.Creatures.First(c => c.Id == speciesId);
            if (Main.Args.ContainsKey("yaratik4_kod") && _d.Data.Body(speciesId) != null)
            {
                // Karsilastirma: ayni turun koddan yer tutucu govdesi (bodies.json), Tripo modeli yerine; rol adlari govdede ayni.
                def.Rig!.Model = "code:" + speciesId;
                def.Rig.Bones = def.Rig.Bones.Keys.ToDictionary(k => k, k => k);
                def.Rig.Clips = new(); def.Rig.ClipSpeed = new();
            }
            var fwd = -p.GlobalTransform.Basis.Z; fwd.Y = 0; fwd = fwd.Normalized();
            var at = p.GlobalPosition + fwd * 3.6f + fwd.Cross(Vector3.Up) * 0.6f;
            _siraC = Spawn(def, at, display: true)[0] as WildCreature;
            _siraC.Rotation = new Vector3(0, Mathf.Atan2(fwd.X, fwd.Z) + Mathf.DegToRad(215f), 0); // yan-onden gorunum
            _siraC.DisplaySlot = SiraSlots[0];
            _siraI = 0; _siraT = 0;
            // Ayri kamera: yaratigin onunden-yanindan 3/4 (oyuncu kamerasi uzak ve kucuk gosteriyordu).
            var b = _siraC.GlobalTransform.Basis;
            var cen = _siraC.GlobalPosition + Vector3.Up * ((def.Rig?.Height ?? 0.9f) * 0.5f);
            var camAt = cen + b.Z * 2.3f + b.X * 2.2f + Vector3.Up * 0.5f;
            _siraCam = new Camera3D { Fov = 38f, Far = 400f };
            AddChild(_siraCam);
            _siraCam.GlobalPosition = camAt;
            _siraCam.LookAt(cen, Vector3.Up);
            _siraCam.MakeCurrent();
            GD.Print($"YARATIK4 sira basladi {speciesId} dogus_y={_siraC.GlobalPosition.Y:0.00} zemin_y={_d.World.Ground(_siraC.GlobalPosition).Y:0.00}");
            return;
        }
        if (_siraC == null) return;
        _siraT += dt;
        if (_siraT > 1.6f && _siraT - dt <= 1.6f)
        {
            var dir = Main.Args.TryGetValue("yaratik4_dir", out var d) ? d : "progress/m4/yaratik4/sira";
            var path = ProjectSettings.GlobalizePath("res://").TrimEnd('/') + "/../" + dir.Trim('/') + $"/{speciesId}_{SiraSlots[_siraI]}.png";
            DirAccess.MakeDirRecursiveAbsolute(path.GetBaseDir());
            GetViewport().GetTexture().GetImage().SavePng(path);
            GD.Print($"YARATIK4 sira {speciesId} {SiraSlots[_siraI]} y={_siraC.GlobalPosition.Y:0.00} {path}");
        }
        if (_siraT > 2.4f)
        {
            _siraT = 0;
            if (++_siraI >= SiraSlots.Length) { GetTree().Quit(0); return; }
            _siraC.DisplaySlot = SiraSlots[_siraI];
        }
    }

    void TickM4Temp(float dt)
    {
        if (Main.Args.TryGetValue("yaratik4_sira", out var sira)) { TickSira(dt, sira); return; }
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
