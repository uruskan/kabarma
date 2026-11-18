using System.Buffers.Binary;
using System.Text;

namespace Kabarma.Sim;

/// <summary>Bozuk ya da kisa mesaj: alan taraf mesaji atar (gerekirse esi duser).</summary>
public sealed class NetFormatException : Exception
{
    public NetFormatException(string m) : base(m) { }
}

/// <summary>
/// Ag mesaji yazicisi (TEKNIK 5.1: kendi ince, ikili, surumlu katmanimiz). Kucuk endian; degisken boylu tamsayi;
/// konum 24 bit sabit noktali (<see cref="NetQuant"/>). Godot tipi kullanmaz: ayni kod testte ve oyunda.
/// </summary>
public sealed class NetWriter
{
    byte[] _b;
    int _n;

    public NetWriter(int capacity = 256) { _b = new byte[Math.Max(16, capacity)]; }

    public int Length => _n;
    public void Reset() => _n = 0;
    public byte[] ToArray() => _b.AsSpan(0, _n).ToArray();
    public ReadOnlySpan<byte> Span => _b.AsSpan(0, _n);

    void Grow(int k)
    {
        if (_n + k > _b.Length) Array.Resize(ref _b, Math.Max(_b.Length * 2, _n + k));
    }

    public void U8(byte v) { Grow(1); _b[_n++] = v; }
    public void Bool(bool v) => U8(v ? (byte)1 : (byte)0);
    public void I8(sbyte v) => U8(unchecked((byte)v));
    public void U16(ushort v) { Grow(2); BinaryPrimitives.WriteUInt16LittleEndian(_b.AsSpan(_n), v); _n += 2; }
    public void U32(uint v) { Grow(4); BinaryPrimitives.WriteUInt32LittleEndian(_b.AsSpan(_n), v); _n += 4; }
    public void U64(ulong v) { Grow(8); BinaryPrimitives.WriteUInt64LittleEndian(_b.AsSpan(_n), v); _n += 8; }
    public void F32(float v) { Grow(4); BinaryPrimitives.WriteSingleLittleEndian(_b.AsSpan(_n), v); _n += 4; }

    /// <summary>24 bit isaretli tamsayi (-8.388.608 .. 8.388.607).</summary>
    public void I24(int v)
    {
        v = Math.Clamp(v, -(1 << 23), (1 << 23) - 1);
        Grow(3);
        _b[_n++] = (byte)v; _b[_n++] = (byte)(v >> 8); _b[_n++] = (byte)(v >> 16);
    }

    public void VarU(uint v)
    {
        while (v >= 0x80) { U8((byte)(v | 0x80)); v >>= 7; }
        U8((byte)v);
    }

    public void VarI(int v) => VarU((uint)((v << 1) ^ (v >> 31)));

    public void Bytes(ReadOnlySpan<byte> s) { Grow(s.Length); s.CopyTo(_b.AsSpan(_n)); _n += s.Length; }

    /// <summary>Boyu onde (degisken boy) bayt dizisi.</summary>
    public void Blob(ReadOnlySpan<byte> s) { VarU((uint)s.Length); Bytes(s); }

    public void Str(string? s)
    {
        var bytes = Encoding.UTF8.GetBytes(s ?? "");
        Blob(bytes);
    }

    /// <summary>Konum: eksen basina 24 bit, metrede <paramref name="perMetre"/> adim.</summary>
    public void Pos(float x, float y, float z, int perMetre)
    {
        I24(NetQuant.Pos(x, perMetre)); I24(NetQuant.Pos(y, perMetre)); I24(NetQuant.Pos(z, perMetre));
    }

    /// <summary>Aci (radyan), 16 bit tam tur.</summary>
    public void Angle16(float rad) => U16(NetQuant.Angle16(rad));
    /// <summary>Aci (radyan), 8 bit tam tur.</summary>
    public void Angle8(float rad) => U8(NetQuant.Angle8(rad));
}

/// <summary>Ag mesaji okuyucu. Tasma <see cref="NetFormatException"/> atar.</summary>
public sealed class NetReader
{
    readonly byte[] _b;
    readonly int _end;
    int _p;

    public NetReader(byte[] b, int offset = 0, int length = -1)
    {
        _b = b;
        _p = offset;
        _end = length < 0 ? b.Length : offset + length;
        if (_end > b.Length || offset < 0) throw new NetFormatException("bad range");
    }

    public int Left => _end - _p;
    public bool AtEnd => _p >= _end;

    void Need(int k) { if (_p + k > _end) throw new NetFormatException($"short read {k} at {_p}/{_end}"); }

