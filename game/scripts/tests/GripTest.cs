using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Kavrama sinamasi (--test=grip; oyuncu akisi). Capa elde mi, namlu nisana mi bakiyor, araba iki elle mi tutuluyor.
///  1. Model: Capa'nin namlu ve tutamak dugumleri modelin gercek geometrisine oturuyor mu (dugume degil, meshe bakilir).
///     Capa cercevesinde (namlu dugumu: koku namlu ucu, +Z namlu): +Z'de en ondeki kose namlu ucunda (3 cm), +Z modelin
///     en uzun yatay ekseni; tutamak noktasinin cevresinde (bar'a dik kesit) iki yanda ve onde-arkada avuc boyunda yuzey var;
///     tutamak namlunun arkasinda ve altinda.
///  2. Bekleme, yurume, kosu, comelme; her biri nisansiz ve nisanli: sag el soketinin "grip" dugumune uzakligi &lt; 3 cm,
///     bar ve parmak eksenleri &lt; 10 derece; nisanliyken namlu dugumunun +Z'si ile nisan yonu arasi &lt; 10 derece;
///     nisansizken namlu govdenin onune bakar ve Capa yerde degil (tutamak dizden yukarida).
///     Ayrica gercek tetikle (AimOverride yok) Kazma ve Bicme isini acikken ayni olculer.
///  3. El Arabasi: modelde sap dugumleri (handle_r, handle_l) var (pipeline/fix_cart_handles.py + grip_markers.py).
///     Model: dugumlerde bar'a dik kesit avuc boyunda (2-7 cm), iki sap simetrik, ayni yukseklikte ve hizada, arka uctan
///     12 cm icinde, sag sap oyuncunun sagi; kovanin onunde sap yok (on ust kose kenardan en fazla 2 cm yukarida).
///     Itme (yururken ve dururken): iki el soketi iki sap dugumune &lt; 3 cm, eksenler &lt; 10 derece; saplar oyuncunun
///     pushReach onunde; pushReach olculen omuz ve koldan hesaplanan degerden &lt; 4 cm sapar.
/// Sonuc: "RESULT ok ..." ya da "RESULT fail ...", cikis kodu 0/1.
/// </summary>
public partial class GripTest : Node
{
    const double TimeoutSeconds = 180;
    const float MaxGap = 0.03f, MaxDeg = 10f;
    static readonly Vector3 Site = new(-400f, 220f, 400f); // dunyanin cok ustu: duz sinama zemini
    Director _d;
    Player P => _d.Player;
    readonly List<string> _fails = new();
    ulong _start;
    bool _done;
    int _checks;
    string _cart = "?";

    public override void _Ready()
    {
        _start = Time.GetTicksUsec();
        CallDeferred(nameof(Begin));
    }

    async void Begin()
    {
        try { await Run(); }
        catch (Exception e) { _fails.Add("istisna: " + e.Message); GD.Print(e.ToString()); }
        Finish();
    }

    public override void _Process(double delta)
    {
        if (!_done && (Time.GetTicksUsec() - _start) / 1e6 > TimeoutSeconds) { _fails.Add($"zaman asimi {TimeoutSeconds} sn"); Finish(); }
    }

    void Finish()
    {
        if (_done) return;
        _done = true;
        GD.Print(_fails.Count == 0 ? $"RESULT ok grip checks={_checks} cart={_cart}" : $"RESULT fail grip ({_fails.Count}/{_checks}) cart={_cart} {string.Join("; ", _fails)}");
        GetTree().Quit(_fails.Count == 0 ? 0 : 1);
    }

    void Check(bool ok, string what)
    {
        _checks++;
        if (!ok) { _fails.Add(what); GD.Print("GRIP FAIL " + what); }
    }

    async Task Physics(int n)
    {
        for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
    }

    Task Seconds(float s) => Physics(Mathf.CeilToInt(s * Engine.PhysicsTicksPerSecond));

