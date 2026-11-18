using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Kartlar akisi (M3, GDD 15, ICERIK 8): kart elinin oyundaki karsiligi. Kurallar Sim'de (CardHand); burada yalniz
/// uygulanir: yorunge silahlari, patlamalar, can, kalkan, kalkma, hiz, yenileme ve surgun girdisi.
/// GECICI: hasar ve durumlar savas akisinin Combat/Status sozlesmesi gelene kadar Zipzip.Hit, BurnT ve FreezeT ile.
/// Godot'da henuz karsiligi olmayanlar (Sim'de testli, baska akisi bekliyor): Delici, Uzun Namlu, Ikiz Baslik, Yanki,
/// Saçmalık, Dolu (capa: mermi), ekip kartlari (savunma, savas-hayvan, kabarma3), Sadik Golge, beden boyu.
/// </summary>
public partial class Director
{
    float _baseMaxHp = -1f;
    bool _banishMode;
    bool _cardsDown;
    readonly List<(float t, Vector3 at, float dmg, float radius, bool swell)> _blasts = new();

    /// <summary>Director.Night TickPhase'ten (gece, kart secimi ve hiz satirindan sonra).</summary>
    void TickCards(float dt)
    {
        var n = Night;
        if (n == null || n.Phase == NightPhase.Dawn || Player == null) return;
        var h = n.Hand;
        if (_baseMaxHp < 0) _baseMaxHp = Player.MaxHp;
        float maxHp = h.MaxHp(_baseMaxHp);
        if (!Mathf.IsEqualApprox(Player.MaxHp, maxHp))
        {
            float frac = Player.Hp / Mathf.Max(1f, Player.MaxHp);
            Player.MaxHp = maxHp;
            Player.Hp = Mathf.Min(maxHp, frac * maxHp);
        }
        h.PlayerHpFrac = Player.Hp / Mathf.Max(1f, Player.MaxHp);
        h.Airborne = !Player.IsOnFloor();
        Player.SpeedMult = h.SpeedMult; // Cevik, Kabarik Beden, Ufak Tefek, Hasat Senligi
        if (!Player.Down && h.RegenPerSecond > 0) Player.Hp = Mathf.Min(Player.MaxHp, Player.Hp + h.RegenPerSecond * dt);

        // Ikinci Nefes: yere dusunce kendi kendine kalkar.
        if (Player.Down && !_cardsDown && h.TryRevive())
        {
            Player.NetRevive(Player.MaxHp * 0.5f);
            Hud.Big("İKİNCİ NEFES", Palette.MayaTeal, 1.6f);
        }
        _cardsDown = Player.Down;

        foreach (var f in h.Tick(dt)) FireOrbital(f);
        TickBlasts(dt);
        CardInput();
    }

    void CardInput()
    {
        var n = Night;
        if (n?.Offer == null) { _banishMode = false; return; }
        if (Input.IsActionJustPressed("card_reroll") && n.Hand.Reroll())
        {
            Sfx.Play("levelup", null, -10f);
            GD.Print($"NIGHT reroll left={n.Hand.RerollsLeft}");
        }
        if (Input.IsActionJustPressed("card_banish") && n.Hand.BanishesLeft > 0)
        {
            _banishMode = !_banishMode;
            Hud.Hint(_banishMode ? "Sürgün: sürgün edilecek kartı seç" : "Sürgün iptal", 1.5f);
        }
    }

    /// <summary>ChooseCard'dan: surgun kipindeyse secilen kart surgun edilir. true: secim yerine surgun yapildi.</summary>
    bool TryBanishInstead(int i)
    {
        if (!_banishMode) return false;
        _banishMode = false;
        var n = Night;
        var id = n?.Offer != null && i < n.Offer.Cards.Count ? n.Offer.Cards[i].Id : null;
        if (n != null && n.Hand.Banish(i)) { GD.Print($"NIGHT banish {id} left={n.Hand.BanishesLeft}"); Hud.Hint("Kart bu gece için sürgün edildi", 1.5f); }
        else Hud.Hint("Bu kart sürgün edilemez", 1.5f);
        RefreshOffer();
        return true;
    }

    public bool BanishMode => _banishMode;
    /// <summary>Yorunge silahlarinin bu gece vurdugu hedef sayisi (sinama).</summary>
    public int OrbitalHits { get; private set; }

    /// <summary>Yorunge atesi: hedefleri oyuncunun cevresinden secer, Zipzip'lere vurur (GECICI hasar yolu).</summary>
    void FireOrbital(OrbitalFire f)
    {
        var p = Player.GlobalPosition;
        var alive = Zipzip.All.Where(z => !z.Dead && z.Center.DistanceTo(p) <= f.Radius).OrderBy(z => z.Center.DistanceTo(p)).ToList();
        var col = OrbitalColor(f.Def);
        if (f.Def.Param("taunt") > 0)
        {
            foreach (var z in alive) z.LookAt(p with { Y = z.GlobalPosition.Y }, Vector3.Up);
            Fx.Burst(p + Vector3.Up, col, 12, 3f, 0.08f, 0.5f, true, -1f);
            return;
        }
        if (alive.Count == 0) return;
        if (f.Def.Param("devour") > 0)
        {
            var prey = alive.FirstOrDefault(z => !z.Swollen) ?? alive[0];
            prey.Hit(prey.Hp + 1f, false, Vector3.Zero, 0, false);
            Fx.Burst(prey.Center, col, 16, 3f, 0.1f, 0.6f, true, -1f);
            return;
        }
        IEnumerable<Zipzip> targets;
        if (f.Def.Param("area") > 0)
        {
            var center = f.Def.Param("crowd") > 0 ? alive.OrderByDescending(z => alive.Count(o => o.Center.DistanceTo(z.Center) < 4f)).First().Center
                       : f.Def.Param("behind") > 0 ? p - Player.AimDir with { Y = 0 } * 1.5f : p;
            targets = Zipzip.All.Where(z => !z.Dead && z.Center.DistanceTo(center) <= (f.Def.Param("crowd") > 0 ? 4f : f.Radius)).ToList();
            Fx.Burst(center + Vector3.Up * 0.3f, col, 18, 4f, 0.1f, 0.5f, true, -1f);
        }
        else targets = alive.Take(Mathf.Max(1, f.Count));
        float healed = 0;
        foreach (var z in targets.ToList())
        {
            var dir = (z.Center - p) with { Y = 0 };
            z.Hit(f.Damage, false, dir.Normalized(), f.Def.Param("pull") > 0 ? -3f : 1f, false);
            if (f.Def.Param("poison") > 0) z.BurnT = Mathf.Max(z.BurnT, 2f); // GECICI: Zehir yerine Yanik (savas sozlesmesi)
            healed += f.Damage * f.Def.Param("drain");
            OrbitalHits++;
            Fx.Burst(z.Center, col, 6, 2.5f, 0.06f, 0.35f, true, -1f);
        }
        if (healed > 0) Player.Hp = Mathf.Min(Player.MaxHp, Player.Hp + healed);
    }

