using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>
/// Veri denetimi (TEKNIK §11 DataCheck): her JSON anahtari bir C# alanina denk gelmeli. Yukleyici bilinmeyen anahtari
/// sessizce atlar (oyun kirilmasin); bu test atlanan her anahtari, yani yazim hatasini, yakalar. "_" ile baslayan
/// anahtarlar aciklamadir; sozluk (Dictionary) alanlarinin anahtarlari serbesttir.
/// </summary>
public class DataCheckTests
{
    static readonly JsonDocumentOptions Doc = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    [Fact]
    public void Every_json_key_maps_to_a_data_field()
    {
        var problems = new List<string>();
        foreach (var (prop, files) in GameData.Sources)
            foreach (var f in files)
                Check(JsonNode.Parse(TestData.Read(f), null, Doc), prop.PropertyType, f, problems);
        Assert.True(problems.Count == 0, "Bilinmeyen veri anahtarlari:\n" + string.Join("\n", problems));
    }

    [Fact]
    public void Every_data_file_is_loaded_by_some_family()
    {
        var claimed = GameData.Sources.SelectMany(s => s.Files).ToHashSet();
        // palette.json Sim verisi degil: uretim hatti (pipeline/clean_asset.py) ve Palette.cs icin.
        var stray = Directory.GetFiles(TestData.Dir, "*.json").Select(Path.GetFileName).Where(f => f != "palette.json" && !claimed.Contains(f!));
        Assert.Empty(stray);
    }

    [Fact]
    public void Content_counts_match_plan_7_1_for_farm()
    {
        var d = TestData.Load();
        Assert.Equal(24, d.Crops.Crops.Count);                                   // Ekin 24
        Assert.Equal(32, d.Tool.Foods.Count);                                    // Yemek tarifi 32
        Assert.Equal(32, d.Tool.Recipes.Count(r => r.Category == "yemek"));
    }

    [Fact]
    public void Checker_catches_a_misspelled_key()
    {
        var problems = new List<string>();
        Check(JsonNode.Parse("""{ "_not": "aciklama", "mowRadiuss": 1.6, "weapon": { "damagee": 3 }, "prices": { "herhangi": 1 } }"""), typeof(ToolFile), "deneme.json", problems);
        Assert.Equal(2, problems.Count);
        Assert.Contains(problems, p => p.Contains("mowRadiuss"));
        Assert.Contains(problems, p => p.Contains("damagee"));
    }

    [Fact]
    public void Merged_files_reject_a_key_defined_twice()
    {
        // mineRate heads.json'da; prices.json'a da yazilirsa hangisinin gecerli oldugu belirsiz olur: yukleme durur.
        Assert.Throws<InvalidDataException>(() => GameData.Load(f => f == "prices.json" ? """{ "mineRate": 1 }""" : TestData.Read(f)));
    }

    static void Check(JsonNode? node, Type type, string path, List<string> problems)
    {
        if (node == null) return;
        type = Nullable.GetUnderlyingType(type) ?? type;
        switch (node)
        {
            case JsonObject obj when DictionaryValue(type) is Type valueType:
                foreach (var (k, v) in obj) Check(v, valueType, $"{path}.{k}", problems);
                break;
            case JsonObject obj:
                if (type.IsPrimitive || type == typeof(string)) { problems.Add($"{path}: {type.Name} beklenirken nesne"); break; }
                var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanWrite)
                    .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
                foreach (var (k, v) in obj)
                {
                    if (k.StartsWith('_')) continue;
                    if (!props.TryGetValue(k, out var p)) { problems.Add($"{path}: bilinmeyen anahtar '{k}' ({type.Name})"); continue; }
                    Check(v, p.PropertyType, $"{path}.{k}", problems);
                }
                break;
            case JsonArray arr:
                var elem = ListElement(type);
                if (elem == null) { problems.Add($"{path}: {type.Name} beklenirken dizi"); break; }
                for (int i = 0; i < arr.Count; i++) Check(arr[i], elem, $"{path}[{i}]", problems);
                break;
        }
    }

    static Type? DictionaryValue(Type t) =>
        t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Dictionary<,>) ? t.GetGenericArguments()[1] : null;

    static Type? ListElement(Type t) =>
        t.IsArray ? t.GetElementType() : t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>) ? t.GetGenericArguments()[0] : null;
}
