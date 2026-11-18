using System.Linq;
using Godot;

namespace Kabarma.Game;

/// <summary>
/// Tutulan nesne sinamalari (goruntu ve gunluk kaniti icin; oyunda kapali). Kapsul acilisi bittikten sonra baslar:
///   --aim            govde onundeki bir noktaya surekli nisan (AimOverride): kol IK'si ve el hizalama
///   --closeup=ACI    oyuncunun yakinindan bakan kamera; ACI govdeye gore (0 on, 90 sag, 180 arka, -90 sol)
///   --closeup_h=M --closeup_d=M  yakin kameranin yuksekligi ve uzakligi (varsayilan 0,3 ve 1,7; ustten: 2,4 ve 0,6)
///   --closeup_fwd=M --closeup_lookh=M  bakilan nokta govdenin M onunde / yerden M yukarida (varsayilan 0 ve 1,0; araba icin)
///   --firehold       tetigi basili tutar (Capa isini acik; --aim ile govde onundeki noktaya)
///   --aimdist=M --aimh=M  --aim noktasinin uzakligi ve yuksekligi (varsayilan 12 ve 1,3; isin menzil icinde gorunur)
///   --headcycle      1,5 sn'de bir sonraki Capa basligi (baslik degisiminde Capa elde kalir mi)
///   --head=AD        baslangicta bu basligi tak (Kazma, Bicme, Toprak, Sulama, Cekim, Insa, Tirpan, Civi)
///   --cartpush       onune bir El Arabasi koyup tutar (iki el tutamaklara, Capa sirta)
///   --griptest       0,5 sn'de bir GRIP satiri (tutus olcumleri: Player.Grip.cs GripReport)
///   --gripmarkers    sag el soketinde eksen noktalari (kirmizi merkez, yesil bar, mavi parmaklar; sari Capa tutamagi)
/// </summary>
public partial class Player
{
    Camera3D _closeCam;
    MeshInstance3D[] _markers;
    float? _closeAng;
    bool _gripDbgStarted;

    void DebugGrips()
    {
        var a = Main.Args;
        if (a.TryGetValue("closeup", out var ca) && float.TryParse(ca, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var ang))
            _closeAng = ang;
    }

