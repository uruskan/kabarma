using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Elde tutulan nesneler (grips.json). Tek kural: nesne = el kemiginin son pozu x el soketi x tutamagin tersi.
/// Capa bekleme, yurume, kosu, nisan, baslik degisimi, poz ve ragdoll'da hep boyle yerlesir; elden kopmaz, ele gore
/// kaymaz. Nisan almak eli surmektir: istenen Capa duruşundan bilegin hedefi cikarilir, kol IK'si ve el hizalama
/// (PlayerRig) eli oraya goturur, Capa da yine elden yerlesir. Geri tepme eli iter (el ve Capa birlikte teper).
/// Iki elle tutulan nesne: El Arabasi itilirken iki el tutamaklarina gider, Capa sirta gecer.
/// Tek istisna sirt: uyurken ve araba iterken Capa cantanin ustunde durur.
/// Iskeletin son pozu (IK ve ragdoll sonrasi) yalniz PlayerRig.PoseReady aninda okunabilir; nesne o anda yerlesir.
/// </summary>
public partial class Player
{
    GripsFile _grips = new();
    HeldItemDef _capaDef;
    Transform3D _sockR = Transform3D.Identity, _sockL = Transform3D.Identity; // el kemiginin uzayinda
    Transform3D _capaGripR = Transform3D.Identity;                              // Capa'nin uzayinda
    Transform3D _capaBack = Transform3D.Identity;
    string _backBone = "Backpack";
    bool _capaOnBack;
    Vector3 _aimCache;
    ElArabasi _cart;            // oyuncunun ittigi araba (iki elle tutulur)
    Node3D _cartR, _cartL;      // tutamak dugumleri
    Transform3D _visPrev = Transform3D.Identity; // govdenin onceki fizik adimindaki donusumu
    // Sinama olcumleri (son pozda): elin govde tabanina yuksekligi, namlu ile nisan arasi aci, bilegin hedefe uzakligi,
    // sol elin hedefe uzakligi.
    float _handHeight, _aimErrDeg, _wristErr, _leftErr;
    Node3D _capaGripNode;       // modelin "grip" dugumu (BuildCapa)

    // Sinama (GripTest) icin son pozdaki olcumler: sag el soketinin Capa'nin "grip" dugumune uzakligi ve eksen acilari,
    // namlu dugumunun +Z'si ile nisan yonu arasi; araba itilirken iki el soketinin iki sap dugumune uzakligi.
    public float GripGapR { get; private set; }
    public float GripBarDeg { get; private set; }
    public float GripFingersDeg { get; private set; }
    public float MuzzleAimDeg { get; private set; }
    public float CartGapR { get; private set; } = -1f;
    public float CartGapL { get; private set; } = -1f;
    /// <summary>Araba itilirken el soketinin bar ve parmak eksenleri ile sap dugumununkiler arasi en buyuk aci (derece).</summary>
    public float CartAxisDegR { get; private set; } = -1f;
    public float CartAxisDegL { get; private set; } = -1f;
    /// <summary>Sag kolun omuzdan (RightArm) dirsege, bilege ve el soketine boyu (kemik boylari; poza bagli degil).</summary>
    public float ArmLengthR => Rig == null ? 0f
        : Rig.BoneOrigin("RightArm").DistanceTo(Rig.BoneOrigin("RightForeArm")) + Rig.BoneOrigin("RightForeArm").DistanceTo(Rig.BoneOrigin("RightHand"))
          + Rig.BoneOrigin("RightHand").DistanceTo(SocketWorld(Rig.HandWorld, _sockR).Origin);

    static float AxisDeg(Transform3D a, Transform3D b) =>
        Mathf.RadToDeg(Mathf.Max(a.Basis.Y.Normalized().AngleTo(b.Basis.Y.Normalized()), a.Basis.Z.Normalized().AngleTo(b.Basis.Z.Normalized())));
    public bool CapaInHand => _capa != null && !(_capaOnBack || _cart != null);
    /// <summary>Capa dugumu (cercevesi namlu: koku namlu ucu, +Z namlu ekseni) ve tutamagi bu cercevede.</summary>
    public Node3D CapaNode => _capa;
    public Transform3D CapaGrip => _capaGripR;
    public Node3D CapaGripNode => _capaGripNode;
    public Vector3 AimTarget => _aimCache;

    static Vector3 V(float[] a) => a != null && a.Length >= 3 ? new Vector3(a[0], a[1], a[2]) : Vector3.Zero;

    /// <summary>Kavrama cercevesi: +Y bar (basparmaga dogru), +Z parmak bogumlari, +X = Y x Z.</summary>
    static Transform3D Frame(float[] pos, float[] bar, float[] fingers)
    {
        var y = V(bar).Normalized();
        var f = V(fingers);
        var z = (f - y * f.Dot(y)).Normalized();
        return new Transform3D(new Basis(y.Cross(z), y, z), V(pos));
    }

