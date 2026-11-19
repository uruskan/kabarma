using System.Linq;
using Godot;

namespace Kabarma.Game;

/// <summary>
/// Gorunum sinamasi (yaratik4 hatti, bkz. pipeline/yaratik_rig.py): --yaratik4_sira=<tur> turu oyuncunun onunde 3/4 acidan,
/// yerinde, ozel kamerayla gosterir; her klip 2.4 sn, klip ortasinda ekran goruntusu (yurume, kosu, bekleme, saldiri, darbe),
/// sonra cikar. --yaratik4_dir=klasor hedef klasoru, --yaratik4_kod=1 ayni turun koddan yer tutucu govdesini cizer (karsilastirma).
/// Yaratiklarin gercek dogmasi biyom satirlarindan (biomes.json) gelir; bu dosya yalniz goruntu araci.
/// </summary>
public partial class Wildlife
{
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

    void TickSiraGate(float dt)
    {
        if (Main.Args.TryGetValue("yaratik4_sira", out var sira)) TickSira(dt, sira);
    }
}