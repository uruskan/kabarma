using System.Numerics;

namespace Kabarma.Sim;

/// <summary>
/// Poz kutuphanesinin motordan bagimsiz cekirdegi (URETIM §2.2 E.3): aynalama ve anahtarlar arasi yumusatma.
/// Godot tarafi (PoseLibrary.cs) bu ornekleri iskelete aktarir; burada kural ve test.
/// </summary>
public static class Poses
{
    /// <summary>SANAT-SES §4: tur basina sekiz klip (bekleme, yurume, kosu, iki saldiri, darbe alma, yeme, dinlenme).</summary>
    public static readonly IReadOnlyList<string> Slots = new[] { "idle", "walk", "run", "attack1", "attack2", "hit", "eat", "rest" };

    /// <summary>Dongulu klipler: klip bitince basa sarar.</summary>
    public static bool Loops(string slot) => slot is "idle" or "walk" or "run" or "eat" or "rest";

    public static string MirrorRole(string role) =>
        role.EndsWith("_l", StringComparison.Ordinal) ? role[..^2] + "_r" :
        role.EndsWith("_r", StringComparison.Ordinal) ? role[..^2] + "_l" : role;

    /// <summary>Sol taraftaki donus sag tarafa: X ayni, Y ve Z ters (karakterin YZ duzlemine gore ayna).</summary>
    public static Vector3 MirrorRot(Vector3 v) => new(v.X, -v.Y, -v.Z);

    /// <summary>Bir anahtar listesi: [t, x, y, z].</summary>
    public sealed record Track(IReadOnlyList<(float t, Vector3 v)> Keys);

    /// <summary>
    /// Klibin butun rol izleri, aynalama uygulanmis: "phase" -> _r izi yarim dongu kaydirilir, "same" -> ayni anda,
    /// "none" -> yalniz yazilanlar. Acikca yazilmis _r izi her zaman kazanir.
    /// </summary>
    public static Dictionary<string, Track> Expand(PoseClipDef clip)
    {
        var res = new Dictionary<string, Track>();
        foreach (var (role, keys) in clip.Tracks) res[role] = ToTrack(keys);
        if (clip.Mirror is "phase" or "same")
        {
            foreach (var (role, keys) in clip.Tracks)
            {
                if (!role.EndsWith("_l", StringComparison.Ordinal)) continue;
                var other = MirrorRole(role);
                if (clip.Tracks.ContainsKey(other)) continue;
                float shift = clip.Mirror == "phase" ? clip.Length * 0.5f : 0f;
                var src = ToTrack(keys).Keys;
                var mirrored = src.Select(k => (t: Wrap(k.t + shift, clip.Length, clip.Loop), v: MirrorRot(k.v))).OrderBy(k => k.t).ToList();
                res[other] = new Track(mirrored);
            }
        }
        return res;
    }

    public static Track ToTrack(List<List<float>> keys) =>
        new(keys.Select(k => (t: k[0], v: new Vector3(k.Count > 1 ? k[1] : 0, k.Count > 2 ? k[2] : 0, k.Count > 3 ? k[3] : 0))).OrderBy(k => k.t).ToList());

    static float Wrap(float t, float length, bool loop)
    {
        if (!loop || length <= 0) return Math.Clamp(t, 0, length);
        t %= length;
        return t < 0 ? t + length : t;
    }

    /// <summary>
    /// t anindaki deger: Catmull-Rom. Dongulu klipte son anahtardan ilkine sarar (son anahtar ile uzunluk arasi da
    /// yumusak); dongusuzde uclar sabit. Tek anahtar sabit, anahtarsiz iz sifir.
    /// </summary>
    public static Vector3 Sample(Track track, float t, float length, bool loop)
    {
        var k = track.Keys;
        if (k.Count == 0) return Vector3.Zero;
        if (k.Count == 1) return k[0].v;
        t = Wrap(t, length, loop);
        if (!loop)
        {
            if (t <= k[0].t) return k[0].v;
            if (t >= k[^1].t) return k[^1].v;
        }
        int n = k.Count;
        // t'yi iceren aralik [i, i+1] (dongude son aralik son anahtardan ilk anahtar + uzunluga)
        int i = -1;
        for (int j = 0; j < n; j++) if (k[j].t <= t) i = j;
        if (i < 0) i = loop ? n - 1 : 0; // dongude ilk anahtardan once: son aralik
        float T(int idx) // dongude sanal zaman
        {
            if (!loop) return k[Math.Clamp(idx, 0, n - 1)].t;
            int w = ((idx % n) + n) % n;
            int lap = (int)Math.Floor(idx / (double)n);
            return k[w].t + lap * length;
        }
        Vector3 V(int idx) => loop ? k[((idx % n) + n) % n].v : k[Math.Clamp(idx, 0, n - 1)].v;
        float tt = t;
        if (loop && t < k[0].t) tt = t + length; // son aralik: i = n-1, t1 = k[0].t + uzunluk
        float t1 = T(i), t2 = T(i + 1);
        float u = t2 > t1 ? Math.Clamp((tt - t1) / (t2 - t1), 0f, 1f) : 0f;
        var p0 = V(i - 1); var p1 = V(i); var p2 = V(i + 1); var p3 = V(i + 2);
        // Uniform Catmull-Rom: anahtarda gecer, aralarda yumusak.
        float u2 = u * u, u3 = u2 * u;
        return 0.5f * (2f * p1 + (-p0 + p2) * u + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u2 + (-p0 + 3f * p1 - 3f * p2 + p3) * u3);
    }
}
