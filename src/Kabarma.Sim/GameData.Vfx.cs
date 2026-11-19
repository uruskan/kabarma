namespace Kabarma.Sim;

// vfx.json: ortak efekt kitapligi (SANAT-SES §7). Her efekt kimlikle cagrilir (Vfx.Play("hit_spark", konum)).
// Parcacik efektleri havuzlanir, omru kisadir; halka ve telgraf efektleri ayni sekilde.

/// <summary>Tek bir efektin tarifi. Renkler palet adi ya da #RRGGBB; ilki baslangic, sonuncusu sonuc rengi.</summary>
public sealed class VfxDef
{
    /// <summary>Parcacik sayisi (0: yalniz halka).</summary>
    public int Count { get; set; } = 12;
    public float Life { get; set; } = 0.5f;
    public float Speed { get; set; } = 3f;
    /// <summary>Yayilma acisi (derece, yonun cevresi; 180: her yon).</summary>
    public float Spread { get; set; } = 180f;
    public float Gravity { get; set; }
    public float Size { get; set; } = 0.1f;
    /// <summary>Omur sonunda boyut orani (0: kaybolur, 1: ayni).</summary>
    public float SizeEnd { get; set; } = 0.2f;
    public List<string> Colors { get; set; } = new() { "OffWhite" };
    public bool Glow { get; set; } = true;
    public float Damping { get; set; } = 1.5f;
    /// <summary>Yon (yerel: oyuncuya gore degil, dunya; yon verilmezse bu kullanilir).</summary>
    public List<float> Dir { get; set; } = new() { 0, 1, 0 };
    /// <summary>sphere | streak (hiza gore uzayan) | quad.</summary>
    public string Shape { get; set; } = "sphere";
    /// <summary>Baslangic yaricapi (parcaciklar bu kureden dogar).</summary>
    public float Emit { get; set; }
    /// <summary>Halka yaricapi (m; 0: halka yok) ve acilma suresi.</summary>
    public float Ring { get; set; }
    public float RingSeconds { get; set; } = 0.5f;
    public float RingWidth { get; set; } = 0.12f;
    /// <summary>Kisa isik patlamasi (OmniLight enerjisi; 0: yok).</summary>
    public float Light { get; set; }
    /// <summary>Kamera sarsintisi (feel.json master.shake ile carpilir).</summary>
    public float Shake { get; set; }
    /// <summary>Surekli efekt (durum aurasi): Count/Life ile akar, Stop'a kadar.</summary>
    public bool Loop { get; set; }
    /// <summary>Yaziyla efekt (Z'ler, ünlem): bos degilse parcacik yerine uçan metin.</summary>
    public string Text { get; set; } = "";
}

public sealed class VfxFile
{
    public Dictionary<string, VfxDef> Effects { get; set; } = new();
    /// <summary>Capa basligi -> efekt on eki: "head.civi.muzzle" yoksa "head.default.muzzle" kullanilir.</summary>
    public float StatusScanSeconds { get; set; } = 0.15f;
    /// <summary>Gece boyunca zemin damarlarinin dogus araligi (sn) ve gokyuzu dalgasi araligi.</summary>
    public float VeinEvery { get; set; } = 0.5f;
    public float VeinRadius { get; set; } = 24f;
    public float SkyWaveEvery { get; set; } = 14f;
    /// <summary>En cok eszamanli parcacik dugumu (havuz tavani; asinca en eski yeniden kullanilir).</summary>
    public int PoolMax { get; set; } = 96;
}

public sealed partial class GameData
{
    [DataFile("vfx.json")]
    public VfxFile Vfx { get; private set; } = new();
}
