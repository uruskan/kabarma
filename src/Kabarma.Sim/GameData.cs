using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Kabarma.Sim;

// Veri dosyalarinin (game/data/*.json) C# karsiliklari. Sayilar burada degil, veride durur.
// Aile basina bir parca: GameData.Crops.cs, GameData.Creatures.cs, GameData.Night.cs (kartlar, Kabarma),
// GameData.Tool.cs (Capa, silahlar, tarifler, yerlestirilebilirler, yemekler, fiyatlar), GameData.Scenario.cs.
// Yeni veri ailesi: yeni bir GameData.Xxx.cs icinde DTO'lar ve [DataFile("xxx.json")] isaretli bir GameData ozelligi.
// Load onu kendisi bulur; DataCheckTests anahtarlarini denetler.

/// <summary>
/// Bir GameData ozelligini veri dosya(lar)ina baglar. Birden cok dosya verilirse ust duzey anahtarlari tek nesnede
/// birlesir ("_" ile baslayanlar aciklamadir; ayni anahtar iki dosyada olamaz).
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class DataFileAttribute : Attribute
{
    public string[] Files { get; }
    public DataFileAttribute(params string[] files) { Files = files; }
}

public sealed partial class GameData
{
    static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
    };

    static readonly JsonDocumentOptions DocOptions = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    /// <summary>Yukleme listesi: [DataFile] isaretli her ozellik ve dosyalari (ad sirasiyla). Aile parcalari kendini buraya isaretle ekler.</summary>
    public static IReadOnlyList<(PropertyInfo Property, string[] Files)> Sources { get; } =
        typeof(GameData).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => (Property: p, Files: p.GetCustomAttribute<DataFileAttribute>()?.Files))
            .Where(x => x.Files != null)
            .OrderBy(x => x.Property.Name, StringComparer.Ordinal)
            .Select(x => (x.Property, x.Files!))
            .ToList();

    /// <param name="read">Dosya adini (ornegin "crops.json") metne ceviren okuyucu. Godot ve testler kendi okuyucusunu verir.</param>
    public static GameData Load(Func<string, string> read)
    {
        var d = new GameData();
        foreach (var (prop, files) in Sources) prop.SetValue(d, Parse(prop.PropertyType, read, files));
        return d;
    }

    static object Parse(Type type, Func<string, string> read, string[] files)
    {
        if (files.Length == 1)
            return JsonSerializer.Deserialize(read(files[0]), type, Options) ?? throw new InvalidDataException($"{files[0]} okunamadi");
        var merged = new JsonObject();
        var from = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            var obj = JsonNode.Parse(read(file), null, DocOptions) as JsonObject ?? throw new InvalidDataException($"{file}: ust duzey JSON nesnesi olmali");
            foreach (var (key, value) in obj)
            {
                if (key.StartsWith('_')) continue;
                if (from.TryGetValue(key, out var other)) throw new InvalidDataException($"{file}: '{key}' anahtari {other} icinde de var");
                from[key] = file;
                merged[key] = value?.DeepClone();
            }
        }
        return merged.Deserialize(type, Options) ?? throw new InvalidDataException($"{string.Join(", ", files)} okunamadi");
    }
}
