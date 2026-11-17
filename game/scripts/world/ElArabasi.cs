using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// El Arabasi (ICERIK §14; URETIM §6 M1): Tezgah'ta yapilir, dunyaya cikar. E ile tutamaga gecilir ve yurume hizinin %80'iyle
/// itilir (depar yok). Ya tek bir yuk nesnesi (en fazla 200 kg) ya da 6 esya yuvasi tasir, ikisi birden degil.
/// Dik yokusta itilirse devrilir; yuku yere duser. Devrilmis araba E ile duzeltilir.
/// Bosta dururken donuk (kinematik) durur: yalniz devrilirken fizik nesnesidir.
/// </summary>
public partial class ElArabasi : RigidBody3D
{
    readonly Director _d;
    readonly CartDef _def;
    public Inventory Slots { get; }
    /// <summary>Kasadaki tek yuk nesnesi (kargo kasasi ya da kabarik urun); yoksa null.</summary>
    public SwollenCrop Load { get; private set; }
    public bool Pushed { get; private set; }
    Vector3 _size = new(0.8f, 0.85f, 1.8f);
    float _savedSprint, _savedMult;
    float _settleT;
    Label3D _label;

    public ElArabasi(Director d)
    {
        _d = d;
        _def = d.Data.Economy.Cart;
        Slots = new Inventory(_def.Slots, d.Data.Tool.StackOf);
        Name = "ElArabasi";
        Mass = 30f;
        Freeze = true;
        FreezeMode = FreezeModeEnum.Kinematic;
        CanSleep = true;
        AddToGroup("carts");
    }

