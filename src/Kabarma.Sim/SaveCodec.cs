using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace Kabarma.Sim;

/// <summary>
/// Kayit kapsayicisi (TEKNIK 9): "KBRS" + bicim surumu + JSON bildirim + ikili bolumler. Her bolum: kimlik, surum,
/// ham boy, CRC32, sikistirilmis (Deflate) veri. Bildirim basta durur: menu yalniz onu okur (ReadManifest).
/// </summary>
public static class SaveCodec
{
    static readonly byte[] Magic = "KBRS"u8.ToArray();
    public const int Format = 1;

    public static byte[] Encode(SaveGame g)
    {
        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
        {
            w.Write(Magic);
            w.Write((ushort)Format);
            var man = JsonSerializer.SerializeToUtf8Bytes(g.Manifest, SaveJson.Options);
            w.Write(man.Length);
            w.Write(man);
            w.Write(g.Sections.Count);
            foreach (var (id, b) in g.Sections)
            {
                w.Write(id);
                w.Write(b.Version);
                w.Write(b.Data.Length);
                w.Write(Crc32.Of(b.Data));
                var z = Deflate(b.Data);
                w.Write(z.Length);
                w.Write(z);
            }
        }
        return ms.ToArray();
    }

    public static SaveGame Decode(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes, writable: false);
        using var r = new BinaryReader(ms, Encoding.UTF8);
        var g = new SaveGame { Manifest = ReadHeader(r) };
        int n = r.ReadInt32();
        if (n < 0 || n > 10_000) throw new SaveException($"bozuk kayit: {n} bolum");
        for (int i = 0; i < n; i++)
        {
            string id = r.ReadString();
            int version = r.ReadInt32();
            int raw = r.ReadInt32();
            uint crc = r.ReadUInt32();
            int zl = r.ReadInt32();
            if (raw < 0 || zl < 0 || zl > ms.Length - ms.Position) throw new SaveException($"bozuk kayit: '{id}' boyu");
            var data = Inflate(r.ReadBytes(zl), raw);
            if (Crc32.Of(data) != crc) throw new SaveException($"bozuk kayit: '{id}' CRC tutmuyor");
            g.Sections[id] = new SaveBlob { Version = version, Data = data };
        }
        return g;
    }

    /// <summary>Yalniz bildirim (yuva listesi icin; bolumleri acmaz).</summary>
    public static SaveManifest ReadManifest(Stream s)
    {
        using var r = new BinaryReader(s, Encoding.UTF8, leaveOpen: true);
        return ReadHeader(r);
    }

    static SaveManifest ReadHeader(BinaryReader r)
    {
        var magic = r.ReadBytes(4);
        if (!magic.AsSpan().SequenceEqual(Magic)) throw new SaveException("kayit dosyasi degil (KBRS yok)");
        int format = r.ReadUInt16();
        if (format > Format) throw new SaveException($"kayit bicimi v{format}, bu surum en fazla v{Format} okur");
        int ml = r.ReadInt32();
        if (ml < 0 || ml > 16 * 1024 * 1024) throw new SaveException("bozuk bildirim");
        var man = JsonSerializer.Deserialize<SaveManifest>(r.ReadBytes(ml), SaveJson.Options) ?? throw new SaveException("bildirim okunamadi");
        man.Format = format;
        return man;
    }

    static byte[] Deflate(byte[] data)
    {
        using var ms = new MemoryStream();
        using (var z = new DeflateStream(ms, CompressionLevel.Fastest, leaveOpen: true)) z.Write(data);
        return ms.ToArray();
    }

    static byte[] Inflate(byte[] z, int raw)
    {
        var o = new byte[raw];
        using var s = new DeflateStream(new MemoryStream(z), CompressionMode.Decompress);
        int at = 0;
        while (at < raw)
        {
            int k = s.Read(o, at, raw - at);
            if (k <= 0) throw new SaveException("bozuk kayit: veri kisa");
            at += k;
        }
        return o;
    }
}

/// <summary>CRC-32 (IEEE). Kayit bolumlerinin butunluk denetimi.</summary>
public static class Crc32
{
    static readonly uint[] Table = Build();

    static uint[] Build()
    {
        var t = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint c = i;
            for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            t[i] = c;
        }
        return t;
    }

    public static uint Of(ReadOnlySpan<byte> data)
    {
        uint c = 0xFFFFFFFFu;
        foreach (var b in data) c = Table[(c ^ b) & 0xFF] ^ (c >> 8);
        return c ^ 0xFFFFFFFFu;
    }
}
