using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Bereket Holding birimi (GDD §19.3; holding, M3). Durum Sim'de (<see cref="HoldingUnit"/>): bu dugum yalniz gosterir ve
/// host'ta yurur ve ates eder, konumu Sim'e geri yazar. Istemcide kukladir (Sim konumuna yumusak gider).
/// YER TUTUCU GORUNUS: oyuncu modeli kirmizi tulum boyasiyla (birim basina "tint"); gercek Holding modelleri uretim isteginde.
/// Davranis: isci damar ile kamp arasinda gider gelir, oyuncudan kacar; bekci kampin cevresinde devriye atar (gunduz
/// Perde'de ates etmez; kiskirtilirsa ya da gece ates eder); kervan korumasi araci izler; baskin ekibi tapuya yurur, iki
/// tasiyici kabarik urun kapar ve kampa yarim hizla tasir; tasiyici duserse urun yere duser.
/// </summary>
public partial class HoldingUnitActor : Node3D
{
    readonly HoldingWorld _w;
    public int UnitId { get; }
    public int CampId { get; }
    public HoldingUnitDef Def { get; }
    public string Job { get; }
    Node3D _visual;
    AnimationPlayer _anim;
    string _clip = "";
    Label3D _label;
    float _fireCd, _t, _speedNow;
    public SwollenCrop Carried { get; private set; }
    public bool Entered { get; private set; }
    Vector3 _lastPos;

    public HoldingUnitActor(HoldingWorld w, int campId, HoldingUnit u, HoldingUnitDef def)
    {
        _w = w; CampId = campId; UnitId = u.Id; Def = def; Job = u.Job;
        Name = $"Holding_{def.Id}_{u.Id}";
        _t = u.Id * 1.37f;
        // ortak: CreatureGroup'a girmez (kurulus sirasi kimligi sonradan doganlarla tutmaz); konum "holding" bolumundeki
        // Sim durumundan gelir, istemcide Think calismaz (HoldingWorld.Host).
    }

