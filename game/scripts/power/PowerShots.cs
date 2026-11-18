using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Guc akisinin gorsel kaniti (--test=powershots; bot degil, kurulum dogrudan): Ocak, Ruzgar Gulu, Direk, Salter, Elektrik
/// Lambasi, Hayvan Carki, Dugme, Basinc Plakasi kurulur, kablolar ve sinyal telleri cekilir; gunduz ve gece goruntuleri
/// progress/m2/guc/ altina. Basliksiz kosuda yalniz denetimler (kablo agi, lamba isigi) yapilir.
/// </summary>
public partial class PowerShots : Node
{
    readonly Director _d;
    string _out;
    int _fail;

    public PowerShots(Director d) { _d = d; Name = "PowerShots"; }

    async Task Frames(int n) { for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    Vector3 G(float x, float z) => _d.World.Ground(_d.OcakPos + new Vector3(x, 0, z));
    PlaceableDef Def(string id) => _d.Data.Tool.Placeables.Single(p => p.Id == id);

    void Check(bool ok, string what) { if (!ok) _fail++; GD.Print($"POWERSHOT {(ok ? "OK  " : "FAIL")} {what}"); }

    public override async void _Ready()
    {
        _out = ProjectSettings.GlobalizePath("res://").TrimEnd('/', '\\') + "/../progress/m2/guc/";
        DirAccess.MakeDirRecursiveAbsolute(_out);
        GetTree().CreateTimer(120, true, false, true).Timeout += () => { GD.Print("POWERSHOT zaman asimi"); GetTree().Quit(1); };
        await Frames(20);
        try { await Run(); }
        catch (Exception e) { _fail++; GD.Print("POWERSHOT HATA " + e); }
        GD.Print($"POWERSHOT RESULT {(_fail == 0 ? "ok" : "fail")} fail={_fail}");
        GetTree().Quit(_fail == 0 ? 0 : 1);
    }

    async Task Run()
    {
        _d.PlaceOcak(_d.World.Ground(Vector3.Zero));
        await Frames(2);
        var ps = _d.Power;
        var gen = ps.Place(Def("ruzgar_gulu"), G(4, -8), 0.4f);
        var pole = ps.Place(Def("direk"), G(9, -8), 0);
        var sw = ps.Place(Def("salter"), G(12, -6), 0);
        var lamp = ps.Place(Def("elektrik_lambasi"), G(14, -9), 0);
        var lamp2 = ps.Place(Def("elektrik_lambasi"), G(9, -12), 0);
        var wheel = ps.Place(Def("hayvan_carki"), G(5, -13), 0);
        var btn = ps.Place(Def("dugme"), G(15, -5), 0);
        var plate = ps.Place(Def("basinc_plakasi"), G(11, -3), 0);
        await Frames(2);
        var f = _d.Data.Power; var net = _d.State.Power;
        Check(net.Connect(f, gen.NodeId, pole.NodeId, false) != null, "Ruzgar Gulu -> Direk");
        Check(net.Connect(f, pole.NodeId, sw.NodeId, false) != null, "Direk -> Salter");
        Check(net.Connect(f, sw.NodeId, lamp.NodeId, false) != null, "Salter -> Lamba");
        Check(net.Connect(f, pole.NodeId, lamp2.NodeId, false) != null, "Direk -> Lamba 2");
        Check(net.Connect(f, wheel.NodeId, pole.NodeId, false) != null, "Hayvan Carki -> Direk");
        Check(net.Connect(f, plate.NodeId, sw.NodeId, true) != null, "Plaka ~> Salter (sinyal)");
        Check(net.Connect(f, btn.NodeId, lamp.NodeId, true) != null, "Dugme ~> Lamba (sinyal)");
        await Frames(10);
        Check(net.Get(lamp2.NodeId).Powered, "Lamba 2 ruzgarla yaniyor");
        Check(!net.Get(lamp.NodeId).Powered, "Lamba 1: salter acik (plaka bos) ve dugme basilmadi, sonuk");
        Check(GetTree().Root.FindChildren("*", "MeshInstance3D", true, false).Count > 0, "kablo gorunusleri");
        // Oyuncu plakaya basar, dugmeye basilir: lamba 1 yanar
        _d.Player.GlobalPosition = plate.GlobalPosition + Vector3.Up * 0.2f;
        net.Press(f, btn.NodeId);
        await Frames(3);
        Check(net.Get(sw.NodeId).Signal > 0, "plaka salteri kapatti");
        Check(net.Get(lamp.NodeId).Powered, "dugme sinyaliyle lamba 1 yaniyor");
        if (DisplayServer.GetName() == "headless") return;
        _d.Player.GlobalPosition = G(8, 4) + Vector3.Up * 0.3f;
        await Shot("guc_gunduz.png", G(2, 2) + Vector3.Up * 4.5f, G(10, -8) + Vector3.Up * 1.2f);
        await Shot("guc_yakin.png", G(10, -2) + Vector3.Up * 2.2f, G(10, -8) + Vector3.Up * 1.6f);
        // Gece: gunes isigi kisilir (gorsel kanit; gun saati gecehud'un)
        foreach (var l in GetTree().Root.FindChildren("*", "DirectionalLight3D", true, false).OfType<DirectionalLight3D>()) l.LightEnergy = 0.05f;
        foreach (var we in GetTree().Root.FindChildren("*", "WorldEnvironment", true, false).OfType<WorldEnvironment>())
            if (we.Environment != null) { we.Environment.AmbientLightEnergy = 0.1f; we.Environment.BackgroundEnergyMultiplier = 0.1f; }
        _d.Player.GlobalPosition = plate.GlobalPosition + Vector3.Up * 0.2f;
        net.Press(f, btn.NodeId);
        await Frames(12);
        await Shot("guc_gece.png", G(4, 0) + Vector3.Up * 4f, G(11, -8) + Vector3.Up * 1.2f);
    }

    async Task Shot(string file, Vector3 from, Vector3 at)
    {
        var cam = new Camera3D { Fov = 55f };
        _d.AddChild(cam);
        cam.GlobalPosition = from;
        cam.LookAt(at);
        cam.MakeCurrent();
        _d.Hud.Visible = false;
        await Frames(12);
        GetViewport().GetTexture().GetImage().SavePng(_out + file);
        GD.Print($"POWERSHOT shot {file}");
        cam.QueueFree();
    }
}