    static Color OrbitalColor(CardDef c) => c.Class == "asi" ? Palette.MayaViolet : Palette.MayaTeal;

    /// <summary>Capa'nin her atisi (Capa.Weapons): Patlar Uc'un 5. atisi nisan noktasinda patlar.</summary>
    public void OnCardShot(Vector3 aim)
    {
        var plan = Night?.Hand.NextShot();
        if (plan is not { Explodes: true } s || Night.Phase == NightPhase.Dawn) return;
        _blasts.Add((0f, aim, s.ExplodeDamage * Data.Tool.Weapon.Damage, s.ExplodeRadius, false));
    }

    /// <summary>Capa isabetinden sonra (Capa.WeaponHit): can emme, Sok sicramasi, Zehir.</summary>
    public void OnCardHit(Zipzip z)
    {
        var n = Night;
        if (n == null || n.Phase == NightPhase.Dawn) return;
        var hit = n.LastHit;
        if (hit.Heal > 0) Player.Hp = Mathf.Min(Player.MaxHp, Player.Hp + hit.Heal);
        if (hit.Poison) z.BurnT = Mathf.Max(z.BurnT, 3f); // GECICI: Zehir (savas Status gelene kadar)
        if (hit.Shock)
        {
            int jumps = hit.UnlimitedChain ? 99 : 2;
            var from = z.Center;
            var done = new HashSet<Zipzip> { z };
            for (int j = 0; j < jumps; j++)
            {
                var next = Zipzip.All.Where(e => !e.Dead && !done.Contains(e) && e.Center.DistanceTo(from) < 5f).OrderBy(e => e.Center.DistanceTo(from)).FirstOrDefault();
                if (next == null) break;
                done.Add(next);
                next.Hit(Mathf.Round(hit.Damage * 0.5f), false, Vector3.Zero, 0, false);
                next.FreezeT = Mathf.Max(next.FreezeT, 0.3f); // kisa sersemletme
                Fx.Burst(next.Center, Palette.Warning, 6, 2.5f, 0.05f, 0.3f, true, -1f);
                from = next.Center;
            }
        }
    }

    /// <summary>OnZipzipKilled'dan: Geri Sayim ve Maya Bombasi.</summary>
    void OnCardKill(Zipzip z)
    {
        var k = Night?.LastKill ?? default;
        if (k.BlastDamage > 0) _blasts.Add((k.BlastDelay, z.Center, k.BlastDamage * Data.Tool.Weapon.Damage, k.BlastRadius, k.ChainSwell));
    }

    void TickBlasts(float dt)
    {
        for (int i = _blasts.Count - 1; i >= 0; i--)
        {
            var b = _blasts[i];
            b.t -= dt;
            if (b.t > 0) { _blasts[i] = b; continue; }
            _blasts.RemoveAt(i);
            Fx.Burst(b.at, b.swell ? Palette.MayaViolet : Palette.Warning, 22, 5f, 0.12f, 0.6f, true, -1f);
            Sfx.Play("hit", b.at, -6f);
            foreach (var z in Zipzip.All.Where(e => !e.Dead && e.Center.DistanceTo(b.at) <= b.radius).ToList())
                z.Hit(Mathf.Round(b.dmg), false, (z.Center - b.at) with { Y = 0 }, 2f, false);
        }
    }

    /// <summary>Player.Hurt'ten: Ufak Tefek, Kabuk, Dikenli. Donus: oyuncuya kalan hasar.</summary>
    public float CardGuard(float dmg, Vector3 from)
    {
        var n = Night;
        if (n == null || n.Phase == NightPhase.Dawn) return dmg;
        var g = n.Hand.Guard(dmg);
        if (g.Reflected > 0)
        {
            var src = Zipzip.All.Where(z => !z.Dead).OrderBy(z => z.Center.DistanceTo(from)).FirstOrDefault();
            if (src != null && src.Center.DistanceTo(from) < 3f) src.Hit(Mathf.Round(g.Reflected), false, Vector3.Zero, 0, false);
        }
        if (g.Dodged) Hud.Hint("Kaçtın", 0.6f);
        return g.Taken;
    }

    /// <summary>Safakta (BeginDawn): can tavani tabana doner.</summary>
    void CardsDawn()
    {
        _blasts.Clear();
        _banishMode = false;
        if (_baseMaxHp > 0 && Player != null) { Player.MaxHp = _baseMaxHp; Player.Hp = Mathf.Min(Player.Hp, _baseMaxHp); }
    }
}
