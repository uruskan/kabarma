using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>Oyuncu kamerasi (camera.json): iki kip, yakinlastirma sinirlari, gecis.</summary>
public class CameraTests
{
    static CameraFile C => TestData.Load().Camera;

    [Fact]
    public void Shoulder_mode_is_closer_than_any_normal_zoom()
    {
        var c = C;
        Assert.True(c.ZoomMin > 0f && c.ZoomMin < c.ZoomMax, "zoomMin < zoomMax olmali");
        Assert.InRange(c.Normal.Distance, c.ZoomMin, c.ZoomMax);
        Assert.True(c.Shoulder.Distance > 0f && c.Shoulder.Distance < c.ZoomMin, "yakin omuz normal kipin en yakinindan da yakin olmali");
        Assert.True(c.Shoulder.Side > 0f, "yakin omuzda kamera omzun yaninda (saga ofset)");
        foreach (var p in new[] { c.Normal, c.Shoulder })
        {
            Assert.InRange(p.Height, 0.5f, 3f);
            Assert.InRange(p.Fov, 30f, 110f);
        }
        Assert.True(c.CollisionRadius > 0f && c.TransitionSeconds > 0f && c.WheelStep > 0f && c.ReturnRate > 0f && c.ZoomSmooth > 0f);
    }

    [Fact]
    public void Wheel_steps_and_clamps()
    {
        var c = C;
        float d = c.Normal.Distance;
        Assert.Equal(d + c.WheelStep, c.StepZoom(d, 1), 3);
        Assert.Equal(d - c.WheelStep, c.StepZoom(d, -1), 3);
        Assert.Equal(c.ZoomMax, c.StepZoom(c.ZoomMax, 5), 3);
        Assert.Equal(c.ZoomMin, c.StepZoom(c.ZoomMin, -5), 3);
        Assert.Equal(c.ZoomMin, c.ClampZoom(0.1f), 3);
    }

    [Fact]
    public void Transition_eases_from_zero_to_one()
    {
        Assert.Equal(0f, CameraFile.Ease(-1f));
        Assert.Equal(1f, CameraFile.Ease(2f));
        Assert.Equal(0.5f, CameraFile.Ease(0.5f), 3);
        Assert.True(CameraFile.Ease(0.1f) < 0.1f, "yumusak baslar");
    }
}
