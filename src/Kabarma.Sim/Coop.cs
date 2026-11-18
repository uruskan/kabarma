namespace Kabarma.Sim;

// Evcil hayvan ve agil ici uretim (GDD 9.1; URETIM 6 M1 "Gidak ve agil"): yemle guven, izleme, agila girince kayit,
// Yemlik'te yem varken belli aralikla urun (Gidak: yumurta) Folluk'a, Folluk yoksa yere. Kacma, mutluluk, ureme M2.
// Durum duz veri: kayda hazir (TEKNIK 9), Godot katmani yalniz gorur ve bildirir.

/// <summary>Bir evcillestirilebilir hayvanin kural durumu.</summary>
public sealed class PenAnimal
{
    public string Species { get; init; } = "";
    /// <summary>Ilk gunun ogretici hayvani (GDD 24: tek yemle guvenir, agila ilk girisinde hemen yumurtlar).</summary>
    public bool Tutorial { get; init; }
    /// <summary>Guven cubugu 0..1; dolunca oyuncuyu izler.</summary>
    public float Trust { get; set; }
    public bool Tame => Trust >= 1f;
    /// <summary>Agila girip kayit oldu.</summary>
    public bool Registered { get; set; }
    /// <summary>Bir sonraki urune kalan sure (yem varken isler).</summary>
    public float LayTimer { get; set; }
    public int Laid { get; set; }

    // ---- M2 hayvancilik (GDD 9.2-9.4; kurallar Husbandry*.cs). Duz, serilestirilebilir (TEKNIK 9). ----
    /// <summary>Kalici kimlik (Ranch.NextId); ebeveynler kimlikle anilir.</summary>
    public int Id { get; set; }
    public AnimalGenes Genes { get; set; } = new();
    /// <summary>Mutluluk 0-100; eksi = henuz baslamadi (ilk tikte happiness.start).</summary>
    public float Happiness { get; set; } = -1;
    /// <summary>Yas (oyun gunu). Yabaniden evcillesen yetiskindir.</summary>
    public float AgeDays { get; set; } = 99;
    public string Role { get; set; } = "";
    public string Command { get; set; } = "izle";
    /// <summary>Urun (esya kimligi), is ("work") ve gubre ("manure") sayaclari, oyun saniyesi.</summary>
    public Dictionary<string, float> Timers { get; set; } = new();
    /// <summary>Hayvanin ustunde biriken urun (E ile alinir): esya -> (adet, ★).</summary>
    public List<ProductStack> Pending { get; set; } = new();
    /// <summary>Son okşandigi gun (Ranch.Day).</summary>
    public int LastPetDay { get; set; } = -1;
    /// <summary>Mutluluk kacma esiginin altinda gecen sure; dolunca WantsOut.</summary>
    public float LowSeconds { get; set; }
    public bool WantsOut { get; set; }
    /// <summary>Ureme sayaci (saniye, eksi = yok) ve esi.</summary>
    public float BreedTimer { get; set; } = -1;
    public int MateId { get; set; }
    public int MotherId { get; set; }
    public int FatherId { get; set; }
    /// <summary>Yirtici yolu: can %25 altinda Cekim/Zipkin ile tutuldu (GDD 9.1).</summary>
    public bool Bound { get; set; }
    /// <summary>Kovan turu: yerlestigi Ari Kovani'nin kimligi (0 = yok).</summary>
    public int HiveId { get; set; }
    /// <summary>Yuk rolunde heybe: esya -> adet (yuva sayisi Rules.PackSlots).</summary>
    public Dictionary<string, int> Pack { get; set; } = new();
    /// <summary>Konum (Godot her kare yazar; agil uyeligi buradan turetilir).</summary>
    public float X { get; set; }
    public float Z { get; set; }
    /// <summary>Alaca (GDD 9.3 = 8.3): "" ya da benekli, altin, aurora (crops.json genetics.alaca).</summary>
    public string Alaca { get; set; } = "";
    public bool Adult(float adultDays) => AgeDays >= adultDays;
}

/// <summary>Bir urun yigini: esya, adet, kalite (★1-3).</summary>
public sealed class ProductStack
{
    public string Item { get; set; } = "";
    public int Count { get; set; }
    public int Stars { get; set; } = 1;
}

/// <summary>Yemlik: icine yem konur (ICERIK 10.2). Bosken ilk konan yem turunu belirler (M2: her tur kendi yemini yer).</summary>
public sealed class Feeder
{
    public string Food { get; set; } = "wheat";
    public int Amount { get; set; }
    public int Capacity { get; init; }
    public int Room => Math.Max(0, Capacity - Amount);
    public int Id { get; set; }
    public float X { get; set; }
    public float Z { get; set; }
}

