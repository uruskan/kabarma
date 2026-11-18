using System.Collections.Generic;
using Godot;

namespace Kabarma.Game;

/// <summary>
/// Dunya: ic mekan odalari (Oyuklar, M3 oyuk akisi; TEKNIK §7.5). Odalar oynanabilir karenin disindaki xz'lerde, arazinin
/// altinda durur; HeightAt oda icinde oda tabanini verir (Cekim, mermi, yerlestirme dogru zemini gorur). Indoor acikken
/// gunes, gok isigi ve sis kapanir (ApplyTime sonunda).
/// </summary>
public partial class World
{
    readonly List<(float x, float z, float r, float floor)> _interiors = new();
    float _interiorMinX = float.MaxValue;

    /// <summary>Oyuncu bir odadaysa true (OyukSystem verir): ApplyTime icerisi icin karartir.</summary>
    public bool Indoor { get; private set; }
    float _indoorAmbient = 0.06f;
    static readonly Color IndoorAmbientColor = new(0.55f, 0.5f, 0.6f);
    Color _savedAmbientColor;
    bool _savedFog;
    Environment.AmbientSource _savedSource;

    public void AddInterior(float x, float z, float radius, float floor)
    {
        _interiors.Add((x, z, radius, floor));
        _interiorMinX = Mathf.Min(_interiorMinX, x - radius - 2f);
    }

    /// <summary>xz bir odanin icindeyse oda tabani (oynanabilir karenin icinde hic dogru degil: tek karsilastirma).</summary>
    public bool InteriorFloor(float wx, float wz, out float floor)
    {
        floor = 0;
        if (wx < _interiorMinX) return false;
        foreach (var r in _interiors)
        {
            float dx = wx - r.x, dz = wz - r.z;
            if (dx * dx + dz * dz <= (r.r + 2f) * (r.r + 2f)) { floor = r.floor; return true; }
        }
        return false;
    }

    public void SetIndoor(bool on, float ambient)
    {
        if (on == Indoor) return;
        var env = Env.Environment;
        if (on) { _savedAmbientColor = env.AmbientLightColor; _savedFog = env.FogEnabled; _savedSource = env.AmbientLightSource; }
        Indoor = on;
        _indoorAmbient = ambient;
        if (!on) { env.AmbientLightColor = _savedAmbientColor; env.FogEnabled = _savedFog; env.AmbientLightSource = _savedSource; }
        ApplyTime();
    }

    /// <summary>ApplyTime'in son adimi: iceride gunes ve sis yok, ortam isigi zayif.</summary>
    void ApplyIndoor()
    {
        if (!Indoor) return;
        var env = Env.Environment;
        env.AmbientLightSource = Environment.AmbientSource.Color;
        env.AmbientLightColor = IndoorAmbientColor;
        env.AmbientLightEnergy = _indoorAmbient;
        env.FogEnabled = false;
        Sun.LightEnergy = 0f;
    }
}