    public override void _Ready()
    {
        var vis = Assets.Load("res://assets/models/el_arabasi/el_arabasi.glb");
        Node3D root;
        if (vis != null)
        {
            // Tripo modeli +X'e bakar (kapsul gibi): tekerlek one (+Z) gelsin.
            vis.RotationDegrees = new Vector3(0, -90, 0);
            root = new Node3D { Name = "Visual" };
            root.AddChild(vis);
        }
        else
        {
            var kit = new MeshKit();
            kit.Pillow(new Vector3(0.8f, 0.35f, 1.0f), MeshKit.At(0, 0.55f, 0.15f), Palette.CoopBlue, 0.35f);
            kit.Cylinder(0.22f, 0.22f, 0.12f, MeshKit.At(new Vector3(0, 0.22f, 0.75f), new Vector3(0, 0, Mathf.Pi / 2f)), Palette.DarkGrey, 12);
            foreach (var s in new[] { -0.28f, 0.28f }) kit.Cylinder(0.03f, 0.03f, 1.0f, MeshKit.At(new Vector3(s, 0.6f, -0.55f), new Vector3(Mathf.Pi / 2f - 0.3f, 0, 0)), Palette.Wood, 6);
            root = new Node3D { Name = "Visual" };
            root.AddChild(kit.Instance());
        }
        AddChild(root);
        if (PlaceableVisuals.Bounds(root, out var aabb))
        {
            _size = aabb.Size;
            root.Position = new Vector3(-aabb.GetCenter().X, -aabb.Position.Y, -aabb.GetCenter().Z);
        }
        AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = _size * new Vector3(0.85f, 0.8f, 0.85f) }, Position = new Vector3(0, _size.Y * 0.45f, 0) });
        _label = new Label3D
        {
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 32, OutlineSize = 10, PixelSize = 0.006f,
            Modulate = Palette.OffWhite, OutlineModulate = Palette.NightDeep, NoDepthTest = true, Position = new Vector3(0, _size.Y + 0.4f, 0), Visible = false,
        };
        AddChild(_label);
    }

    public Vector3 Forward { get { var f = GlobalBasis.Z; f.Y = 0; return f.LengthSquared() > 1e-4f ? f.Normalized() : Vector3.Back; } }
    /// <summary>Tutamak: arabanin arkasi (oyuncu burada durur).</summary>
    public Vector3 Handle => GlobalPosition - Forward * (_size.Z * 0.5f + 0.25f);
    /// <summary>Kasa tabani: yuk nesnesi buraya oturur.</summary>
    public Vector3 Bed => GlobalPosition + GlobalBasis.Y * (_size.Y * 0.62f) + Forward * 0.1f;
    public float Length => _size.Z;

    public bool Tipped => GlobalBasis.Y.Normalized().Dot(Vector3.Up) < Mathf.Cos(Mathf.DegToRad(_def.TipDegrees));

    /// <summary>Oyuncu tutamaga yakin mi (E).</summary>
    public bool InReach(Vector3 p) => new Vector2(p.X - GlobalPosition.X, p.Z - GlobalPosition.Z).Length() < _size.Z * 0.5f + 1.6f;

    // ---------- itme ----------

    public void Grab()
    {
        if (Pushed || Tipped) return;
        var p = _d.Player;
        Pushed = true;
        Freeze = true;
        _savedSprint = p.SprintSpeed;
        _savedMult = p.SpeedMult;
        p.SprintSpeed = p.WalkSpeed;                 // iterken depar yok
        p.SpeedMult = _savedMult * _def.PushSpeedMult; // yurume hizinin %80'i
        // Govde arabaya donsun: oyuncu tutamagin arkasina gecer.
        var behind = Handle - Forward * 0.55f;
        p.GlobalPosition = new Vector3(behind.X, p.GlobalPosition.Y, behind.Z);
        p.Visual.Rotation = new Vector3(0, Mathf.Atan2(Forward.X, Forward.Z), 0);
        Sfx.Play("place", GlobalPosition, -6f);
        GD.Print($"CART grab speed={p.WalkSpeed * p.SpeedMult:0.00}");
    }

    public void Release()
    {
        if (!Pushed) return;
        Pushed = false;
        var p = _d.Player;
        p.SprintSpeed = _savedSprint;
        p.SpeedMult = _savedMult;
        Sfx.Play("place", GlobalPosition, -10f);
    }

    /// <summary>Devir: fizik nesnesi olur, yan yatar, yuku yere duser.</summary>
    public void Tip(float side = 1f)
    {
        Release();
        DropLoad();
        Freeze = false;
        Sleeping = false;
        ApplyImpulse(Forward.Cross(Vector3.Up) * side * 60f + Vector3.Up * 40f, GlobalBasis.Y * _size.Y);
        AngularVelocity = Forward * side * 3.5f;
        _settleT = 0;
        Sfx.Play("hit", GlobalPosition, 0f);
        Fx.Burst(GlobalPosition + Vector3.Up * 0.4f, Palette.Ground, 18, 4f, 0.1f, 0.6f);
        _d.Hud.Hint("Araba devrildi. E: düzelt", 2f);
        GD.Print("CART tipped");
    }

    /// <summary>Devrilmis arabayi dogrultur (E).</summary>
    public void Right()
    {
        Freeze = true;
        var pos = _d.World.Ground(GlobalPosition);
        float yaw = Mathf.Atan2(Forward.X, Forward.Z);
        var tw = CreateTween();
        tw.TweenProperty(this, "global_transform", new Transform3D(new Basis(Vector3.Up, yaw), pos), 0.45f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        Sfx.Play("place", pos, 0f);
        GD.Print("CART righted");
    }

    // ---------- yuk ----------

    /// <summary>Tek yuk nesnesini kasaya alir: araba bos (yuk ve yuva) olmali, agirlik sinir icinde.</summary>
    public bool TryLoad(SwollenCrop s, out string why)
    {
        why = "";
        if (Load != null) { why = "arabada zaten yük var"; return false; }
        if (!Slots.IsEmpty) { why = "arabanın yuvaları dolu: önce eşyaları al"; return false; }
        if (s.Size.Kg > _def.MaxLoadKg) { why = $"çok ağır ({s.Size.Kg} kg > {_def.MaxLoadKg:0} kg)"; return false; }
        Load = s;
        if (s is CargoCrateNode c) c.OnCart = this;
        s.Held = false;
        Sfx.Play("place", Bed, -4f);
        GD.Print($"CART load {s.CropId} kg={s.Size.Kg}");
        return true;
    }

    /// <summary>Yuku arabadan ayirir (Cekim ile alindi ya da rampaya yuklendi).</summary>
    public void Detach(SwollenCrop s)
    {
        if (Load != s) return;
        Load = null;
        if (s is CargoCrateNode c && c.OnCart == this) c.OnCart = null;
    }

    /// <summary>Yuku arabanin yanina, yere indirir.</summary>
    public void DropLoad()
    {
        var s = Load;
        if (s == null) return;
        Detach(s);
        if (!IsInstanceValid(s)) return;
        var side = Forward.Cross(Vector3.Up).Normalized();
        var g = _d.World.Ground(GlobalPosition + side * (_size.X * 0.5f + s.Radius + 0.3f));
        var tw = s.CreateTween();
        tw.TweenProperty(s, "global_position", g + Vector3.Up * s.Radius * 0.6f, 0.35f).SetTrans(Tween.TransitionType.Bounce).SetEase(Tween.EaseType.Out);
    }

    /// <summary>Yuva kullanimi: yuk nesnesi varken esya konmaz (ikisi birden degil).</summary>
    public bool CanUseSlots => Load == null;

    // ---------- kare ----------

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        if (Pushed)
        {
            var p = _d.Player;
            if (p.Down) { Release(); return; }
            float yaw = p.Visual.Rotation.Y;
            var fwd = new Vector3(Mathf.Sin(yaw), 0, Mathf.Cos(yaw));
            var center = p.GlobalPosition + fwd * (_size.Z * 0.5f + 0.8f);
            var front = center + fwd * (_size.Z * 0.45f);
            var back = center - fwd * (_size.Z * 0.45f);
            float hf = _d.World.HeightAt(front.X, front.Z), hb = _d.World.HeightAt(back.X, back.Z);
            float slope = Mathf.Atan2(hf - hb, _size.Z * 0.9f);
            var basis = new Basis(Vector3.Up, yaw) * new Basis(Vector3.Right, -slope);
            GlobalTransform = new Transform3D(basis, new Vector3(center.X, (hf + hb) * 0.5f, center.Z));
            if (Mathf.Abs(Mathf.RadToDeg(slope)) > _def.MaxSlopeDegrees) Tip(slope > 0 ? 1f : -1f);
        }
        else if (!Freeze)
        {
            // Devrik ya da dusen araba: durulunca yeniden donar (kararli dursun).
            _settleT += dt;
            if (_settleT > 2.5f && LinearVelocity.Length() < 0.15f && AngularVelocity.Length() < 0.2f) Freeze = true;
        }
        if (Load != null && IsInstanceValid(Load))
        {
            if (Load.Held) Detach(Load);
            else Load.GlobalPosition = Bed + GlobalBasis.Y * Load.Radius * 0.9f;
        }
        else if (Load != null) Load = null;
    }

    public override void _Process(double delta)
    {
        bool near = !Pushed && InReach(_d.Player.GlobalPosition);
        _label.Visible = near;
        if (!near) return;
        string what = Load != null ? $"yük: {(Load is CargoCrateNode ? "Kargo Kasası" : ItemNames.Of(Load.CropId))} ({Load.Size.Kg} kg)"
            : Slots.IsEmpty ? "boş" : $"{Slots.UsedSlots}/{Slots.Slots} yuva";
        _label.Text = Tipped ? "El Arabası (devrik)\nE: düzelt" : $"El Arabası · {what}\nE: tutamak / yük";
    }

    public override void _ExitTree() => Release();
}