/// <summary>Folluk: agildaki Gidak buraya yumurtlar (ICERIK 10.2: 6 yuva).</summary>
public sealed class Nest
{
    public int Eggs { get; set; }
    public int Slots { get; init; }
    public bool Full => Eggs >= Slots;
    public int Id { get; set; }
    public float X { get; set; }
    public float Z { get; set; }
}

/// <summary>Bir yumurtlamanin nereye gittigi: Folluk ya da yer.</summary>
public enum LayTarget { Nest, Ground }

public sealed class CoopRules
{
    readonly PenFile _def;
    public CoopRules(PenFile def) { _def = def; }

    public PenAnimalDef Animal(string species) => _def.Animals.First(a => a.Id == species);

    /// <summary>Yem yedirir. Ogretici hayvan tutorialFoodToTrust yemle, digerleri foodToTrust yemle guvenir. Bu yemle evcillestiyse true.</summary>
    public bool Feed(PenAnimal a)
    {
        if (a.Tame) return false;
        int need = a.Tutorial ? _def.TutorialFoodToTrust : Animal(a.Species).FoodToTrust;
        a.Trust = Math.Min(1f, a.Trust + 1f / Math.Max(1, need));
        return a.Tame;
    }

    /// <summary>Kosmak ve ates etmek cubugu sifirlar (GDD 9.1). Dolmus cubuga dokunmaz.</summary>
    public void Scare(PenAnimal a) { if (!a.Tame) a.Trust = 0f; }

    /// <summary>
    /// Evcil hayvan agilin icine girdi: kayit olur (GDD 9.1). Agil alani tur basina alana yetmiyorsa kayit olmaz.
    /// Donus: kayit oldu mu; immediateEgg, ogretici hayvanin ilk girisinde hemen bir yumurta (GDD 24).
    /// </summary>
    public bool Register(PenAnimal a, PenShape pen, int alreadyInPen, out bool immediateEgg)
    {
        immediateEgg = false;
        if (a.Registered || !a.Tame) return false;
        if (pen.Capacity(Animal(a.Species).Area) <= alreadyInPen) return false;
        a.Registered = true;
        a.LayTimer = Animal(a.Species).LayInterval;
        immediateEgg = a.Tutorial && _def.TutorialEggOnEntry && a.Laid == 0;
        if (immediateEgg) a.Laid++;
        return true;
    }

    /// <summary>
    /// Kayitli hayvanin uretimi: agildaki Yemlik'te yem varken sure isler; dolunca yemden foodPerEgg alinir ve
    /// bir urun cikar. Yem yoksa sure durur. Donus: bu adimda cikan urun sayisi.
    /// </summary>
    public int Tick(PenAnimal a, float dt, Feeder? feeder)
    {
        if (!a.Registered) return 0;
        var def = Animal(a.Species);
        if (feeder == null || feeder.Food != def.Food || feeder.Amount < def.FoodPerEgg) return 0;
        a.LayTimer -= dt;
        int n = 0;
        while (a.LayTimer <= 0 && feeder.Amount >= def.FoodPerEgg)
        {
            feeder.Amount -= def.FoodPerEgg;
            a.LayTimer += def.LayInterval;
            a.Laid++;
            n++;
        }
        if (a.LayTimer <= 0) a.LayTimer = def.LayInterval; // yem bitti: sure bastan
        return n;
    }

    /// <summary>Yumurta agildaki bos yuvasi olan ilk Folluk'a girer; yoksa yere duser (ICERIK 10.2).</summary>
    public static LayTarget Place(IEnumerable<Nest> nestsInPen, out Nest? nest)
    {
        nest = nestsInPen.FirstOrDefault(n => !n.Full);
        if (nest == null) return LayTarget.Ground;
        nest.Eggs++;
        return LayTarget.Nest;
    }

    /// <summary>Yemlik'e cantadan yem koyar; konan miktar.</summary>
    public static int Fill(Feeder f, Inventory bag)
    {
        int n = Math.Min(f.Room, bag.Count(f.Food));
        if (n <= 0) return 0;
        bag.Remove(f.Food, n);
        f.Amount += n;
        return n;
    }

    /// <summary>Folluk'taki yumurtalari cantaya alir; alinan miktar.</summary>
    public static int Collect(Nest n, Inventory bag, string product)
    {
        int got = n.Eggs;
        if (got <= 0) return 0;
        bag.Add(product, got);
        n.Eggs = 0;
        return got;
    }
}

/// <summary>Agil ve hayvan durumu (GameState icinde).</summary>
public sealed class Coop
{
    public CoopRules Rules { get; }
    public List<PenAnimal> Animals { get; } = new();
    public List<Feeder> Feeders { get; } = new();
    public List<Nest> Nests { get; } = new();

    public Coop(PenFile def) { Rules = new CoopRules(def); }
}
