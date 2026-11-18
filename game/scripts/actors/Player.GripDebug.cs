using Godot;

namespace Kabarma.Game;

/// <summary>
/// Tutulan nesne sinamalari (goruntu ve gunluk kaniti icin; oyunda kapali). Kapsul acilisi bittikten sonra baslar:
///   --aim            govde onundeki bir noktaya surekli nisan (AimOverride): kol IK'si ve el hizalama
///   --closeup=ACI    oyuncunun yakinindan bakan kamera; ACI govdeye gore (0 on, 90 sag, 180 arka, -90 sol)
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
        if (!(a.ContainsKey("aim") || a.ContainsKey("closeup") || a.ContainsKey("headcycle") || a.ContainsKey("cartpush") || a.ContainsKey("griptest") || a.ContainsKey("gripmarkers") || a.ContainsKey("head"))) return;
        var d = GetParent() as Director;
        if (d == null || !(d.Pod?.IntroDone ?? true)) return;
        if (!_gripDbgStarted)
        {
            _gripDbgStarted = true;
            var f = Visual.GlobalBasis.Z with { Y = 0 };
            f = f.LengthSquared() > 1e-4f ? f.Normalized() : Vector3.Forward;
            if (a.ContainsKey("aim")) AimOverride = GlobalPosition + f * 12f + Vector3.Up * 1.3f;
            if (a.TryGetValue("head", out var hn) && System.Enum.TryParse<Capa.Head>(hn, true, out var head)) d.Capa.Select(head);
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
            var look = down ? Rig.HipsPosition + Vector3.Up * 0.1f : GlobalPosition + Vector3.Up * 1.0f + right * 0.18f;
            _closeCam.GlobalPosition = look + (fwd * Mathf.Cos(r) + right * Mathf.Sin(r)) * (down ? 2.0f : 1.7f) + Vector3.Up * (down ? 1.1f : 0.3f);
            _closeCam.LookAt(look, Vector3.Up);
        }
    }
}
