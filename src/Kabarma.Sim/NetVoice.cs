namespace Kabarma.Sim;

/// <summary>
/// Sesli sohbet (GDD 22.1: yakinlik sesi, uzak mesafe icin telsiz tusu; TEKNIK 5.4). Steam varken Steam Voice sikistirir;
/// ENet'te G.711 mu-law (8 bit, 16 kHz: 16 KB/sn konusurken). Uzakliga gore kazanc burada; konumlu ses kaynagi Godot'ta.
/// Cerceve: [konusan es (1), telsiz (1), sira (2), kodek (1), veri].
/// </summary>
public static class NetVoice
{
    public const byte CodecMuLaw = 1, CodecSteam = 2;

    /// <summary>Uzakliga gore kazanc: tam ses yakininda 1, en uzakta 0, arasi yumusak (karesel) dusus. Telsiz sabit.</summary>
    public static float Gain(float distance, bool radio, NetVoiceDef def)
    {
        if (radio) return def.RadioGain;
        if (distance <= def.FullVolumeDistance) return 1f;
        if (distance >= def.MaxDistance) return 0f;
        float t = (distance - def.FullVolumeDistance) / Math.Max(0.001f, def.MaxDistance - def.FullVolumeDistance);
        return (1f - t) * (1f - t);
    }

    /// <summary>Kapi: cercevenin tepe genligi esigin altindaysa gonderilmez.</summary>
    public static bool Gate(ReadOnlySpan<float> pcm, NetVoiceDef def)
    {
        float peak = 0;
        foreach (var s in pcm) peak = Math.Max(peak, Math.Abs(s));
        return peak >= def.Gate;
    }

    /// <summary>Dogrusal yeniden ornekleme (mikrofon 44,1/48 kHz → 16 kHz, alici 16 kHz → karistirici).</summary>
    public static float[] Resample(ReadOnlySpan<float> src, int fromRate, int toRate)
    {
        if (src.Length == 0 || fromRate == toRate) return src.ToArray();
        int n = Math.Max(1, (int)((long)src.Length * toRate / fromRate));
        var dst = new float[n];
        double step = (double)fromRate / toRate;
        for (int i = 0; i < n; i++)
        {
            double p = i * step;
            int a = (int)p;
            int b = Math.Min(a + 1, src.Length - 1);
            float f = (float)(p - a);
            dst[i] = src[Math.Min(a, src.Length - 1)] * (1 - f) + src[b] * f;
        }
        return dst;
    }

    const int Bias = 0x84, Clip = 32635;

    public static byte MuLaw(float sample)
    {
        int s = (int)Math.Clamp(sample * 32767f, -32768f, 32767f);
        int sign = (s >> 8) & 0x80;
        if (sign != 0) s = -s;
        if (s > Clip) s = Clip;
        s += Bias;
        int exponent = 7;
        for (int mask = 0x4000; (s & mask) == 0 && exponent > 0; mask >>= 1) exponent--;
        int mantissa = (s >> (exponent + 3)) & 0x0F;
        return (byte)~(sign | (exponent << 4) | mantissa);
    }

    public static float UnMuLaw(byte b)
    {
        int u = ~b & 0xFF;
        int sign = u & 0x80, exponent = (u >> 4) & 0x07, mantissa = u & 0x0F;
        int s = ((mantissa << 3) + Bias) << exponent;
        s -= Bias;
        return (sign != 0 ? -s : s) / 32768f;
    }

    public static byte[] Encode(ReadOnlySpan<float> pcm)
    {
        var b = new byte[pcm.Length];
        for (int i = 0; i < pcm.Length; i++) b[i] = MuLaw(pcm[i]);
        return b;
    }

    public static float[] Decode(ReadOnlySpan<byte> data)
    {
        var f = new float[data.Length];
        for (int i = 0; i < data.Length; i++) f[i] = UnMuLaw(data[i]);
        return f;
    }

    public static byte[] Packet(byte peer, bool radio, ushort seq, byte codec, ReadOnlySpan<byte> data)
    {
        var w = NetPackets.Begin(NetMsg.Voice, data.Length + 8);
        w.U8(peer); w.Bool(radio); w.U16(seq); w.U8(codec); w.Blob(data);
        return w.ToArray();
    }

    public static (byte peer, bool radio, ushort seq, byte codec, byte[] data) Read(NetReader r) =>
        (r.U8(), r.Bool(), r.U16(), r.U8(), r.Blob());
}
