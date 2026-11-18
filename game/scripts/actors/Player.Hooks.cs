using Godot;

namespace Kabarma.Game;

/// <summary>
/// Oyun olaylari -> vizor ifadeleri (visor.json "events") ve kodla pozlar: yemek yenince eat, uyuyunca sleep,
/// yakindan tohum ekilince plant. Ayrica goruntu kaniti icin sinama bayraklari:
///   --pose=plant|eat|sleep|getup_front|getup_back|push_cart|wade|ID   2 sn sonra pozu/katmani acar
///   --expr=mutlu|saskin|yorgun|korkmus|...                            vizor ifadesini sabitler
///   --knocktest                                                       6 sn sonra hasarsiz yere serilme (GDD §3.2)
///   --drive=f|b|l|r[s][a]                                             kameraya gore surekli yuru (s: depar, a: ileri nisan)
///   --expr=cycle                                                      her ifade 1,5 sn (1,0 sn'den), EXPR satiri yazar
///   --jumptest                                                        2 sn'de bir gercek "jump" eylemiyle zipla
///   --rigtest                                                         0,5 sn'de bir RIG satiri (klip, hiz, dayaniklilik, tutus, vizor)
///   --aim, --closeup=ACI, --headcycle, --cartpush                     tutulan nesne sinamalari (Player.GripDebug.cs)
/// </summary>
public partial class Player
{
    void HookEvents()
    {
        if (GetParent() is not Director d) return;
        Events.Ate += _ => { PlayPose("eat"); Visor?.Event("yemek"); };
        Events.Slept += () => PlayPose("sleep", 4f);
        Events.Sold += (_, _, _) => Visor?.Event("satis");
        Events.CropShipped += (_, _, _) => Visor?.Event("satis");
        Events.CardPicked += _ => Visor?.Event("kart");
        Events.NightStarted += () => Visor?.Event("gece");
        Events.Dawn += () => Visor?.Event("safak");
        Events.Harvested += _ => Visor?.Event("hasat");
        Events.CreatureKilled += (_, swollen) => { if (swollen) Visor?.Event("kabarik_av"); };
        d.State.Farm.Planted += p =>
        {
            Visor?.Event("ekim");
            // Diz cokup ekme: ekilen kare yakinsa ve oyuncu duruyorsa bir tur (uzaktan Capa ile ekerken poz yok).
            var at = d.FarmView.CellCenter(p.X, p.Y);
            if (at.DistanceTo(GlobalPosition) < 3f && new Vector3(Velocity.X, 0, Velocity.Z).Length() < 0.5f && !Posed)
                PlayPose("plant", ClipSeconds("plant"));
        };
        DebugFlags();
    }

    void DebugFlags()
    {
        var a = Main.Args;
        if (a.ContainsKey("pose") || a.ContainsKey("expr") || a.ContainsKey("knocktest") || a.ContainsKey("drive"))
            GetTree().CreateTimer(0.5).Timeout += () => GD.Print($"PLAYER at {GlobalPosition} visualYaw={Visual.Rotation.Y:0.00} bag={Rig?.Body.HasBag}");
        if (a.TryGetValue("pose", out var pose))
            GetTree().CreateTimer(2.0).Timeout += () =>
            {
                switch (pose)
                {
                    case "push_cart": PushingCart = true; break;
                    case "wade": WadeDepth = 1f; break;
                    case "getup_front": BeginGetup(true, false); break;
                    case "getup_back": BeginGetup(false, false); break;
                    case "sleep": PlayPose("sleep", 600f); break;
                    default: PlayPose(pose, 600f, true); break;
                }
                GD.Print($"POSE {pose} clip={_cur}");
            };
        if (a.TryGetValue("expr", out var expr) && expr != "cycle")
            GetTree().CreateTimer(1.0).Timeout += () => Visor?.Hold(expr);
        if (expr == "cycle" && GetParent() is Director dd)
        {
            // Her ifade 1,5 sn: 1,0 sn'den baslar (goruntu kaniti icin --shots=1.9,3.4,4.9,...).
            int i = 0;
            foreach (var name in dd.Data.Visor.Expressions.Keys)
            {
                string n = name; float t = 1.0f + 1.5f * i++;
                GetTree().CreateTimer(t).Timeout += () => { Visor?.Hold(n); GD.Print($"EXPR {n} t={t:0.0}"); };
            }
        }
        if (a.ContainsKey("rigtest"))
        {
            var tm = new Timer { WaitTime = 0.5, Autostart = true };
            AddChild(tm);
            tm.Timeout += () => GD.Print($"RIG t={Time.GetTicksMsec() / 1000f:0.0} clip={_curId}->{_cur} mode={_mode} sp={new Vector3(Velocity.X, 0, Velocity.Z).Length():0.0} sprint={_sprinting} stam={Stamina?.Value:0} ex={Stamina?.Exhausted} {GripReport()} down={Down} visor={Visor?.Expression}");
        }
        DebugGrips(); // Player.GripDebug.cs
        if (a.TryGetValue("knocktest", out var kt)) // --knocktest: geriye (sirtustu), --knocktest=front: one (yuzustu); kapsul acilisindan (~5,1 sn) sonra
            GetTree().CreateTimer(6.0).Timeout += () => Knockdown(Visual.GlobalBasis.Z * (kt == "front" ? 4f : -3f) + Vector3.Up * 2f);
        if (a.TryGetValue("drive", out var drive))
            GetTree().CreateTimer(1.0).Timeout += () => _drive = drive;
    }

    string _drive;
    float _jumpT = -1f;

    public override void _Process(double delta)
    {
        // --jumptest: 2 sn'de bir zipla (gercek girdi eylemiyle)
        if (Main.Args.ContainsKey("jumptest"))
        {
            if (Input.IsActionPressed("jump")) Input.ActionRelease("jump");
            _jumpT += (float)delta;
            if (_jumpT >= 2f) { _jumpT = 0f; Input.ActionPress("jump"); }
        }
        TickGripDebug(); // Player.GripDebug.cs
        if (_drive == null) return;
        var b = Yaw.GlobalBasis;
        var dir = _drive[0] switch { 'b' => b.Z, 'l' => -b.X, 'r' => b.X, _ => -b.Z };
        dir.Y = 0;
        MoveOverride = dir.Normalized() * (_drive.Contains('s') ? 1.5f : 1f);
        if (_drive.Contains('a')) AimOverride = GlobalPosition + (-b.Z with { Y = 0 }).Normalized() * 20f + Vector3.Up * 1.2f;
    }
}
