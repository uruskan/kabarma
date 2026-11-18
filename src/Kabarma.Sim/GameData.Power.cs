namespace Kabarma.Sim;

// Guc agi ve temel sinyal: power.json (GDD 11.1, 11.2; ICERIK 12 "M2 bedelleri").

/// <summary>Guc ya da sinyal parcasi.</summary>
public sealed class PowerDeviceDef
{
    public string Id { get; set; } = "";
    /// <summary>generator, battery, pole, switch, consumer, button, plate.</summary>
    public string Kind { get; set; } = "";
    /// <summary>Uretec kaynagi: wind (0..watts), animal (icinde hayvan varken), fixed.</summary>
    public string? Source { get; set; }
    /// <summary>Uretec en yuksek uretimi, tuketici talebi, akunun en yuksek dolma/bosalma gucu (W).</summary>
    public float Watts { get; set; }
    /// <summary>Aku kapasitesi (Wh).</summary>
    public float CapacityWh { get; set; }
    /// <summary>Guc kablosu soketi (GDD 11.1: Direk 4). Sinyal teli saymaz.</summary>
    public int Sockets { get; set; }
    /// <summary>Sinyal teliyle surulebilir (Salter, Elektrik Lambasi).</summary>
    public bool SignalIn { get; set; }
    public float LightRange { get; set; }
    /// <summary>Hayvan Carki: hayvanin aranacagi yaricap (m).</summary>
    public float AnimalRadius { get; set; }
    public int Tier { get; set; }

    public bool IsSignalSource => Kind is "button" or "plate";
}

public sealed class WindDef
{
    public float Min { get; set; }
    public float PeriodA { get; set; } = 2400f;
    public float PeriodB { get; set; } = 970f;
}

public sealed class PowerFile
{
    public float CableMaxLength { get; set; } = 12f;
    public string CableItem { get; set; } = "kablo";
    public int SignalMax { get; set; } = 100;
    public int PriorityMin { get; set; } = 1;
    public int PriorityMax { get; set; } = 5;
    public int PriorityDefault { get; set; } = 3;
    public float ButtonSeconds { get; set; } = 1f;
    public float PlateRadius { get; set; } = 0.8f;
    public WindDef Wind { get; set; } = new();
    public List<PowerDeviceDef> Devices { get; set; } = new();
    /// <summary>Guc isteyen istasyonlar (zincir): yerlestirilebilir kimligi -> W.</summary>
    public Dictionary<string, float> Consumers { get; set; } = new();

    /// <summary>Kimligin parca tanimi; guc isteyen istasyon icin tuketici tanimi uretir; ikisi de degilse null.</summary>
    public PowerDeviceDef? Def(string id)
    {
        foreach (var d in Devices) if (d.Id == id) return d;
        if (Consumers.TryGetValue(id, out float w) && w > 0)
            return new PowerDeviceDef { Id = id, Kind = "consumer", Watts = w, Sockets = 1 };
        return null;
    }

    /// <summary>Bu kimlik guc ya da sinyal parcasi mi (yerlestirme yolu buna gore ayrilir).</summary>
    public bool IsDevice(string id) => Devices.Exists(d => d.Id == id);

    /// <summary>Istasyonun guc talebi (W); tabloda yoksa 0 (guc istemez, hep calisir).</summary>
    public float ConsumerWatts(string id) => Consumers.TryGetValue(id, out float w) ? w : 0f;
}

public sealed partial class GameData
{
    [DataFile("power.json")]
    public PowerFile Power { get; private set; } = new();
}