    static Transform3D Frame(GripFrameDef g) => Frame(g.Pos, g.Bar, g.Fingers);

    /// <summary>grips.json'u okur (BuildCapa'dan once).</summary>
    void LoadGrips()
    {
        _grips = (GetParent() as Director)?.Data?.Grips ?? new GripsFile();
        if (_grips.Hands.TryGetValue("right", out var r)) _sockR = Frame(r.Pos, r.Bar, r.Fingers);
        if (_grips.Hands.TryGetValue("left", out var l)) _sockL = Frame(l.Pos, l.Bar, l.Fingers);
        _grips.Items.TryGetValue("capa", out _capaDef); // tutamak cercevesi BuildCapa'da modelin "grip" dugumunden
        if (_capaDef?.Back is { } b)
        {
            _backBone = b.Bone;
            _capaBack = new Transform3D(Basis.FromEuler(V(b.RotDeg) * (Mathf.Pi / 180f)), V(b.Pos));
        }
    }

    /// <summary>El kemiginin dunya donusumu + soket -> soketin dunya cercevesi (olcek yalniz konuma).</summary>
    static Transform3D SocketWorld(Transform3D hand, Transform3D sock) => new(hand.Basis.Orthonormalized() * sock.Basis, hand * sock.Origin);

    /// <summary>Soketin istenen dunya cercevesi -> bilegin (el kemiginin) istenen dunya donusumu.</summary>
    Transform3D WristFor(Transform3D socketWorld, Transform3D sock)
    {
        var b = socketWorld.Basis * sock.Basis.Inverse();
        return new Transform3D(b, socketWorld.Origin - b * (sock.Origin * (Rig?.Scale ?? 1f)));
    }

    /// <summary>Nesnenin (ya da tutamak dugumunun) tutamaginin dunya cercevesi.</summary>
    static Transform3D GripWorld(Node3D n, GripFrameDef g)
    {
        var f = Frame(g);
        var t = n.GlobalTransform;
        return new Transform3D(t.Basis.Orthonormalized() * f.Basis, t * f.Origin);
    }

    ElArabasi FindPushedCart()
    {
        foreach (var n in GetTree().GetNodesInGroup("carts"))
            if (n is ElArabasi c && c.Pushed) return c;
        return null;
    }

    /// <summary>Fizik karesinde, Rig.Update'ten once: ellerin hedefleri (nisan ya da araba tutamaklari).</summary>
    void UpdateGrips()
    {
        if (Rig == null) return;
        var cart = FindPushedCart();
        if (cart != _cart)
        {
            // Itilen araba govdeye degecek kadar yakin: carpismasin (yoksa oyuncu arabaya takilip yuruyemez).
            if (_cart != null && IsInstanceValid(_cart)) RemoveCollisionExceptionWith(_cart);
            if (cart != null) AddCollisionExceptionWith(cart);
            _cart = cart;
            _grips.Items.TryGetValue("el_arabasi", out var cd);
            _cartR = cart != null && cd?.Right != null ? cart.FindChild(cd.Right.Node, true, false) as Node3D : null;
            _cartL = cart != null && cd?.Left != null ? cart.FindChild(cd.Left.Node, true, false) as Node3D : null;
        }
        Rig.RightGoal = null;
        Rig.LeftGoal = null;
        Rig.RightCarry = null;
        var vis = Visual.GlobalTransform;
        var lastVis = _visPrev;
        _visPrev = vis;
        if (Down) return;
        if (_cart != null && _grips.Items.TryGetValue("el_arabasi", out var cart2))
        {
            // Araba kendini oyuncudan SONRA yerlestirir (ElArabasi._PhysicsProcess): tutamaklar bir adim geride.
            // Oyuncunun bu adimdaki hareketi tutamaklara da uygulanir.
            var step = vis * lastVis.AffineInverse();
            if (_cartR != null && cart2.Right != null) Rig.RightGoal = WristFor(step * GripWorld(_cartR, cart2.Right), _sockR);
            if (_cartL != null && cart2.Left != null) Rig.LeftGoal = WristFor(step * GripWorld(_cartL, cart2.Left), _sockL);
            return;
        }
        if (_capaDef == null || _capaOnBack) return;
        // Alcak hazir tutus: namlu govdenin onune, CarryPitchDeg kadar asagi; yalniz bilek doner (kol animasyondan).
        var front = vis.Basis.Z with { Y = 0 };
        front = front.LengthSquared() > 1e-4f ? front.Normalized() : Vector3.Back;
        float pitch = Mathf.DegToRad(_capaDef.CarryPitchDeg);
        var carryFwd = front * Mathf.Cos(pitch) + Vector3.Up * Mathf.Sin(pitch);
        Rig.CarryWeight = _capaDef.CarryAlign;
        Rig.RightCarry = Basis.LookingAt(-carryFwd, Vector3.Up) * _capaGripR.Basis * _sockR.Basis.Inverse();
        if (!(Facing || AimOverride != null)) return;

        // Nisan: yumruk omzun AimReach onunde, AimDrop altinda; namlu nisana; geri tepme yumrugu geri ve yukari iter.
        var shoulder = Rig.BoneOrigin("RightArm");
        var dir = _aimCache - shoulder;
        dir = dir.LengthSquared() > 1e-4f ? dir.Normalized() : Visual.GlobalBasis.Z;
        var hold = shoulder + dir * (_capaDef.AimReach - _recoil * RecoilPushBack) + Vector3.Down * _capaDef.AimDrop
                   + Vector3.Up * _recoil * RecoilLift; // feel.json (Player.Feel.cs)
        var fwd = _aimCache - hold;
        fwd = fwd.LengthSquared() > 1e-4f ? fwd.Normalized() : dir;
        var look = Basis.LookingAt(-fwd, Vector3.Up); // nesnenin +Z'si nisana
        var item = look.Rotated(look.X, _recoil * RecoilTilt);
        var gripW = new Transform3D(item * _capaGripR.Basis, hold);
        Rig.RightGoal = WristFor(gripW, _sockR);
        if (_capaDef.Left != null)
        {
            var itemW = gripW * _capaGripR.AffineInverse();
            Rig.LeftGoal = WristFor(itemW * Frame(_capaDef.Left), _sockL);
        }
    }