    public override void _Ready()
    {
        _visual = new Node3D { Name = "Visual" };
        AddChild(_visual);
        var tint = Color.FromString("#" + (Def.Tint.Length > 0 ? Def.Tint : "B83A2A"), Palette.HoldingRed);
        var clips = _w.D.Data.PlayerAnims;
        string path = string.IsNullOrEmpty(clips.Model) ? "res://assets/models/player/player.glb" : clips.Model;
        if (ResourceLoader.Exists(path) && GD.Load<PackedScene>(path) is PackedScene scene)
        {
            var model = scene.Instantiate<Node3D>();
            _visual.AddChild(model);
            var meshes = model.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().ToList();
            Aabb? box = null;
            foreach (var mi in meshes) { var b = mi.Transform * mi.GetAabb(); box = box == null ? b : box.Value.Merge(b); }
            float h = Def.Role == "elite" ? 1.85f : 1.6f;
            if (box is Aabb a && a.Size.Y > 0.5f && a.Size.Y < 5f) { float s = h / a.Size.Y; model.Scale = Vector3.One * s; model.Position = new Vector3(0, -a.Position.Y * s, 0); }
            // Holding kirmizisi: modelin ustune yari saydam boya (yer tutucu; gercek tulum modeli uretim isteginde).
            var overlay = new StandardMaterial3D { AlbedoColor = new Color(tint, 0.55f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };
            foreach (var mi in meshes) mi.MaterialOverlay = overlay;
            _anim = model.FindChildren("*", "AnimationPlayer", true, false).OfType<AnimationPlayer>().FirstOrDefault();
            if (_anim != null) foreach (var c in clips.Clips) if (c.Loop && _anim.HasAnimation(c.Id)) _anim.GetAnimation(c.Id).LoopMode = Animation.LoopModeEnum.Linear;
        }
        else
        {
            var kit = new MeshKit();
            kit.Pillow(new Vector3(0.62f, 0.9f, 0.45f), MeshKit.At(0, 0.6f, 0), tint, 0.5f);
            kit.Sphere(0.42f, MeshKit.At(0, 1.33f, 0.02f), Palette.OffWhite, 16, 8);
            _visual.AddChild(kit.Instance());
        }
        if (Def.Role == "elite") // Ustabasi: sirtta havan borusu
        {
            var kit = new MeshKit();
            kit.Cylinder(0.14f, 0.16f, 0.9f, MeshKit.At(new Vector3(0, 1.25f, -0.28f), new Vector3(0.5f, 0, 0)), Palette.DarkGrey);
            _visual.AddChild(kit.Instance());
        }
        _label = new Label3D
        {
            Text = Def.Name, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, Position = new Vector3(0, Def.Role == "elite" ? 2.3f : 2.05f, 0),
            FontSize = 34, OutlineSize = 8, Modulate = Palette.HoldingRed.Lightened(0.35f), PixelSize = 0.006f,
        };
        AddChild(_label);
        _lastPos = GlobalPosition;
    }

    /// <summary>Vurus icin govde merkezi.</summary>
    public Vector3 Center => GlobalPosition + Vector3.Up * 0.9f;
    public float HitRadius => Def.Role == "elite" ? 0.6f : 0.45f;

    /// <summary>Her kare: istemcide Sim konumuna gider; host'ta dusunur. Sonra yere oturur, yonelir, klip secer.</summary>
    public void Step(float dt, HoldingCamp camp, HoldingUnit u, bool host)
    {
        _t += dt;
        if (host) Think(dt, camp, u);
        else
        {
            // Konum kayit kopruyle saniyede bir gelir: birim oraya kendi hizinin biraz ustunde yurur (cok gerideyse isinlanir).
            var to = new Vector3(u.X, GlobalPosition.Y, u.Z);
            float gap = GlobalPosition.DistanceTo(to);
            GlobalPosition = gap > 12f ? to : GlobalPosition.MoveToward(to, Mathf.Max(Def.Speed * 1.3f, gap * 1.5f) * dt);
        }
        var p = GlobalPosition;
        p.Y = _w.D.World.HeightAt(p.X, p.Z);
        GlobalPosition = p;
        var v = (p - _lastPos) / Mathf.Max(dt, 0.001f);
        v.Y = 0;
        _speedNow = Mathf.Lerp(_speedNow, v.Length(), Mathf.Min(1f, dt * 6f));
        if (v.LengthSquared() > 0.04f) _visual.Rotation = new Vector3(0, Mathf.LerpAngle(_visual.Rotation.Y, Mathf.Atan2(v.X, v.Z), Mathf.Min(1f, dt * 8f)), 0);
        _lastPos = p;
        if (Carried != null && IsInstanceValid(Carried)) Carried.GlobalPosition = GlobalPosition + Vector3.Up * 2.1f;
        PlayClip(_speedNow < 0.3f ? "idle" : _speedNow > 4.5f ? "run" : "walk");
        _label.Text = $"{Def.Name}  {Mathf.CeilToInt(u.Hp)}";
    }

    void PlayClip(string id)
    {
        if (_anim == null) return;
        var (name, _) = _w.D.Data.PlayerAnims.Resolve(id);
        if (!_anim.HasAnimation(name)) name = _anim.HasAnimation(id) ? id : "";
        if (name.Length == 0 || name == _clip) return;
        _clip = name;
        _anim.Play(name, 0.2);
    }

    // ---------------------------------------------------------------- host yapay zekasi

    void Think(float dt, HoldingCamp camp, HoldingUnit u)
    {
        var home = new Vector3(camp.X, 0, camp.Z);
        var (tPos, tPeer, has) = _w.NearestTarget(GlobalPosition);
        float tDist = has ? Flat(tPos - GlobalPosition).Length() : 1e9f;
        bool night = _w.H.State.NightOpen;
        bool provoked = _w.ProvokedAt(CampId) > 0;
        float speed = Def.Speed * MudurRules.UnitSpeedMult(_w.Def, _w.H.State.Mudur); // Mudur hoparlor konusmasi birimleri hizlandirir
        bool repair = u.Kind == "mekanik" && _w.Encounter is { Stage: 0 } enc0 && enc0.CampId == CampId; // Mudur avlusu: Mekanik kamyonu onarmaya kosar
        Vector3? goal = null;
        bool hostile = false;

        switch (repair ? "repair" : Job)
        {
            case "repair":
                goal = _w.Encounter.TruckPos;
                speed *= 1.2f;
                break;
            case "raid":
                hostile = true;
                if (Carried != null)
                {
                    if (!IsInstanceValid(Carried)) { Carried = null; u.Carrying = false; break; }
                    speed *= _w.Def.Raid.CarrySpeedMult;
                    goal = home;
                    hostile = false;
                    if (Flat(home - GlobalPosition).Length() < 6f) _w.Stolen(this, u);
                    break;
                }
                var claim = _w.D.OcakPos;
                if (!Entered && Flat(claim - GlobalPosition).Length() < _w.ClaimRadius)
                {
                    Entered = true;
                    _w.RaidEntered(CampId);
                }
                if (Entered && _w.IsCarrier(this) && _w.FreeCrop(GlobalPosition) is Node3D target)
                {
                    goal = target.GlobalPosition;
                    hostile = tDist < Def.Range * 0.5f;
                    if (Flat(target.GlobalPosition - GlobalPosition).Length() < 1.6f)
                    {
                        var crop = target as SwollenCrop ?? (target as PlotView)?.Lift(); // tarladaki urun sokulur (Farm.Uproot)
                        if (crop != null) { Carried = crop; crop.Held = true; u.Carrying = true; GD.Print($"HOLDING carry {crop.CropId} by #{UnitId}"); }
                    }
                    break;
                }
                if (tDist < Def.AggroRange) goal = tDist > Def.Range * 0.7f ? tPos : null;
                else goal = claim;
                break;
            case "caravan":
                hostile = provoked;
                goal = _w.EscortSlot(CampId, UnitId);
                if (hostile && tDist < Def.AggroRange && tDist > Def.Range * 0.7f) goal = tPos;
                speed *= 1.15f;
                break;
            default: // kamp
                if (Def.Flees)
                {
                    if (tDist < 10f) { goal = GlobalPosition + Flat(GlobalPosition - tPos).Normalized() * 6f; speed *= 1.3f; }
                    else
                    {
                        // Isci: kamp ile damar arasinda gider gelir (40 sn'lik dongu; damar yonu kimlige gore).
                        float ang = UnitId * 2.39996f;
                        var vein = home + new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)) * Mathf.Min(40f, _w.Def.Camp.VeinRadius);
                        goal = Mathf.PosMod(_t, 40f) < 20f ? vein : home + new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)) * 6f;
                    }
                    break;
                }
                hostile = (night || provoked) && !_w.InPerdeDay(CampId, GlobalPosition);
                float leash = Flat(GlobalPosition - home).Length();
                if (hostile && tDist < Def.AggroRange * (provoked ? 2f : 1f) && leash < 70f)
                    goal = tDist > Def.Range * 0.7f ? tPos : null;
                else
                {
                    float a = _t * 0.12f + UnitId * 1.7f;
                    goal = home + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (12f + (UnitId % 3) * 3f);
                }
                break;
        }

        if (goal is Vector3 g) Move(g, speed, dt, Job == "raid" && Carried == null && !Entered);
        _fireCd -= dt;
        if (hostile && has && Def.Damage > 0 && tDist <= Def.Range && _fireCd <= 0)
        {
            _fireCd = Def.FireSeconds;
            _w.Shoot(this, tPos, tPeer, Def.Damage);
        }
    }

    void Move(Vector3 goal, float speed, float dt, bool useFlow)
    {
        var to = Flat(goal - GlobalPosition);
        float d = to.Length();
        if (d < 0.6f) return;
        var dir = to / d;
        if (useFlow && _w.D.Flow?.Steer(GlobalPosition, "crop") is Vector3 s && s.LengthSquared() > 0.01f) dir = Flat(s).Normalized(); // yolbulma: akis alani (yoksa dogrudan)
        GlobalPosition += dir * Mathf.Min(d, speed * dt);
    }

    static Vector3 Flat(Vector3 v) => new(v.X, 0, v.Z);

    /// <summary>Tasiyici duser ya da baskin dagilirsa urun yere birakilir.</summary>
    public void DropCrop()
    {
        if (Carried == null) return;
        if (IsInstanceValid(Carried)) { Carried.Held = false; Carried.GlobalPosition = _w.D.World.Ground(GlobalPosition) + Vector3.Up * 0.3f; }
        Carried = null;
    }

    /// <summary>Urun kampa vardi: tasiyicinin elinden alinir (urun kaybolur).</summary>
    public void ConsumeCrop()
    {
        if (Carried != null && IsInstanceValid(Carried)) { Carried.Sold = true; Carried.QueueFree(); }
        Carried = null;
    }
}