    /// <summary>Player.Hooks.cs _Process'ten her kare.</summary>
    void TickGripDebug()
    {
        var a = Main.Args;
        if (!(a.ContainsKey("aim") || a.ContainsKey("closeup") || a.ContainsKey("headcycle") || a.ContainsKey("cartpush") || a.ContainsKey("griptest") || a.ContainsKey("gripmarkers") || a.ContainsKey("head") || a.ContainsKey("firehold"))) return;
        var d = GetParent() as Director;
        if (d == null || !(d.Pod?.IntroDone ?? true)) return;
        if (!_gripDbgStarted)
        {
            _gripDbgStarted = true;
            var f = Visual.GlobalBasis.Z with { Y = 0 };
            f = f.LengthSquared() > 1e-4f ? f.Normalized() : Vector3.Forward;
            float Num(string k, float def) => a.TryGetValue(k, out var s) && float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : def;
            if (a.ContainsKey("aim")) AimOverride = GlobalPosition + f * Num("aimdist", 12f) + Vector3.Up * Num("aimh", 1.3f);
            if (a.TryGetValue("head", out var hn) && System.Enum.TryParse<Capa.Head>(hn, true, out var head)) d.Capa.Select(head);
            if (a.ContainsKey("firehold")) Input.ActionPress("fire");
            if (a.ContainsKey("cartpush"))
            {
                var cart = new ElArabasi(d);
                d.AddChild(cart);
                var at = d.World.Ground(GlobalPosition + f * 2.4f);
                cart.GlobalTransform = new Transform3D(new Basis(Vector3.Up, Mathf.Atan2(f.X, f.Z)), at);
                cart.Grab();
            }
            if (a.ContainsKey("headcycle"))
            {
                var heads = (Capa.Head[])System.Enum.GetValues(typeof(Capa.Head));
                int i = 0;
                var tm = new Timer { WaitTime = 1.5, Autostart = true };
                AddChild(tm);
                tm.Timeout += () =>
                {
                    for (int k = 0; k < heads.Length; k++) { i = (i + 1) % heads.Length; if (d.Capa.IsAvailable(heads[i])) break; }
                    d.Capa.Select(heads[i]);
                    GD.Print($"HEAD {heads[i]} {GripReport()}");
                };
            }
            if (a.ContainsKey("griptest"))
            {
                var tm = new Timer { WaitTime = 0.5, Autostart = true };
                AddChild(tm);
                tm.Timeout += () => GD.Print($"GRIP t={Time.GetTicksMsec() / 1000f:0.0} clip={_cur} sp={new Vector3(Velocity.X, 0, Velocity.Z).Length():0.0} down={Down} {GripReport()}");
                // Sirt kemigi govde uzayinda (+Z on, +Y yukari, +X modelin solu): grips.json "back" ayari icin.
                tm.Timeout += () =>
                {
                    if (Rig == null) return;
                    var b = Visual.GlobalTransform.AffineInverse() * Rig.BoneWorld(_backBone);
                    var bb = b.Basis.Orthonormalized();
                    GD.Print($"BACKBONE {_backBone} origin={b.Origin} x={bb.X} y={bb.Y} z={bb.Z} capa_origin={Visual.GlobalTransform.AffineInverse() * _capa.GlobalPosition}");
                    // Canta (derisiz mesh) kemigin uzayinda kutu
                    var inv = Rig.BoneWorld(_backBone).AffineInverse();
                    foreach (var mi in Visual.FindChildren("*", "MeshInstance3D", true, false))
                        if (mi is MeshInstance3D m && m.Skin == null && m.Mesh != null && m.Name.ToString().Contains("Backpack"))
                        {
                            Vector3 lo = Vector3.One * 99, hi = -Vector3.One * 99;
                            var t = inv * m.GlobalTransform;
                            var vs = (Vector3[])m.Mesh.SurfaceGetArrays(0)[(int)Mesh.ArrayType.Vertex];
                            foreach (var v in vs) { var p = t * v; lo = lo.Min(p); hi = hi.Max(p); }
                            // Arka yuzun ortasi: dis yuzdeki (z > max-3 cm) koselerin ortasi
                            var outer = vs.Select(v => t * v).Where(p => p.Z > hi.Z - 0.03f).ToList();
                            var oc = outer.Aggregate(Vector3.Zero, (a2, p) => a2 + p) / Mathf.Max(1, outer.Count);
                            GD.Print($"BACKBONE canta {m.Name} kemik uzayinda min={lo} max={hi} dis_yuz_ortasi={oc} ({outer.Count} kose)");
                        }
                };
            }
        }
        if (a.ContainsKey("gripmarkers") && Rig != null)
        {
            // Sag el soketi: kirmizi nokta merkez, yesil bar (basparmak), mavi parmaklar; sari: Capa tutamagi.
            if (_markers == null)
            {
                _markers = new MeshInstance3D[4];
                Color[] cs = { new(1, 0, 0), new(0, 1, 0), new(0, 0.4f, 1), new(1, 0.9f, 0) };
                for (int k = 0; k < 4; k++)
                {
                    _markers[k] = new MeshInstance3D
                    {
                        Mesh = new SphereMesh { Radius = 0.012f, Height = 0.024f },
                        MaterialOverride = new StandardMaterial3D { AlbedoColor = cs[k], ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, NoDepthTest = true },
                        TopLevel = true,
                    };
                    AddChild(_markers[k]);
                }
            }
            var s = SocketWorld(Rig.HandWorld, _sockR);
            _markers[0].GlobalPosition = s.Origin;
            _markers[1].GlobalPosition = s.Origin + s.Basis.Y * 0.06f;
            _markers[2].GlobalPosition = s.Origin + s.Basis.Z * 0.06f;
            _markers[3].GlobalPosition = _capa.GlobalTransform * _capaGripR.Origin + Vector3.Up * 0.002f;
        }
        if (_closeAng is float deg)
        {
            if (_closeCam == null)
            {
                _closeCam = new Camera3D { Name = "CloseCam", Fov = 45f, TopLevel = true };
                AddChild(_closeCam);
            }
            if (!_closeCam.Current) _closeCam.MakeCurrent();
            var fwd = Visual.GlobalBasis.Z with { Y = 0 };
            fwd = fwd.Normalized();
            var right = -(Visual.GlobalBasis.X with { Y = 0 }).Normalized(); // modelin +X'i sol
            float r = Mathf.DegToRad(deg);
            // Yerdeyken (ragdoll) govde kapsulu yerinde kalir, beden savrulur: kamera kalcaya bakar, biraz yukaridan.
            bool down = Down && Rig != null;
            float Arg(string k, float def) => a.TryGetValue(k, out var s) && float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : def;
            var look = down ? Rig.HipsPosition + Vector3.Up * 0.1f
                : GlobalPosition + Vector3.Up * Arg("closeup_lookh", 1.0f) + right * 0.18f + fwd * Arg("closeup_fwd", 0f);
            float ch = a.TryGetValue("closeup_h", out var hs) && float.TryParse(hs, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var hv) ? hv : 0.3f;
            float cd = a.TryGetValue("closeup_d", out var ds) && float.TryParse(ds, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var dv) ? dv : 1.7f;
            _closeCam.GlobalPosition = look + (fwd * Mathf.Cos(r) + right * Mathf.Sin(r)) * (down ? 2.0f : cd) + Vector3.Up * (down ? 1.1f : ch);
            _closeCam.LookAt(look, Vector3.Up);
        }
    }
}