    void OnPoseReady()
    {
        if (_capa == null || Rig == null) return;
        var hand = Rig.HandWorld;
        _handHeight = hand.Origin.Y - GlobalPosition.Y;
        _wristErr = Rig.RightGoal is Transform3D g && Rig.RightWeight > 0.99f ? g.Origin.DistanceTo(hand.Origin) : 0f;
        _leftErr = Rig.LeftGoal is Transform3D lg && Rig.LeftWeight > 0.99f ? Rig.BoneOrigin("LeftHand").DistanceTo(lg.Origin) : 0f;
        var sockR = SocketWorld(hand, _sockR);
        // Kemerden elde tutulan esya (Meshale, Insa Plani; Player.Hand.cs) araba itilirken gizlenir: iki el saplarda.
        if (_handItem != null) _handItem.Visible = _cart == null;
        if (_cart != null)
        {
            var sockL = SocketWorld(Rig.BoneWorld("LeftHand"), _sockL);
            _grips.Items.TryGetValue("el_arabasi", out var cd);
            CartGapR = _cartR != null ? sockR.Origin.DistanceTo(_cartR.GlobalPosition) : -1f;
            CartGapL = _cartL != null ? sockL.Origin.DistanceTo(_cartL.GlobalPosition) : -1f;
            CartAxisDegR = _cartR != null && cd?.Right != null ? AxisDeg(sockR, GripWorld(_cartR, cd.Right)) : -1f;
            CartAxisDegL = _cartL != null && cd?.Left != null ? AxisDeg(sockL, GripWorld(_cartL, cd.Left)) : -1f;
        }
        if ((_capaOnBack || _cart != null) && Rig.Body.HasBag)
        {
            _capa.GlobalTransform = Rig.BoneWorld(_backBone) * _capaBack;
            return;
        }
        _capa.GlobalTransform = sockR * _capaGripR.AffineInverse();
        if (_capaGripNode != null)
        {
            // Olcum modelin dugumunden (agactaki gercek donusum), _capaGripR'den degil.
            var gn = _capaGripNode.GlobalTransform;
            GripGapR = sockR.Origin.DistanceTo(gn.Origin);
            GripBarDeg = Mathf.RadToDeg(sockR.Basis.Y.AngleTo(gn.Basis.Y));
            GripFingersDeg = Mathf.RadToDeg(sockR.Basis.Z.AngleTo(gn.Basis.Z));
        }
        _aimErrDeg = Facing || AimOverride != null
            ? Mathf.RadToDeg(_capa.GlobalBasis.Z.Normalized().AngleTo((_aimCache - Muzzle.GlobalPosition).Normalized()))
            : 0f;
        MuzzleAimDeg = Mathf.RadToDeg(Muzzle.GlobalBasis.Z.Normalized().AngleTo((_aimCache - Muzzle.GlobalPosition).Normalized()));
    }

    /// <summary>Sinama satiri (--rigtest, --griptest).</summary>
    string GripReport() =>
        $"held={(_capaOnBack || _cart != null ? "sirt" : "el")} cart={_cart != null} armR={Rig?.RightWeight:0.00} armL={Rig?.LeftWeight:0.00} " +
        $"aimErr={_aimErrDeg:0.0}deg wristErr={_wristErr:0.000} leftErr={_leftErr:0.000} handY={_handHeight:0.00} " +
        $"muzzleY={Muzzle.GlobalPosition.Y - GlobalPosition.Y:0.00} head={(GetParent() as Director)?.Capa?.Current}";
}