    async Task Run()
    {
        while ((_d = GetParent().GetNodeOrNull<Director>("Director")) == null || _d.Player == null || _d.Capa == null)
            await Physics(1);
        while (!(_d.Pod?.IntroDone ?? true)) await Physics(1);
        await Physics(10);

        Model();

        var floor = new StaticBody3D { Name = "GripTestFloor" };
        floor.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(120, 1, 120) } });
        AddChild(floor);
        floor.GlobalPosition = Site + Vector3.Down * 0.5f;
        P.GlobalPosition = Site + Vector3.Up * 0.05f;
        P.Velocity = Vector3.Zero;
        P.Yaw.Rotation = Vector3.Zero;
        P.Pitch.Rotation = Vector3.Zero;
        _d.Capa.Select(Capa.Head.Kazma);
        await Seconds(1f);

        var fwd = (P.Visual.GlobalBasis.Z with { Y = 0 }).Normalized();
        foreach (bool aim in new[] { false, true })
        {
            await State("bekleme", aim, null, null);
            await State("yurume", aim, fwd, null);
            await State("kosu", aim, fwd * 1.5f, null);
            await State("comelme", aim, null, "crouch_walk");
        }
        P.MoveOverride = null;
        P.AimOverride = null;
        await Fire(Capa.Head.Kazma);
        await Fire(Capa.Head.Bicme);
        await Cart();
    }

    /// <summary>Gercek tetik (fare sol, Director.ReadInput yolu), AimOverride yok: isin acikken Capa elde ve namlu
    /// kameranin nisan noktasina bakiyor (sahibin gordugu durum: Bicme ile nisan, Capa elin onunde havada).</summary>
    async Task Fire(Capa.Head head)
    {
        _d.Capa.Select(head);
        P.AimOverride = null;
        P.Yaw.GlobalRotation = new Vector3(0, P.Visual.GlobalRotation.Y + Mathf.Pi, 0); // kamera govdenin arkasindan baksin
        await Physics(3);
        Check(_d.Capa.Current == head, $"{head} secilemedi");
        var tok = _d.Controls.Bindings("fire", InputDevice.Kbm)[0];
        Input.ParseInputEvent(Controls.DeviceEvent(tok, InputDevice.Kbm, true));
        await Seconds(0.6f);
        float gap = 0, aimDeg = 0;
        bool used = true;
        for (int i = 0; i < Engine.PhysicsTicksPerSecond; i++)
        {
            await Physics(1);
            used &= _d.Capa.UsingNow;
            if (!P.CapaInHand) { Check(false, $"{head} ates: Capa elde degil"); break; }
            gap = Mathf.Max(gap, P.GripGapR);
            aimDeg = Mathf.Max(aimDeg, P.MuzzleAimDeg);
        }
        Input.ParseInputEvent(Controls.DeviceEvent(tok, InputDevice.Kbm, false));
        Check(used, $"{head} ates: tetik basiliyken Capa kullanilmiyor");
        Check(gap < MaxGap, $"{head} ates: el kavrama noktasi ile grip arasi {gap * 100:0.0} cm");
        Check(aimDeg < MaxDeg, $"{head} ates: namlu ile nisan yonu arasi {aimDeg:0.0} derece");
        GD.Print($"GRIP ates/{head} gap={gap * 100:0.0}cm namlu-nisan={aimDeg:0.0}d");
        await Seconds(0.4f);
        _d.Capa.Select(Capa.Head.Kazma);
    }

    /// <summary>1. Modelin geometrisi: dugumler meshin gercek namlusuna ve tutamagina oturuyor mu.</summary>
    void Model()
    {
        var capa = P.CapaNode;
        Check(capa != null && P.CapaGripNode != null && P.Muzzle?.Name == "muzzle", "Capa modeli ya da 'grip'/'muzzle' dugumleri yok (pipeline/grip_markers.py)");
        if (capa == null || P.CapaGripNode == null) return;
        // Capa cercevesinde butun koseler (namlu dugumu: koku namlu ucu, +Z namlu, +Y ust)
        var inv = capa.GlobalTransform.AffineInverse();
        var pts = new List<Vector3>();
        foreach (var mi in capa.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
        {
            var t = inv * mi.GlobalTransform;
            for (int s = 0; s < mi.Mesh.GetSurfaceCount(); s++)
                foreach (var v in (Vector3[])mi.Mesh.SurfaceGetArrays(s)[(int)Mesh.ArrayType.Vertex]) pts.Add(t * v);
        }
        Check(pts.Count > 100, $"Capa meshi bos ({pts.Count} kose)");
        if (pts.Count == 0) return;
        float zmax = pts.Max(p => p.Z);
        var tip = pts.Where(p => p.Z > zmax - 0.01f).ToList();
        var tipC = new Vector2((tip.Min(p => p.X) + tip.Max(p => p.X)) / 2, (tip.Min(p => p.Y) + tip.Max(p => p.Y)) / 2);
        Check(Mathf.Abs(zmax) < MaxGap && tipC.Length() < MaxGap, $"namlu dugumu namlu ucunda degil: en ondeki kose z={zmax:0.000}, uc kesit ortasi ({tipC.X:0.000}, {tipC.Y:0.000})");
        float ex = pts.Max(p => p.X) - pts.Min(p => p.X), ez = zmax - pts.Min(p => p.Z), ey = pts.Max(p => p.Y) - pts.Min(p => p.Y);
        Check(ez > ex && ez > 0.2f, $"namlu ekseni (+Z) modelin en uzun yatay ekseni degil (z {ez:0.00}, x {ex:0.00} m)");
        // Tutamak: grip cercevesinde bar'a dik 3 cm'lik kesit; iki yanda ve onde-arkada avuc boyunda yuzey
        var g = P.CapaGrip;
        var gi = g.AffineInverse();
        var sl = pts.Select(p => gi * p).Where(q => Mathf.Abs(q.Y) < 0.015f && new Vector2(q.X, q.Z).Length() < 0.09f).ToList();
        bool sides = sl.Any(q => q.X < -0.015f) && sl.Any(q => q.X > 0.015f);
        bool frontBack = sl.Any(q => q.Z < -0.02f) && sl.Any(q => q.Z > 0.02f);
        float wx = sl.Count > 0 ? sl.Max(q => q.X) - sl.Min(q => q.X) : 0f, wz = sl.Count > 0 ? sl.Max(q => q.Z) - sl.Min(q => q.Z) : 0f;
        Check(sl.Count >= 8 && sides && frontBack && wx < 0.12f, $"tutamak noktasi avuc boyunda bir sapin icinde degil (kesitte {sl.Count} kose, genislik {wx:0.000}, derinlik {wz:0.000} m)");
        Check(g.Origin.Z < -0.15f && g.Origin.Y < -0.03f, $"tutamak namlunun arkasinda ve altinda degil ({g.Origin})");
        Check(Mathf.RadToDeg(g.Basis.Y.AngleTo(Vector3.Up)) < 40f, $"tutamak ekseni dik degil ({Mathf.RadToDeg(g.Basis.Y.AngleTo(Vector3.Up)):0} derece)");
        // Model ici dugum ile Capa cercevesindeki tutamak ayni yerde (agactaki donusum zinciri dogru)
        var gn = inv * P.CapaGripNode.GlobalTransform;
        Check(gn.Origin.DistanceTo(g.Origin) < 0.002f, $"grip dugumu ile Capa tutamagi ayri yerde ({gn.Origin.DistanceTo(g.Origin):0.000} m)");
        GD.Print($"GRIP model namlu_ucu z={zmax:0.000} kesit=({tipC.X:0.000},{tipC.Y:0.000}) uzunluk={ez:0.000} genislik={ex:0.000} yukseklik={ey:0.000} " +
                 $"tutamak={g.Origin} kesit {wx:0.000}x{wz:0.000} m ({sl.Count} kose) egim={Mathf.RadToDeg(g.Basis.Y.AngleTo(Vector3.Up)):0}d");
    }

    /// <summary>2. Bir durum: 1,6 sn surer; ilk 0,6 sn yerlesme, sonra her fizik karesi olculur (en kotusu).</summary>
    async Task State(string name, bool aim, Vector3? move, string pose)
    {
        P.MoveOverride = move;
        var fwd = (P.Visual.GlobalBasis.Z with { Y = 0 }).Normalized();
        P.AimOverride = null;
        if (pose != null) P.PlayPose(pose, 1.8f, true);
        string label = name + (aim ? "+nisan" : "");
        float gap = 0, bar = 0, fing = 0, aimDeg = 0, heading = 0, lowGrip = 9f;
        for (int i = 0; i < Mathf.CeilToInt(1.6f * Engine.PhysicsTicksPerSecond); i++)
        {
            if (aim)
            {
                var f = (P.Visual.GlobalBasis.Z with { Y = 0 }).Normalized();
                P.AimOverride = P.GlobalPosition + f * 12f + Vector3.Up * 1.3f;
            }
            await Physics(1);
            if (i < Mathf.CeilToInt(0.6f * Engine.PhysicsTicksPerSecond)) continue;
            if (!P.CapaInHand) { Check(false, $"{label}: Capa elde degil"); break; }
            gap = Mathf.Max(gap, P.GripGapR);
            bar = Mathf.Max(bar, P.GripBarDeg);
            fing = Mathf.Max(fing, P.GripFingersDeg);
            if (aim) aimDeg = Mathf.Max(aimDeg, P.MuzzleAimDeg);
            var mz = P.Muzzle.GlobalBasis.Z with { Y = 0 };
            var body = (P.Visual.GlobalBasis.Z with { Y = 0 }).Normalized();
            if (mz.LengthSquared() > 1e-4f) heading = Mathf.Max(heading, Mathf.RadToDeg(mz.Normalized().AngleTo(body)));
            lowGrip = Mathf.Min(lowGrip, P.CapaGripNode.GlobalPosition.Y - P.GlobalPosition.Y);
        }
        Check(gap < MaxGap, $"{label}: el kavrama noktasi ile grip arasi {gap * 100:0.0} cm (sinir {MaxGap * 100:0} cm)");
        Check(bar < MaxDeg && fing < MaxDeg, $"{label}: el ekseni ile tutamak ekseni arasi bar {bar:0.0}, parmak {fing:0.0} derece");
        if (aim) Check(aimDeg < MaxDeg, $"{label}: namlu ile nisan yonu arasi {aimDeg:0.0} derece (sinir {MaxDeg:0})");
        Check(heading < 60f, $"{label}: namlu govdenin onune bakmiyor ({heading:0} derece)");
        Check(lowGrip > (pose != null ? 0.25f : 0.5f), $"{label}: Capa cok asagida (tutamak yerden {lowGrip:0.00} m)");
        GD.Print($"GRIP {label} gap={gap * 100:0.0}cm bar={bar:0.0}d parmak={fing:0.0}d namlu-nisan={(aim ? $"{aimDeg:0.0}d" : "-")} yon={heading:0}d tutamak_yuksekligi>={lowGrip:0.00}m clip={P.CurrentClip}");
        P.MoveOverride = null;
        P.AimOverride = null;
        await Seconds(0.3f);
    }

    /// <summary>3. El Arabasi: model (sap dugumleri meshin gercek saplarinda, simetrik, onde sap yok), iki el iki sapa,
    /// itme uzakligi olculen koldan.</summary>
    async Task Cart()
    {
        // Araba yuksekligini araziden alir (ElArabasi._PhysicsProcess): sinama zemini yerine ekonomi sinamasinin duz yeri.
        var site = _d.World.Ground(_d.OcakPos + new Vector3(10, 0, -4));
        P.GlobalPosition = site + Vector3.Up * 0.05f;
        P.Velocity = Vector3.Zero;
        await Seconds(0.5f);
        var f = (P.Visual.GlobalBasis.Z with { Y = 0 }).Normalized();
        var cart = new ElArabasi(_d);
        _d.AddChild(cart);
        cart.GlobalTransform = new Transform3D(new Basis(Vector3.Up, Mathf.Atan2(f.X, f.Z)), _d.World.Ground(P.GlobalPosition + f * 2.4f));
        await Physics(2);
        var hr = cart.FindChild("handle_r", true, false) as Node3D;
        var hl = cart.FindChild("handle_l", true, false) as Node3D;
        bool nodes = hr != null && hl != null;
        Check(nodes, "El Arabasi modelinde handle_r/handle_l dugumu yok (pipeline/grip_markers.py el_arabasi)");
        if (nodes) CartModel(cart, hr, hl);
        cart.Grab();
        P.MoveOverride = f;
        await Seconds(0.6f);
        float r = 0, l = 0, ar = 0, al = 0;
        for (int i = 0; i < Engine.PhysicsTicksPerSecond; i++)
        {
            await Physics(1);
            r = Mathf.Max(r, P.CartGapR);
            l = Mathf.Max(l, P.CartGapL);
            ar = Mathf.Max(ar, P.CartAxisDegR);
            al = Mathf.Max(al, P.CartAxisDegL);
        }
        P.MoveOverride = null;
        await Seconds(0.6f);
        float rs = 0, ls = 0;
        for (int i = 0; i < Engine.PhysicsTicksPerSecond / 2; i++)
        {
            await Physics(1);
            rs = Mathf.Max(rs, P.CartGapR);
            ls = Mathf.Max(ls, P.CartGapL);
        }
        Check(!P.CapaInHand, "araba itilirken Capa sirtta degil");
        if (nodes)
        {
            _cart = $"sag {Mathf.Max(r, rs) * 100:0.0}cm sol {Mathf.Max(l, ls) * 100:0.0}cm";
            Check(r >= 0 && r < MaxGap && l >= 0 && l < MaxGap, $"araba (yururken): eller saplarda degil (sag {r * 100:0.0}, sol {l * 100:0.0} cm)");
            Check(rs >= 0 && rs < MaxGap && ls >= 0 && ls < MaxGap, $"araba (dururken): eller saplarda degil (sag {rs * 100:0.0}, sol {ls * 100:0.0} cm)");
            Check(ar >= 0 && ar < MaxDeg && al >= 0 && al < MaxDeg, $"araba: el eksenleri sap dugumunden {ar:0.0} / {al:0.0} derece");
            // Itme uzakligi: kol yanda hafif one sarkar (omuzdan el soketine kolun PushArm'i). Omuz ve kol olculur.
            var pos = P.GlobalPosition;
            var bf = (P.Visual.GlobalBasis.Z with { Y = 0 }).Normalized();
            var br = Vector3.Up.Cross(bf).Normalized();
            var sh = P.Rig.BoneOrigin("RightArm");
            var gw = hr.GlobalPosition;
            float len = P.ArmLengthR, fs = (sh - pos).Dot(bf), dy = sh.Y - gw.Y, dl = Mathf.Abs((gw - pos).Dot(br) - (sh - pos).Dot(br));
            float k = len * PushArm;
            float ideal = fs + Mathf.Sqrt(Mathf.Max(0f, k * k - dy * dy - dl * dl));
            float have = (gw - pos).Dot(bf);
            float want = _d.Data.Grips.Items["el_arabasi"].PushReach;
            Check(k > Mathf.Sqrt(dy * dy + dl * dl), $"araba: saplar kolun yetisemeyecegi kadar asagida (kol {len:0.00} m, omuz-sap dikey {dy:0.00}, yana {dl:0.00} m)");
            Check(Mathf.Abs(want - ideal) < 0.04f, $"araba: pushReach {want:0.00} m, olculen koldan hesaplanan {ideal:0.00} m");
            Check(Mathf.Abs(have - want) < 0.03f, $"araba: sap oyuncunun {have:0.00} m onunde, pushReach {want:0.00} m");
            GD.Print($"GRIP cart sag={r * 100:0.0}/{rs * 100:0.0}cm sol={l * 100:0.0}/{ls * 100:0.0}cm (yururken/dururken) eksen={ar:0.0}/{al:0.0}d " +
                     $"kol={len:0.000} omuz_onde={fs:0.000} omuz-sap_dikey={dy:0.000} yana={dl:0.000} itme_ideal={ideal:0.000} pushReach={want:0.000} " +
                     $"sap_onde={have:0.000} egim={_d.World.SlopeAt(site.X, site.Z):0.0}d");
        }
        cart.Release();
        cart.QueueFree();
        await Physics(2);
    }

    const float PushArm = 0.92f; // itiste kol boyunun bu kadari (dirsek hafif bukuk; yururken omuz sallanir, IK'ye pay)

    /// <summary>Arabanin sap dugumleri meshin gercek saplarinda mi: kesit avuc boyunda, iki sap simetrik, ayni yukseklikte
    /// ve hizada, arkanin ucunda; kovanin onunde sap kalmamis.</summary>
    void CartModel(ElArabasi cart, Node3D hr, Node3D hl)
    {
        var inv = cart.GlobalTransform.AffineInverse();
        var pts = new List<Vector3>();
        foreach (var mi in cart.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
        {
            var t = inv * mi.GlobalTransform;
            for (int s = 0; s < mi.Mesh.GetSurfaceCount(); s++)
                foreach (var v in (Vector3[])mi.Mesh.SurfaceGetArrays(s)[(int)Mesh.ArrayType.Vertex]) pts.Add(t * v);
        }
        Check(pts.Count > 1000, $"araba meshi bos ({pts.Count} kose)");
        if (pts.Count == 0) return;
        var R = inv * hr.GlobalTransform;
        var L = inv * hl.GlobalTransform;
        string sec = "";
        foreach (var (name, g) in new[] { ("sag", R), ("sol", L) })
        {
            // Sap az koseli: bar'a dik 3 cm'lik dilimde dugumun dort yaninda yuzey var, kesit avuc boyunda.
            var gi = g.AffineInverse();
            var sl = pts.Select(p => gi * p).Where(q => Mathf.Abs(q.Y) < 0.015f && new Vector2(q.X, q.Z).Length() < 0.06f).ToList();
            float wx = sl.Count > 0 ? sl.Max(q => q.X) - sl.Min(q => q.X) : 0f, wz = sl.Count > 0 ? sl.Max(q => q.Z) - sl.Min(q => q.Z) : 0f;
            bool inside = sl.Count > 0 && sl.Min(q => q.X) < -0.008f && sl.Max(q => q.X) > 0.008f && sl.Min(q => q.Z) < -0.008f && sl.Max(q => q.Z) > 0.008f;
            Check(sl.Count >= 4 && inside && wx > 0.02f && wx < 0.07f && wz > 0.02f && wz < 0.07f,
                $"araba {name} sap dugumu avuc boyunda bir sapin icinde degil (kesitte {sl.Count} kose, {wx:0.000} x {wz:0.000} m)");
            sec += $" {name}={wx * 100:0.0}x{wz * 100:0.0}cm";
        }
        float minZ = pts.Min(p => p.Z);
        float rim = pts.Where(p => Mathf.Abs(p.Z) < 0.2f).Max(p => p.Y);
        float frontTop = pts.Where(p => p.Z > 0.15f).Max(p => p.Y);
        Check(Mathf.Abs(R.Origin.X + L.Origin.X) < 0.02f && Mathf.Abs(R.Origin.Y - L.Origin.Y) < 0.01f && Mathf.Abs(R.Origin.Z - L.Origin.Z) < 0.01f,
            $"araba saplari simetrik degil (sag {R.Origin}, sol {L.Origin})");
        Check(R.Origin.X < 0 && L.Origin.X > 0 && R.Origin.X - L.Origin.X < -0.35f, $"araba saplari yanlis yanda ya da cok yakin (sag x {R.Origin.X:0.00}, sol x {L.Origin.X:0.00})");
        Check(R.Origin.Z < minZ + 0.12f, $"araba saplari arkada degil (sap z {R.Origin.Z:0.00}, arka uc {minZ:0.00})");
        Check(frontTop < rim + 0.02f, $"kovanin onunde sap kaliyor (on ust {frontTop:0.00} m, kenar {rim:0.00} m)");
        GD.Print($"GRIP cart model sag={R.Origin} sol={L.Origin} kesit{sec} arka_uc={minZ:0.000} kenar={rim:0.000} on_ust={frontTop:0.000}");
    }
}
