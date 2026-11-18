using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Director: Bereket Holding (GDD §19; holding, M3). Durum ve kayit Sim'de (GameState.Holding, bolum "holding");
/// burada gece ve safak olaylari Sim'e baglanir ve <see cref="HoldingWorld"/> kurulur. Gece/safak yalniz host'ta isler.
///   --test=holding   HoldingTest: kamp, Perde, baskin, kervan pususu, onlarin baskini, ganimet (HoldingTest.cs)
/// </summary>
public partial class Director
{
    public HoldingWorld Holding { get; private set; }

    /// <summary>_Ready'den bir kez, InitNet ve InitFlow'dan sonra (ag bolumleri ve akis alani hazir).</summary>
    void InitHolding()
    {
        Holding = new HoldingWorld(this);
        AddChild(Holding);
        Events.NightStarted += OnHoldingNight;
        Events.Dawn += OnHoldingDawn;
        if (Main.Args.ContainsKey("holdingtest")) AddChild(new HoldingTest(this));
    }

    /// <summary>Kabarma gecesi: gercekse Perde iner; Husumet 50+ kamplar baskin ekibi gonderir (kamp yonunden, tapunun disinda dogar).</summary>
    void OnHoldingNight() => HoldingNight(State.RealKabarma);

    /// <summary>Gece girisi (sinama gercek Kabarma'yi dogrudan acar).</summary>
    public void HoldingNight(bool real)
    {
        if (NetClient) return; // ortak: host'ta
        var raids = State.Holding.OnNightStart(real);
        var def = Data.Holding.Raid;
        foreach (var (camp, units) in raids)
        {
            var dir = new Vector3(camp.X - OcakPos.X, 0, camp.Z - OcakPos.Z);
            dir = dir.LengthSquared() < 1f ? Vector3.Forward : dir.Normalized();
            var side = new Vector3(-dir.Z, 0, dir.X);
            var start = OcakPos + dir * def.SpawnDistance;
            for (int i = 0; i < units.Count; i++)
            {
                var p = start + side * ((i - (units.Count - 1) * 0.5f) * def.EntryOffset * 0.5f) + dir * (i % 2) * 2f;
                units[i].X = p.X; units[i].Z = p.Z;
            }
        }
    }

    public void OnHoldingDawn()
    {
        if (NetClient) return;
        State.Holding.OnDawn();
        State.Holding.OnKabarmaEnded(State.RealKabarma, State.Calendar.Day);
    }

    /// <summary>Capa mermileri icin tek giris (Capa.Weapons.cs'teki satirlar).</summary>
    public bool HoldingProjectileHit(Vector3 from, Vector3 to, float radius, float damage, System.Collections.Generic.ISet<Node3D> already = null) =>
        Holding != null && Holding.ProjectileHit(from, to, radius, damage, already);
}
