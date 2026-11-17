namespace Kabarma.Sim;

/// <summary>
/// Gun dongusu (GDD 2): bir gun = gunduz + gece, gun dogumunda baslar. Normal gece Kabarma'sizdir (M1).
/// Uyuyunca normal gece atlanir; Kabarma gecesi atlanamaz. Isigi dunya katmani <see cref="TimeOfDay"/> ve
/// <see cref="Part"/> ile cizer; burada yalnizca saat var.
/// </summary>
public sealed class Calendar
{
    readonly CalendarFile _def;

    public Calendar(CalendarFile def) { _def = def; }

    /// <summary>Saat isliyor mu (ilk geceden once ilk gunun senaryosu zamani yonetir).</summary>
    public bool Running { get; private set; }
    /// <summary>Kabarma suresince saat durur.</summary>
    public bool Paused { get; set; }
    public int Day { get; private set; } = 1;
    /// <summary>Gun dogumundan beri gecen oyun saniyesi.</summary>
    public float Seconds { get; private set; }
    public int NightsSkipped { get; private set; }

    /// <summary>Yeni gun basladi (gun dogumu): gun numarasi.</summary>
    public event Action<int>? Morning;
    /// <summary>Normal gece basladi: gun numarasi.</summary>
    public event Action<int>? Dusk;

    float Scale => Math.Clamp(_def.LengthScale, _def.LengthScaleMin, _def.LengthScaleMax);
    public float DaylightSeconds => _def.DaylightMinutes * 60f * Scale;
    public float NightSeconds => _def.NightMinutes * 60f * Scale;
    public float DayLength => DaylightSeconds + NightSeconds;

    /// <summary>0..1: 0 gun dogumu, gunduz/gun orani gecince gece.</summary>
    public float TimeOfDay => DayLength <= 0 ? 0 : Seconds / DayLength;
    public bool IsNight => Running && Seconds >= DaylightSeconds;
    /// <summary>Gunduzun ne kadari gecti (0..1); gece 1.</summary>
    public float DaylightFraction => DaylightSeconds <= 0 ? 1f : Math.Clamp(Seconds / DaylightSeconds, 0f, 1f);
    /// <summary>Gecenin ne kadari gecti (0..1); gunduz 0.</summary>
    public float NightFraction => !IsNight || NightSeconds <= 0 ? 0f : Math.Clamp((Seconds - DaylightSeconds) / NightSeconds, 0f, 1f);

    /// <summary>Gunun parcasi: veride sabah, ogle, aksam; gece ayri.</summary>
    public DayPartDef Part
    {
        get
        {
            if (IsNight) return _def.NightPart;
            float f = DaylightFraction;
            foreach (var p in _def.Parts) if (f < p.Until) return p;
            return _def.Parts.Count > 0 ? _def.Parts[^1] : _def.NightPart;
        }
    }

    /// <summary>Gunduz kalan saniye (gece 0).</summary>
    public float SecondsToNight => IsNight ? 0f : DaylightSeconds - Seconds;

    public void Start(int day)
    {
        Running = true;
        Day = day;
        Seconds = 0;
        Morning?.Invoke(Day);
    }

    public void Tick(float dt)
    {
        if (!Running || Paused || dt <= 0) return;
        bool wasNight = IsNight;
        Seconds += dt;
        if (!wasNight && IsNight) Dusk?.Invoke(Day);
        while (Seconds >= DayLength && DayLength > 0)
        {
            Seconds -= DayLength;
            Day++;
            Morning?.Invoke(Day);
            if (IsNight) Dusk?.Invoke(Day);
        }
    }

    /// <summary>GDD 2: normal gecede uyuyunca sabaha atlanir. Gunduz ya da Kabarma suresince bir sey olmaz.</summary>
    public bool Sleep(bool kabarmaActive)
    {
        if (!Running || kabarmaActive || !IsNight) return false;
        Seconds = 0;
        Day++;
        NightsSkipped++;
        Morning?.Invoke(Day);
        return true;
    }

    /// <summary>Sinama ve gorunus araci: saati gunun belli bir kesrine alir (gun degismez).</summary>
    public void SetTimeOfDay(float fraction)
    {
        bool wasNight = IsNight;
        Seconds = Math.Clamp(fraction, 0f, 0.9999f) * DayLength;
        if (!wasNight && IsNight) Dusk?.Invoke(Day);
    }
}