    public byte U8() { Need(1); return _b[_p++]; }
    public bool Bool() => U8() != 0;
    public sbyte I8() => unchecked((sbyte)U8());
    public ushort U16() { Need(2); var v = BinaryPrimitives.ReadUInt16LittleEndian(_b.AsSpan(_p)); _p += 2; return v; }
    public uint U32() { Need(4); var v = BinaryPrimitives.ReadUInt32LittleEndian(_b.AsSpan(_p)); _p += 4; return v; }
    public ulong U64() { Need(8); var v = BinaryPrimitives.ReadUInt64LittleEndian(_b.AsSpan(_p)); _p += 8; return v; }
    public float F32() { Need(4); var v = BinaryPrimitives.ReadSingleLittleEndian(_b.AsSpan(_p)); _p += 4; return v; }

    public int I24()
    {
        Need(3);
        int v = _b[_p] | (_b[_p + 1] << 8) | (_b[_p + 2] << 16);
        _p += 3;
        return (v << 8) >> 8; // isaret uzatma
    }

    public uint VarU()
    {
        uint v = 0;
        for (int shift = 0; shift < 35; shift += 7)
        {
            byte b = U8();
            v |= (uint)(b & 0x7F) << shift;
            if (b < 0x80) return v;
        }
        throw new NetFormatException("varint too long");
    }

    public int VarI() { uint u = VarU(); return (int)(u >> 1) ^ -(int)(u & 1); }

    public byte[] Bytes(int n)
    {
        if (n < 0) throw new NetFormatException("negative length");
        Need(n);
        var r = _b.AsSpan(_p, n).ToArray();
        _p += n;
        return r;
    }

    public byte[] Blob() => Bytes((int)VarU());

    public string Str()
    {
        int n = (int)VarU();
        Need(n);
        var s = Encoding.UTF8.GetString(_b, _p, n);
        _p += n;
        return s;
    }

    public (float x, float y, float z) Pos(int perMetre) =>
        (NetQuant.Unpos(I24(), perMetre), NetQuant.Unpos(I24(), perMetre), NetQuant.Unpos(I24(), perMetre));

    public float Angle16() => NetQuant.Unangle16(U16());
    public float Angle8() => NetQuant.Unangle8(U8());
}

/// <summary>Nicemleme: konum (24 bit, sabit nokta) ve aci. TEKNIK 5.3 bayt butcesi.</summary>
public static class NetQuant
{
    const float Tau = MathF.PI * 2f;

    public static int Pos(float metres, int perMetre) =>
        (int)Math.Clamp(MathF.Round(metres * perMetre), -(1 << 23), (1 << 23) - 1);

    public static float Unpos(int q, int perMetre) => q / (float)perMetre;

    /// <summary>En kotu yuvarlama hatasi, m (yarim adim).</summary>
    public static float PosError(int perMetre) => 0.5f / perMetre;

    static float Wrap(float rad)
    {
        rad %= Tau;
        return rad < 0 ? rad + Tau : rad;
    }

    public static ushort Angle16(float rad) => (ushort)((int)MathF.Round(Wrap(rad) / Tau * 65536f) & 0xFFFF);
    public static float Unangle16(ushort q) => q / 65536f * Tau;
    public static byte Angle8(float rad) => (byte)((int)MathF.Round(Wrap(rad) / Tau * 256f) & 0xFF);
    public static float Unangle8(byte q) => q / 256f * Tau;

    /// <summary>Bakis egimi (radyan, -1,57..1,57) tek isaretli bayta.</summary>
    public static sbyte Pitch(float rad) => (sbyte)Math.Clamp((int)MathF.Round(rad / 1.6f * 127f), -127, 127);
    public static float Unpitch(sbyte q) => q / 127f * 1.6f;
}

/// <summary>FNV-1a 64: bolum ozeti (TEKNIK 5.3: 10 sn'de bir ozet dogrulamasi) ve dunya kimligi.</summary>
public static class NetHash
{
    public const ulong Seed = 14695981039346656037UL;

    public static ulong Of(ReadOnlySpan<byte> b, ulong h = Seed)
    {
        foreach (var x in b) { h ^= x; h *= 1099511628211UL; }
        return h;
    }

    public static ulong Mix(ulong h, ulong v)
    {
        for (int i = 0; i < 8; i++) { h ^= (byte)(v >> (i * 8)); h *= 1099511628211UL; }
        return h;
    }

    public static ulong Mix(ulong h, float v) => Mix(h, (ulong)BitConverter.SingleToUInt32Bits(v));
    public static ulong Mix(ulong h, string s) => Of(Encoding.UTF8.GetBytes(s), h);
}
