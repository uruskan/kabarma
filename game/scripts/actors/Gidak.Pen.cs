using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Gidak'in agil davranisi (GDD 9.1, 24; URETIM 6 M1): Bugdayla guven kazanir, dolunca oyuncuyu izler, acik
/// kapidan agila girince kayit olur; Yemlik'te Bugday varken 3 dakikada bir Folluk'a (yoksa yere) yumurtlar.
/// Duvarlar onu durdurur (acik kapi gecilir). Kurallar Sim'de (CoopRules); PenSystem bunu Gidak'in kendi
/// _Process'inden sonra cagirir. Gorunus ve Kabarma davranisi Gidak.cs'te (yaratik akisi).
/// </summary>
public partial class Gidak
{
    PenAnimal _animal;
    Vector3 _penPrev;
    bool _penPrevSet;
    float _penWanderT, _scareT, _routeT;
    PenShape _home;

    /// <summary>Kural durumu (Sim, Coop.Animals). Ilk Gidak ogretici hayvandir (GDD 24).</summary>
    public PenAnimal Animal
    {
        get
        {
            if (_animal != null) return _animal;
            var coop = _dir.State.Coop;
            _animal = new PenAnimal { Species = Def.Id, Tutorial = coop.Animals.Count(a => a.Species == Def.Id) == 0 };
            coop.Animals.Add(_animal);
            return _animal;
        }
    }

    public bool Tame => Animal.Tame;
    public bool Penned => Animal.Registered;

    /// <summary>Oyuncu bugday verdi (E). Guven dolar; eski "hemen yumurtla" yolu kapali (yumurta agilda gelir).</summary>
    public void GiveWheat()
    {
        Fed++;
        _layT = -1;
        bool tamed = _dir.State.Coop.Rules.Feed(Animal);
        Fx.Burst(GlobalPosition + Vector3.Up * 1f, Palette.Accent, 6, 1.5f, 0.1f, 0.8f, true, -2f);
        Fx.Text3D(GlobalPosition + Vector3.Up * 1.2f, tamed ? "♥ Seni izliyor" : "♥", Palette.Accent, 72, 1.2f, 1.2f);
        Sfx.Play("pickup", GlobalPosition, -6f);
        GD.Print($"GIDAK fed trust={Animal.Trust:0.00} tame={Tame}");
    }

    /// <summary>Agil kare isi: duvar, kayit, izleme, agilda gezinme, yumurta. PenSystem._Process cagirir.</summary>
    internal void PenTick(float dt, PenSystem ps)
    {
        if (!_penPrevSet) { _penPrev = GlobalPosition; _penPrevSet = true; }
        if (!Visible) { _penPrev = GlobalPosition; return; }
        var def = ps.Def;
        float radius = 0.3f * _scale;

        // 1) Duvar: bu karede gecilmez bir duvari kestiyse duvarin onunde durur.
        var hit = ps.FirstHit(_penPrev, GlobalPosition, radius);
        if (hit is WallHit h) GlobalPosition = _dir.World.Ground(new Vector3(h.Point.X, 0, h.Point.Z));
        _penPrev = GlobalPosition;
        Animal.X = GlobalPosition.X; Animal.Z = GlobalPosition.Z; // M2: agil uyeligi konumdan (Ranch)
        if (Swollen) return;

        var p = _dir.Player;
        // 2) Guven: dolmamis cubugu yakinda kosmak ve ates etmek sifirlar (GDD 9.1).
        _scareT -= dt;
        if (!Tame && Animal.Trust > 0 && p.GlobalPosition.DistanceTo(GlobalPosition) < def.ScareRadius && _scareT <= 0
            && (Input.IsActionPressed("sprint") && p.MoveOverride == null && p.Velocity.Length() > p.WalkSpeed * 1.1f || Capa.IsWeapon(_dir.Capa.Current) && _dir.Capa.UsingNow))
        {
            _dir.State.Coop.Rules.Scare(Animal);
            _scareT = 1f;
            Fx.Text3D(GlobalPosition + Vector3.Up * 1.1f, "!", Palette.Warning, 72, 0.8f, 1f);
        }

        // 3) Kayit: evcil Gidak agilin icine girdi.
        if (Tame && !Penned && ps.PenAt(GlobalPosition) is PenShape pen)
        {
            // M2: agil alani turlerin alanlari toplamidir (GDD 9.3); ilk yumurta kurali CoopRules'ta (GDD 24).
            var ranch = _dir.State.Ranch;
            bool room = ranch.UsedArea(pen, Animal) + ranch.Rules.Species(Def.Id).Area <= pen.Area + 0.01f;
            if (room && _dir.State.Coop.Rules.Register(Animal, pen, 0, out bool egg))
            {
                _home = pen;
                GD.Print($"GIDAK penned area={pen.Area:0.0} cells={pen.Cells(def.CellArea)}");
                Fx.Text3D(GlobalPosition + Vector3.Up * 1.2f, "Kayıt", Palette.CoopBlue, 64, 1.2f, 1.4f);
                Events.RaiseAnimalPenned(Def.Id);
                if (egg) ps.Lay(def.Animals.First(a => a.Id == Def.Id).Product, GlobalPosition - GlobalBasis.Z * 0.35f, pen);
            }
        }

        // 4) Hedef
        if (Penned)
        {
            _home = ps.Pens.FirstOrDefault(x => x.Contains(new P2(GlobalPosition.X, GlobalPosition.Z))) ?? _home;
            _penWanderT -= dt;
            if (_home != null && _penWanderT <= 0)
            {
                _penWanderT = (float)GD.RandRange(2.5, 5.0);
                var c = _home.Centroid;
                for (int i = 0; i < 8; i++)
                {
                    var q = new P2(c.X + (float)GD.RandRange(-2.5, 2.5), c.Z + (float)GD.RandRange(-2.5, 2.5));
                    if (_home.Contains(q) && _home.Polygon.Count > 0) { _wanderTo = new Vector3(q.X, 0, q.Z); break; }
                }
            }
            _wanderT = 999f; // Gidak.cs'in serbest gezinmesi agildan cikarmasin
            // Yumurta ve diger urunler M2'de Ranch.Tick'ten (RanchSystem: Folluk ya da yer; mutluluk ve gen).
        }
        else if (Tame)
        {
            // Oyuncuyu izler; arada duvar varsa agil koselerinden ve acik kapidan dolasan yolu tutar.
            var target = p.GlobalPosition + (GlobalPosition - p.GlobalPosition).Normalized() * def.FollowDistance;
            // Oyuncu agildaysa ya da izleme noktasi duvarin obur yanina dustuyse oyuncunun kendisine yol bulur (pesinden iceri girer).
            if (ps.PenAt(p.GlobalPosition) != null || ps.FirstHit(p.GlobalPosition, target, radius) != null) target = p.GlobalPosition;
            _routeT -= dt;
            if (_routeT <= 0 || GlobalPosition.DistanceTo(_wanderTo) < 0.5f)
            {
                _routeT = 0.25f;
                _wanderTo = ps.Route(GlobalPosition, target, radius);
            }
            _wanderT = 999f;
        }
    }
}
