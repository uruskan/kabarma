using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Karar 23 kaniti (--test=kemer): bot oyun yolunu oynarken goruntu alir, progress/kemer/ altina.
///   capa_kemerde   acilis: Capa kemerin 1. yuvasinda, elde
///   bos_el         bos kemer yuvasi secili (bot bir an durur; durum yalniz kemer secimi, sonra geri alinir)
///   uretim_eksik   Uretim paneli, Tas Firin secili: eksik malzeme kirmizi, kaynagi ve "Pusulada goster"
///   uretim_paneli  botun kendi Uretim paneli acilisi (Tezgah uretirken)
///   tezgah_kemerden Tezgah kemerden elde, hayalet gorunur; kurulunca tezgah_kuruldu, sonra cikis
/// Esya verilmez; bot kendi yolunu oynar.
/// </summary>
public partial class KemerShots : Node
{
    Director _d;
    float _t;
    int _phase;
    float _wait;
    bool _botHeld;
    readonly System.Collections.Generic.HashSet<string> _taken = new();

    public override void _Process(double delta)
    {
        _d ??= GetParent().GetNodeOrNull<Director>("Director");
        if (_d?.State == null || _d.Hud == null) return;
        float dt = (float)delta;
        _t += dt;
        if (_wait > 0) { _wait -= dt; return; }
        switch (_phase)
        {
            // Kapsulden ciktiktan sonra (bot ilk adimda kayaya yururken)
            case 0: if (_t > 3f && _d.Bot?.CurrentStep == "mine" && _d.Player.GlobalPosition.DistanceTo(_d.Pod.PadCenter) > 6f) { Shot("capa_kemerde"); _phase = 1; _wait = 0.6f; } break;
            case 1:
                Hold(true);
                _d.State.Belt.Select(_d.Data.Tool.BeltStart.Count + 2); // bos yuva
                _phase = 2; _wait = 0.4f; break;
            case 2: Shot("bos_el"); _phase = 3; _wait = 0.6f; break;
            case 3:
                _d.Hud.OpenCraft();
                _phase = 31; _wait = 0.2f; break;
            case 31: _d.Hud.CraftSelect("tas_firin"); _phase = 4; _wait = 0.4f; break;
            case 4: Shot("uretim_eksik"); _phase = 5; _wait = 0.6f; break;
            case 5:
                _d.Hud.CloseInventory();
                _d.State.Belt.Select(0);
                Hold(false);
                _phase = 6; break;
            case 6:
                if (_d.Hud.CraftOpen && _d.Hud.CraftStation == null && _d.Hud.CraftRowIds.Contains("tezgah")) Shot("uretim_paneli");
                if (_d.Capa.CurrentPlaceable == "tezgah" && _d.Capa.HeldItem == "tezgah" && _d.Bot != null && _d.Bot.CurrentStep == "tezgah")
                    ShotWhenNear();
                if (_d.Stations.Any(s => IsInstanceValid(s) && s.Def.Id == "tezgah")) { Shot("tezgah_kuruldu"); _phase = 7; _wait = 1.5f; }
                break;
            case 7:
                GD.Print($"KEMER RESULT ok shots={string.Join(",", _taken)}");
                GetTree().Quit(_taken.Count >= 5 ? 0 : 1);
                _phase = 8; break;
        }
    }

    float _nearT;
    void ShotWhenNear()
    {
        _nearT += (float)GetProcessDeltaTime();
        if (_nearT > 1.2f) Shot("tezgah_kemerden");
    }

    void Hold(bool on)
    {
        if (_d.Bot == null || _botHeld == on) return;
        _botHeld = on;
        _d.Bot.ProcessMode = on ? ProcessModeEnum.Disabled : ProcessModeEnum.Inherit;
        if (on) _d.Player.MoveOverride = Vector3.Zero;
    }

    void Shot(string name)
    {
        if (!_taken.Add(name)) return;
        var img = GetViewport().GetTexture().GetImage();
        var dir = ProjectSettings.GlobalizePath("res://").TrimEnd('/') + "/../progress/kemer";
        DirAccess.MakeDirRecursiveAbsolute(dir);
        img.SavePng($"{dir}/{name}.png");
        GD.Print($"KEMER SHOT {name}");
    }
}
