using System.Text.Json;

namespace Kabarma.Sim;

/// <summary>Menudeki bir yuvanin ozeti.</summary>
public sealed class SaveSlotInfo
{
    public string Slot { get; set; } = "";
    public bool Exists { get; set; }
    public SaveManifest? Manifest { get; set; }
    /// <summary>Okunamadiysa neden.</summary>
    public string? Error { get; set; }
}

/// <summary>
/// Kayit dosyalari (TEKNIK 9): her yuva bir klasor. Yazma: once gecici dosyaya, sonra yedekleri kaydirip yeniden
/// adlandirma; bes donen yedek; dunya kaydi en fazla save.json maxWorldBytes. Okuma: ana dosya bozuksa en yeni
/// saglam yedege duser. Kok dizini cagiran verir (Godot: user://saves ya da sinamada user://test/kosu).
/// </summary>
public sealed class SaveStore
{
    readonly SaveFile _cfg;
    public string Root { get; }

    public SaveStore(string root, SaveFile cfg)
    {
        Root = root;
        _cfg = cfg;
    }

    public static bool ValidSlot(string slot) =>
        !string.IsNullOrEmpty(slot) && slot.Length <= 32 && slot.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '-');

    public string SlotDir(string slot)
    {
        if (!ValidSlot(slot)) throw new ArgumentException($"gecersiz yuva '{slot}'");
        return Path.Combine(Root, slot);
    }

    public string WorldPath(string slot) => Path.Combine(SlotDir(slot), _cfg.WorldFile);
    public string BackupPath(string slot, int i) => WorldPath(slot) + "." + i;
    public string CharacterPath => Path.Combine(Root, _cfg.CharacterFile);

    /// <summary>Dunya kaydini yazar. Donus: yazilan bayt. Sinir asilirsa hicbir dosyaya dokunmadan SaveException.</summary>
    public long Write(string slot, SaveGame g)
    {
        var bytes = SaveCodec.Encode(g);
        if (bytes.Length > _cfg.MaxWorldBytes)
            throw new SaveException($"kayit {bytes.Length / 1048576.0:0.0} MB, sinir {_cfg.MaxWorldBytes / 1048576.0:0.0} MB");
        var dir = SlotDir(slot);
        Directory.CreateDirectory(dir);
        var path = WorldPath(slot);
        WriteAtomic(path, bytes, _cfg.Backups);
        // Bildirimin okunur kopyasi (menu ve insan icin); gercek olan world.kbs icindeki.
        WriteAtomic(Path.Combine(dir, _cfg.ManifestFile), JsonSerializer.SerializeToUtf8Bytes(g.Manifest, new JsonSerializerOptions(SaveJson.Options) { WriteIndented = true }), 0);
        return bytes.Length;
    }

    /// <summary>
    /// Gecici dosyaya yaz, diske indir, sonra: yedekleri bir kaydir (n-1 -> n, ..., ana -> .1) ve geciciyi ana ada tasi.
    /// Elektrik kesilirse elde ya eski ana dosya ya da .1 yedegi kalir.
    /// </summary>
    static void WriteAtomic(string path, byte[] bytes, int backups)
    {
        var tmp = path + ".tmp";
        using (var f = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            f.Write(bytes);
            f.Flush(flushToDisk: true);
        }
        if (backups > 0 && File.Exists(path))
        {
            var last = path + "." + backups;
            if (File.Exists(last)) File.Delete(last);
            for (int i = backups - 1; i >= 1; i--)
            {
                var from = path + "." + i;
                if (File.Exists(from)) File.Move(from, path + "." + (i + 1));
            }
            File.Copy(path, path + ".1", overwrite: true);
        }
        File.Move(tmp, path, overwrite: true);
    }

    public bool Exists(string slot) => File.Exists(WorldPath(slot)) || Enumerable.Range(1, Math.Max(0, _cfg.Backups)).Any(i => File.Exists(BackupPath(slot, i)));

    /// <summary>Yuvayi okur; ana dosya bozuksa sirayla yedekleri dener. <paramref name="from"/>: okunan dosya.</summary>
    public SaveGame Read(string slot, out string from)
    {
        var tried = new List<string>();
        foreach (var p in new[] { WorldPath(slot) }.Concat(Enumerable.Range(1, Math.Max(0, _cfg.Backups)).Select(i => BackupPath(slot, i))))
        {
            if (!File.Exists(p)) continue;
            try
            {
                var g = SaveCodec.Decode(File.ReadAllBytes(p));
                from = p;
                return g;
            }
            catch (Exception e) when (e is SaveException or EndOfStreamException or IOException or InvalidDataException or JsonException)
            {
                tried.Add($"{Path.GetFileName(p)}: {e.Message}");
            }
        }
        throw new SaveException(tried.Count == 0 ? $"'{slot}' yuvasinda kayit yok" : $"'{slot}' okunamadi: {string.Join("; ", tried)}");
    }

    public SaveGame Read(string slot) => Read(slot, out _);

    /// <summary>Menu icin yuvalar (yuva1..yuvaN) ve ozetleri; yalniz bildirim okunur.</summary>
    public List<SaveSlotInfo> List()
    {
        var list = new List<SaveSlotInfo>();
        for (int i = 1; i <= _cfg.Slots; i++)
        {
            var slot = $"yuva{i}";
            var info = new SaveSlotInfo { Slot = slot };
            var p = WorldPath(slot);
            if (File.Exists(p))
            {
                info.Exists = true;
                try { using var f = File.OpenRead(p); info.Manifest = SaveCodec.ReadManifest(f); }
                catch (Exception e) { info.Error = e.Message; }
            }
            else if (Exists(slot)) { info.Exists = true; info.Error = "yalniz yedek var"; }
            list.Add(info);
        }
        return list;
    }

    /// <summary>En son kaydedilen yuva (Devam): bildirimdeki tarihe gore.</summary>
    public string? Latest() => List().Where(s => s.Manifest != null)
        .OrderByDescending(s => s.Manifest!.SavedUtc, StringComparer.Ordinal).Select(s => s.Slot).FirstOrDefault();

    // ---------- Karakter (TEKNIK 9: ayri dosya; Ustalik, Nisan, Ansiklopedi, kozmetik) ----------

    public void WriteCharacter(SaveGame g)
    {
        Directory.CreateDirectory(Root);
        WriteAtomic(CharacterPath, SaveCodec.Encode(g), _cfg.Backups);
    }

    /// <summary>Karakter dosyasi yoksa null (ilk acilis).</summary>
    public SaveGame? ReadCharacter()
    {
        foreach (var p in new[] { CharacterPath }.Concat(Enumerable.Range(1, Math.Max(0, _cfg.Backups)).Select(i => CharacterPath + "." + i)))
        {
            if (!File.Exists(p)) continue;
            try { return SaveCodec.Decode(File.ReadAllBytes(p)); }
            catch (Exception e) when (e is SaveException or EndOfStreamException or IOException or InvalidDataException or JsonException) { }
        }
        return null;
    }
}
