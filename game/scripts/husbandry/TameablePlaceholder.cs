using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// YER TUTUCU (hayvan akisi, M2): evcil turlerin gercek aktorleri (gorunus, vahsi davranis) yaratik2 akisinda. Onlar
/// main'e girene kadar Homur, Dalli, Hu, Vicik, Bozuk ve Vizviz bu koddan govdeyle durur: renk, boy ve hiz
/// husbandry.json'dan; kucuk bir vahsi gezinme. Kayit: data/assets.json "yer_tutucu", pipeline/licenses.md.
/// IHusbandryHost uygular: evcilken HusbandryAgent yurutur.
/// </summary>
public partial class TameablePlaceholder : Node3D, IHusbandryHost
{
    readonly Director _d;
    readonly SpeciesDef _s;
    readonly Vector3 _homePos;
    Node3D _body;
    Vector3 _to, _goal;
    float _speed, _wait, _t, _step;
    bool _driven;

    public string SpeciesId => _s.Id;
    public float Hp { get; set; } = 1f;
    public float HpFraction => Hp;

    public TameablePlaceholder(Director d, SpeciesDef s, Vector3 home)
    {
        _d = d; _s = s; _homePos = home;
        Name = "YerTutucu_" + s.Id;
    }

    public override void _Ready()
    {
        _body = new Node3D { Name = "Body" };
        AddChild(_body);
        var c = new Color(_s.Color);
        var dark = c.Darkened(0.35f);
        float h = _s.Size;
        var kit = new MeshKit();
        switch (_s.Id)
        {
            case "vizviz": // kovan surusu: bes kucuk ari
                for (int i = 0; i < 5; i++)
                    kit.Sphere(0.09f, MeshKit.At(new Vector3(Mathf.Cos(i * 1.26f) * 0.35f, 0.6f + 0.1f * (i % 2), Mathf.Sin(i * 1.26f) * 0.35f), Vector3.Zero, new Vector3(1.3f, 1f, 1f)), i % 2 == 0 ? c : Palette.NightDeep, 8, 5);
                break;
            case "hu": // kus: govde, bas, iki kanat
                kit.Sphere(0.24f * h / 0.6f, MeshKit.At(new Vector3(0, h * 0.5f, 0), Vector3.Zero, new Vector3(1f, 1.2f, 1f)), c, 10, 7);
                kit.Sphere(0.16f * h / 0.6f, MeshKit.At(0, h * 0.95f, 0.05f), c.Lightened(0.15f), 10, 7);
                kit.Pillow(new Vector3(0.05f, 0.22f, 0.3f), MeshKit.At(0.26f, h * 0.55f, 0), dark, 0.4f);
                kit.Pillow(new Vector3(0.05f, 0.22f, 0.3f), MeshKit.At(-0.26f, h * 0.55f, 0), dark, 0.4f);
                break;
            case "vicik": // surungen: yassi uzun govde
                kit.Sphere(0.3f, MeshKit.At(new Vector3(0, 0.2f, 0), Vector3.Zero, new Vector3(1f, 0.55f, 2.0f)), c, 12, 7);
                kit.Sphere(0.16f, MeshKit.At(0, 0.25f, 0.55f), c.Lightened(0.1f), 10, 6);
                break;
            default: // dort ayak: govde, bas, dort bacak
            {
                float len = h * 0.9f, bh = h * 0.55f;
                kit.Sphere(h * 0.32f, MeshKit.At(new Vector3(0, bh, 0), Vector3.Zero, new Vector3(1f, 0.85f, len / (h * 0.45f))), c, 12, 8);
                kit.Sphere(h * 0.2f, MeshKit.At(0, bh + h * 0.15f, len * 0.62f), c.Lightened(0.1f), 10, 7);
                foreach (var (x, z) in new[] { (1, 1), (-1, 1), (1, -1), (-1, -1) })
                    kit.Pillow(new Vector3(h * 0.1f, bh, h * 0.1f), MeshKit.At(x * h * 0.2f, bh * 0.5f, z * len * 0.35f), dark, 0.3f);
                if (_s.Id == "dalli") // boynuz
                {
                    kit.Pillow(new Vector3(0.04f, 0.4f, 0.04f), MeshKit.At(0.12f, bh + h * 0.45f, len * 0.6f), Palette.OffWhite, 0.3f);
                    kit.Pillow(new Vector3(0.04f, 0.4f, 0.04f), MeshKit.At(-0.12f, bh + h * 0.45f, len * 0.6f), Palette.OffWhite, 0.3f);
                }
                break;
            }
        }
        _body.AddChild(kit.Instance());
        _speed = _s.Speed;
        _to = _homePos;
        GlobalPosition = _d.World.Ground(_homePos);
        AddToGroup("tameable");
    }

    public void HusbandrySteer(Vector3 goal, float speed, bool driven)
    {
        _driven = driven;
        _goal = goal;
        _speed = driven ? speed : _s.Speed;
        if (!driven) _to = GlobalPosition;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _t += dt;
        Vector3 target;
        float speed = _speed;
        if (_driven) target = _goal;
        else
        {
            // vahsi gezinme (yer tutucu): evinin 7 m cevresinde yavas
            var dd = _to - GlobalPosition; dd.Y = 0;
            if (dd.Length() < 0.3f)
            {
                _wait -= dt;
                if (_wait <= 0)
                {
                    _wait = (float)GD.RandRange(2.0, 5.0);
                    _to = _homePos + new Vector3((float)GD.RandRange(-7, 7), 0, (float)GD.RandRange(-7, 7));
                }
            }
            target = _to;
            speed = _s.Speed * 0.5f;
        }
        var d = target - GlobalPosition; d.Y = 0;
        bool moving = d.Length() > 0.35f;
        if (moving)
        {
            var step = d.Normalized() * Mathf.Min(d.Length(), speed * dt);
            var from = GlobalPosition;
            var to = _d.World.Ground(GlobalPosition + step);
            if (_d.Pens?.FirstHit(from, to, 0.3f * _s.Size) is WallHit h) to = _d.World.Ground(new Vector3(h.Point.X, 0, h.Point.Z));
            GlobalPosition = to;
            Rotation = new Vector3(0, Mathf.LerpAngle(Rotation.Y, Mathf.Atan2(d.X, d.Z), Mathf.Min(1f, dt * 6f)), 0);
            _step += dt * speed * 4f;
        }
        // yuruyus salinimi (iskeletsiz)
        float bob = moving ? Mathf.Abs(Mathf.Sin(_step)) * 0.06f * _s.Size : 0f;
        _body.Position = new Vector3(0, bob + (_s.Id is "vizviz" or "hu" ? Mathf.Sin(_t * 3f) * 0.08f : 0f), 0);
        _body.Rotation = new Vector3(0, _s.Id == "vizviz" ? _t * 1.5f : 0, moving ? Mathf.Sin(_step) * 0.06f : 0);
    }
}
