namespace Kabarma.Game;

/// <summary>
/// Ortak oyun (ortak akisi): bayilma ve sok tokadi (GDD 3.6). Ortak oyunda can sifirlaninca serilme suresi 30 sn olur
/// (net.json downed.seconds); bir arkadas tokatlarsa <see cref="NetRevive"/> ile %30 canla bulundugu yerde kalkar, sure
/// dolarsa eski yol: olum, dogma noktasinda tam canla. Tek oyuncuda hicbiri calismaz.
/// </summary>
public partial class Player
{
    /// <summary>Player.Hurt olum dalindan cagrilir (tek satirlik kanca).</summary>
    void NetDowned()
    {
        if (GetParent() is Director d && d.Net != null) _downT = d.Data.Net.Downed.Seconds;
    }

    /// <summary>Baygin oyuncuyu kaldirir: olum sayilmaz, bulundugu yerde, verilen canla.</summary>
    public void NetRevive(float hp)
    {
        if (!Down) return;
        _dead = false;
        Hp = hp;
        EndDown();
        Fx.Burst(GlobalPosition + Godot.Vector3.Up * 1.2f, Palette.MayaTeal, 24, 4f, 0.08f, 0.6f);
        Sfx.Play("hit", GlobalPosition, -4f); // yer tutucu: tokat sesi (ses akisi, SANAT-SES "tokat")
    }

    /// <summary>Kalan baygin sure (oyuncu listesi, HUD).</summary>
    public float DownLeft => Down ? _downT : 0f;
}
