using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Gercek girdi surucusu (--test=input --device=kbm|pad; TEKNIK §11 "insan giris yolu", URETIM §6 M1 kabulu).
/// Bot ayni adim kaydiyla (senaryo + _steps) karar verir; surucu kararlari Input.ParseInputEvent ile sentetik
/// klavye-fare ya da oyun kolu olaylarina cevirir. Tus adlari koda yazilmaz: her olay input.json baglamasindan
/// (Controls) gelir; aygitta baglamasi olmayan eylem hata verir. Oyun olayi gercek yoldan isler: Player (hareket,
/// fare bakisi, ziplama), Director.ReadInput (tetik, baslik, cark, menu, kart, E), Director.PadLook (sag cubuk).
///   Hareket: dunya yonu kameraya gore cevrilir; klavyede 8 yon (WASD), kolda sol cubuk.
///   Nisan: kamera isini hedefe yakinsayana dek fare hareketi / sag cubuk (P denetimi). Tetik nisan oturunca basilir.
///   Ayrik eylemler sirayla: tus/dugme basili tut → birak → bekle. Kolda baslik carki (LB + sag cubuk) ve menu imleci (✚ + Ⓐ).
/// </summary>
public sealed class InputDriver
{
    readonly Director _d;
    readonly Bot _bot;
    readonly Controls _c;
    public InputDevice Device { get; }
    public InputDriverDef Config { get; }
    public string Error { get; private set; }
    /// <summary>--botdebug: her ayrik basisi yazar.</summary>
    readonly bool Debug = Main.Args.ContainsKey("botdebug");

    // Botun son karari (dogrudan kipteki gibi kalicidir: yeni deger gelene dek gecerli).
    Vector3 _move;
    Vector3? _aim;
    bool _fireHold, _fireTap;
    float _fireWait;
    int _fireTapFrames;
    bool _accept;
    // Gorus engeli: engelli sure, yan adim suresi, yonu, ust uste deneme
    float _occT, _sideT, _sinceTap = 99f;
    int _sideSign = 1, _sideN;
    // Sag cubuk bakisinin son karelerdeki zaman adimi (Director.LookDt)
    readonly float[] _ldt = new float[8];
    int _ldtI;

    // Gonderilmis durum: ayni degeri tekrar gondermemek icin (anahtar: "btn:<belirtec>" ya da "axis:<eksen>").
    readonly Dictionary<string, float> _sent = new();
    readonly Queue<Op> _ops = new();
    Op _cur;

    // Istatistik (RESULT satiri ve kanit)
    public int Events, Taps, Wheels, MenuNavs, MouseMoves, Sidesteps;
    public readonly HashSet<string> UsedActions = new();

    /// <summary>Sin(22,5°): klavyede 8 yonden birine yuvarlama esigi.</summary>
    static readonly float Digital = Mathf.Sin(Mathf.Pi / 8f);

    public InputDriver(Director d, Bot bot, InputDevice device)
    {
        _d = d; _bot = bot; Device = device;
        _c = d.Controls;
        if (_c == null) { Error = "no_controls"; Config = new InputDriverDef(); return; }
        Config = _c.Data.Driver;
        Preflight();
        _ops.Enqueue(new ProbeOp());
        _ops.Enqueue(new HeadSweepOp());
        GD.Print($"INPUT driver device={Device} problems={_c.Problems.Count} user={_c.UserPath ?? "-"}");
    }

    /// <summary>Zincir bitince bir kez: kanit satiri (aygit, olay sayilari, kullanilan eylemler).</summary>
    public string Summary() =>
        $"INPUT summary device={Device} events={Events} taps={Taps} wheels={Wheels} menuNavs={MenuNavs} mouseMoves={MouseMoves} sidesteps={Sidesteps} last={_c.Last} actions={string.Join(",", UsedActions.OrderBy(a => a))}";

    public static InputDevice ParseDevice(string s) => s?.ToLowerInvariant() switch
    {
        "pad" or "kol" or "gamepad" or "joy" => InputDevice.Pad,
        _ => InputDevice.Kbm,
    };

    /// <summary>Surucunun kullanacagi her eylemin bu aygitta baglamasi olmali; cozulemeyen belirtec olmamali.</summary>
    void Preflight()
    {
        if (_c.Problems.Count > 0) { Error = "unresolved_bindings " + string.Join("; ", _c.Problems); return; }
        var need = new List<string> { "move_forward", "move_back", "move_left", "move_right", "sprint", "fire", "interact", "bag", "ui_accept", "ui_cancel", "build_next", "card_1", "card_2", "card_3", "head_wheel" };
        if (Device == InputDevice.Pad) need.AddRange(new[] { "look_left", "look_right", "look_up", "look_down", "ui_up", "ui_down" });
        else need.AddRange(Enumerable.Range(1, 8).Select(i => $"head_{i}"));
        var missing = need.Where(a => _c.Bindings(a, Device).Count == 0).ToList();
        if (missing.Count > 0) Error = $"no_binding device={Device} actions={string.Join(",", missing)}";
    }

    string Token(string action)
    {
        var b = _c.Bindings(action, Device);
        if (b.Count == 0) { Error ??= $"no_binding action={action} device={Device}"; return null; }
        UsedActions.Add(action);
        return b[0];
    }

    // ---------- Botun kararlari ----------

    /// <summary>Kare basi: ayrik bir niyet yalnizca surucu bostayken kabul edilir (ayni karedeki niyetler sirayla eklenir).</summary>
    public void BeginFrame() => _accept = _cur == null && _ops.Count == 0;

    void Enqueue(Op op) { if (_accept) _ops.Enqueue(op); }

    public void Tap(string action) => Enqueue(new TapOp(action));

    public void SelectHead(int head) { var op = HeadOp(head, false); if (op != null) Enqueue(op); }

    /// <summary>
    /// Baslik secen islem: her aygitta cark (Q / LB + fare / sag cubuk). M1 karari 20: 1-8 kemer yuvasini secer, baslik
    /// secmez; <paramref name="wheel"/> eski cagrilar icin kaldi.
    /// </summary>
    Op HeadOp(int head, bool wheel)
    {
        string a = $"head_{head + 1}";
        if (_c.Bindings("head_wheel", Device).Count > 0) return new WheelOp(head);
        Error ??= $"no_binding action={a} device={Device}";
        return null;
    }

    public void MenuPick(int i)
    {
        string a = $"head_{i + 1}";
        if (Device == InputDevice.Kbm && _c.Bindings(a, Device).Count > 0) { Enqueue(new TapOp(a)); return; }
        Enqueue(new MenuNavOp(i));
        Enqueue(new TapOp("ui_accept"));
    }

    /// <summary>Isleyicinin alan cikislarini alir (hareket ve nisan dogrudan kipteki gibi kalici) ve oyuncudan siler.</summary>
    public void Take(Player p, bool fire, bool firePressed)
    {
        if (p.MoveOverride is Vector3 m) { _move = m; p.MoveOverride = null; }
        if (p.AimOverride is Vector3 a) { _aim = a; p.AimOverride = null; }
        _fireHold = fire;
        if (firePressed) { _fireTap = true; _sinceTap = 0f; }
    }

    // ---------- Kare ----------

    public void Tick(float dt)
    {
        if (Error != null) return;
        if (_cur == null && _ops.Count > 0) _cur = _ops.Dequeue();
        bool ownsLook = _cur?.OwnsLook == true; // cark acikken fare / sag cubuk carka gider, nisan beklenir
        if (_cur != null && _cur.Step(this)) _cur = null;
        CheckOcclusion(dt);
        DriveMove();
        if (!ownsLook) DriveLook();
        DriveFire(dt);
    }

    /// <summary>
    /// Gorus engeli: nisan oturdugu halde kamera isini hedefe varmadan baska bir seye (agac, istasyon) carpiyorsa insan
    /// yana adim atar. Bot yalnizca hedefi ve duracagi yeri bilir (dogrudan kipte nisan engelin icinden gecer); surucu kisa
    /// bir yan adim ekler, her denemede yonu degistirip uzatir (zikzak arama). Silah basliginda yapilmaz (hareketli hedef).
    /// </summary>
    void CheckOcclusion(float dt)
    {
        _sinceTap += dt;
        if (_sideT > 0f) { _sideT -= dt; return; }
        // Bot is yapmak istiyor: tetigi basili tutuyor ya da yakin zamanda basis istedi (basis nisan oturunca hemen
        // harcanir; engel varken bot aralikla yeniden ister: satis, yerlestirme).
        bool wantFire = _fireHold || _fireTap || _sinceTap < 2f * Config.OccludeSeconds + 0.5f;
        bool want = wantFire && _aim is Vector3 && _move.LengthSquared() < 1e-4f && _cur == null && !Capa.IsWeapon(_d.Capa.Current);
        if (!want || AimMiss() > Config.AimGateMeters) { _occT = 0f; return; }
        var cam = _d.Player.Cam.GlobalPosition;
        var t = _aim.Value;
        var hit = _d.Player.AimPoint(out _, 60f);
        bool occluded = cam.DistanceTo(hit) < cam.DistanceTo(t) - 0.5f && hit.DistanceTo(t) > Config.OccludeMeters;
        if (!occluded) { _occT = 0f; _sideN = 0; return; }
        _occT += dt;
        if (_occT < Config.OccludeSeconds) return;
        _occT = 0f;
        _sideN++;
        _sideSign = -_sideSign;
        _sideT = Config.SidestepSeconds * Mathf.Min(_sideN, 4);
        Sidesteps++;
        if (Debug) GD.Print($"INPUTDBG sidestep n={_sideN} sign={_sideSign} hit={hit} aim={t}");
    }

    public string Status()
    {
        var p = _d.Player;
        var hit = p.AimPoint(out var node, 60f);
        return $"op={_cur?.GetType().Name ?? "-"} queue={_ops.Count} aimErr={AimErrorDeg():0.0}deg miss={AimMiss():0.00}m aim={_aim} hit={hit} node={(node as Node)?.Name} muzzle->hit={p.Muzzle.GlobalPosition.DistanceTo(hit):0.0} " +
               $"pos={p.GlobalPosition} move={_move} fire={_fireHold}/{_fireTap} pressed={Input.IsActionPressed("fire")} head={_d.Capa.Current} menu={_d.Hud.MenuOpen} wheel={_d.Hud.WheelOpen} lock={p.LookLocked} " +
               $"look={Input.GetVector("look_left", "look_right", "look_up", "look_down")} sent={string.Join(",", _sent.Where(kv => kv.Value != 0).Select(kv => $"{kv.Key}={kv.Value}"))} bag={_d.State.Backpack.UsedSlots}/{_d.State.Backpack.Slots} events={Events}";
    }

    void Emit(InputEvent e)
    {
        if (e == null) return;
        Input.ParseInputEvent(e);
        Events++;
    }

    /// <summary>Dugme ya da tus: basili / birakildi (degisince gonderilir). Eksen belirteci (tetik) tam deger alir.</summary>
    void Button(string token, bool down)
    {
        if (token == null) return;
        string key = "btn:" + token;
        float v = down ? 1f : 0f;
        if (_sent.TryGetValue(key, out var old) ? old == v : !down) return;
        _sent[key] = v;
        Emit(Controls.DeviceEvent(token, Device, down));
    }

    /// <summary>
    /// Bir eksen cifti (sol/sag, ileri/geri, bakis): kolda ayni eksene baglaniysa tek eksen olayi (isaretli deger),
    /// degilse esik ustunde tus basimi (klavye 8 yon).
    /// </summary>
    void Pair(string negAction, string posAction, float v)
    {
        string tn = Token(negAction), tp = Token(posAction);
        if (tn == null || tp == null) return;
        if (Controls.IsAxis(tn, out var an, out _) && Controls.IsAxis(tp, out var ap, out _) && an == ap)
        {
            float q = Mathf.Snapped(Mathf.Clamp(v, -1f, 1f), 0.01f);
            string key = "axis:" + ap;
            if (_sent.TryGetValue(key, out var old) ? Mathf.IsEqualApprox(old, q) : q == 0f) return;
            _sent[key] = q;
            Emit(Controls.DeviceEvent(q >= 0 ? tp : tn, Device, q != 0f, Mathf.Abs(q)));
            return;
        }
        Button(tp, v > Digital);
        Button(tn, v < -Digital);
    }

    // ---------- Hareket ----------

    void DriveMove()
    {
        var m = _move; m.Y = 0;
        bool sprint = m.Length() > 1.01f;
        if (_sideT > 0f) { var right = _d.Player.Yaw.GlobalBasis.X; right.Y = 0; m += right.Normalized() * _sideSign; }
        var inp = Vector2.Zero;
        if (m.LengthSquared() > 1e-4f)
        {
            var n = m.Normalized();
            var basis = _d.Player.Yaw.GlobalBasis;
            inp = new Vector2(n.Dot(basis.X.Normalized()), n.Dot(basis.Z.Normalized()));
        }
        Pair("move_left", "move_right", inp.X);
        Pair("move_forward", "move_back", inp.Y);
        Button(Token("sprint"), sprint && inp != Vector2.Zero);
    }

    // ---------- Nisan ----------

    static float YawOf(Vector3 v) => Mathf.Atan2(-v.X, -v.Z);

    /// <summary>Kamera isinin hedefe acisal hatasi (yaw, pitch), radyan.</summary>
    Vector2 AimError()
    {
        if (_aim is not Vector3 t) return Vector2.Zero;
        var cam = _d.Player.Cam;
        var dir = (t - cam.GlobalPosition).Normalized();
        var fwd = -cam.GlobalBasis.Z;
        float yaw = Mathf.Wrap(YawOf(dir) - YawOf(fwd), -Mathf.Pi, Mathf.Pi);
        float pitch = Mathf.Asin(Mathf.Clamp(dir.Y, -1, 1)) - Mathf.Asin(Mathf.Clamp(fwd.Y, -1, 1));
        // Kamera egimi sinirdaysa o yondeki hata giderilemez: nisan kapisini kilitlemesin.
        var lk = _c.Data.Look;
        float px = _d.Player.Pitch.Rotation.X;
        if ((pitch < 0 && px <= lk.PitchMin + 0.01f) || (pitch > 0 && px >= lk.PitchMax - 0.01f)) pitch = 0;
        return new Vector2(yaw, pitch);
    }

    float AimErrorDeg() => Mathf.RadToDeg(AimError().Length());

    /// <summary>
    /// Kamera isinin hedefte tahmini iskalama mesafesi (m). Yere yakin hedefte (tarla karesi) egim hatasi yere
    /// yatik acida buyur: d x egim hatasi / sin(bakis acisi). Nisan kapisi aci degil bu mesafeyle denetlenir.
    /// </summary>
    float AimMiss()
    {
        if (_aim is not Vector3 t) return 0f;
        var to = t - _d.Player.Cam.GlobalPosition;
        float d = to.Length();
        if (d < 0.01f) return 0f;
        var e = AimError();
        float sinGraze = Mathf.Max(Mathf.Abs(to.Y) / d, 0.15f);
        return d * Mathf.Sqrt(e.X * e.X + (e.Y / sinGraze) * (e.Y / sinGraze));
    }

    void DriveLook()
    {
        var e = AimError();
        var lk = _c.Data.Look;
        if (Device == InputDevice.Kbm)
        {
            // Player._Input: Yaw.RotateY(-rel.X * sens), Pitch.X -= rel.Y * sens
            var rel = new Vector2(-e.X, -e.Y) * Config.LookGain / lk.MouseSens;
            if (rel.LengthSquared() > 0.25f) { Emit(new InputEventMouseMotion { Relative = rel, Device = 0 }); MouseMoves++; }
            return;
        }
        // Director.PadLook: aci = yon x |cubuk|^egri x azami hiz x dt; olu bolge GetVector'da (dairesel). Bir sonraki karede
        // hatanin PadLookGain kadarini kapatacak hiz istenir (kare suresinden bagimsiz; olay bir kare gec isler, oran
        // 0.25-0.35 salinmadan yakinsar). Saniye basina kazanc --fast'in uzun karelerinde hedefi asip salinir.
        // Sonraki karenin suresi bilinmez (basliksiz --fast'ta kareler 2 ms ile 100 ms arasi oynar): son 8 karenin en
        // uzunu alinir; kisa karede az doner (yavas yakinsar), uzun karede hedefi asmaz (salinmaz).
        _ldt[_ldtI++ % _ldt.Length] = _d.LookDt;
        float ldt = Mathf.Max(_ldt.Max(), 1e-3f);
        var o = new Vector2(-e.X * Config.PadLookGain / (lk.PadYawSpeed * ldt), -e.Y * Config.PadLookGain / (lk.PadPitchSpeed * ldt));
        float m = o.Length();
        var raw = Vector2.Zero;
        if (m > 0.004f)
        {
            float dz = _c.Data.Deadzone;
            float sm = Mathf.Pow(Mathf.Min(1f, m), 1f / lk.Curve);
            raw = o / m * (dz + sm * (1f - dz) + 0.01f);
        }
        Pair("look_left", "look_right", raw.X);
        Pair("look_up", "look_down", raw.Y);
        if (_trace) GD.Print($"AIMTRACE f={Engine.GetProcessFrames()} err=({Mathf.RadToDeg(e.X):0.00},{Mathf.RadToDeg(e.Y):0.00}) miss={AimMiss():0.00} lookDt={_d.LookDt:0.000} ldt={ldt:0.000} raw=({raw.X:0.00},{raw.Y:0.00}) look={Input.GetVector("look_left", "look_right", "look_up", "look_down")} yaw={_d.Player.Yaw.Rotation.Y:0.0000} pitch={_d.Player.Pitch.Rotation.X:0.0000} fire={_fireHold}/{_fireTap}");
    }

    readonly bool _trace = Main.Args.ContainsKey("aimtrace");

    // ---------- Tetik ----------

    void DriveFire(float dt)
    {
        string tok = Token("fire");
        if (_fireTapFrames > 0)
        {
            _fireTapFrames--;
            if (_fireTapFrames == Config.GapFrames) Button(tok, false);
            return;
        }
        // Baslik degisirken ya da menu islerken tetik bekler (insan da once basligi takar, sonra basar).
        if (_cur != null || _ops.Count > 0) { Button(tok, false); return; }
        bool want = _fireHold || _fireTap;
        bool aimed = AimMiss() <= Config.AimGateMeters;
        _fireWait = want && !aimed ? _fireWait + dt : 0f;
        // Silahla hareketli hedefe nisan tam oturmayabilir: bir sure sonra yine basilir. Is basliginda (sur, sula, kur)
        // nisan oturmadan basilmaz; yanlis kareyi surmek oyunu bozar (insan da once nisan alir).
        bool may = aimed || (Capa.IsWeapon(_d.Capa.Current) && _fireWait >= Config.AimTimeoutSeconds);
        if (_fireTap && may)
        {
            // Basis kenari: basiliysa once birak, sonraki karede bas.
            if (_sent.TryGetValue("btn:" + tok, out var v) && v > 0) { Button(tok, false); return; }
            Button(tok, true);
            _fireTap = false;
            _fireTapFrames = Config.HoldFrames + Config.GapFrames;
            Taps++;
            return;
        }
        Button(tok, _fireHold && may);
    }

    // ---------- Ayrik islemler ----------

    abstract class Op
    {
        protected int F;
        /// <summary>Bir kare ilerletir; bitince true.</summary>
        public bool Step(InputDriver d) { bool done = Run(d); F++; return done; }
        /// <summary>Bu islem bakis girdisini (fare, sag cubuk) kullaniyor: nisan surucusu o sure susar.</summary>
        public virtual bool OwnsLook => false;
        protected abstract bool Run(InputDriver d);
    }

    /// <summary>Tus/dugme: basili tut, birak, bekle.</summary>
    sealed class TapOp : Op
    {
        readonly string _action;
        public TapOp(string action) { _action = action; }
        protected override bool Run(InputDriver d)
        {
            var tok = d.Token(_action);
            if (tok == null) return true;
            if (F == 0) { d.Button(tok, true); d.Taps++; if (d.Debug) GD.Print($"INPUTDBG tap {_action} ({tok}) frame={Engine.GetProcessFrames()} menu={d._d.Hud.MenuOpen}"); }
            else if (F == d.Config.HoldFrames) d.Button(tok, false);
            return F >= d.Config.HoldFrames + d.Config.GapFrames;
        }
    }

    /// <summary>
    /// Acilis denetimi: sekiz basligin hepsi bu aygitla secilebiliyor mu. Klavye: 1-8 tuslari ve Q + fare carki; kol:
    /// LB + sag cubuk carki. Senaryo bazi basliklari adim basinda kendisi taktigi icin (Director.Scenario OnStepStart)
    /// zincir hepsine basmaz; bu tarama eksigi kapatir.
    /// </summary>
    sealed class HeadSweepOp : Op
    {
        int _i = -1;
        Op _sub;
        string _miss = "";
        public override bool OwnsLook => _sub?.OwnsLook == true;
        protected override bool Run(InputDriver d)
        {
            int n = Capa.HeadNames.Length;
            if (_sub != null)
            {
                if (!_sub.Step(d)) return false;
                _sub = null;
                int want = _i % n;
                if ((int)d._d.Capa.Current != want) _miss += $" {(_i < n && d.Device == InputDevice.Kbm ? "tus" : "cark")}:{Capa.HeadNames[want]}->{d._d.Capa.Current}";
            }
            if (d.Error != null) return true;
            // Klavyede once tuslar (0..n-1), sonra cark (n..2n-1); kolda yalnizca cark.
            int total = d.Device == InputDevice.Kbm ? 2 * n : n;
            if (++_i >= total)
            {
                if (_miss.Length > 0) d.Error = $"head sweep{_miss}";
                else GD.Print($"INPUT heads ok device={d.Device} n={n} via={(d.Device == InputDevice.Kbm ? "keys+wheel" : "wheel")}");
                return true;
            }
            _sub = d.HeadOp(_i % n, d.Device == InputDevice.Pad || _i >= n);
            return _sub == null;
        }
    }

    /// <summary>Cark: Q / LB basili (cark acilir), fare ya da sag cubuk baslik yonune, birak (Director.ReadInput takar).</summary>
    sealed class WheelOp : Op
    {
        readonly int _head;
        public WheelOp(int head) { _head = head; }
        public override bool OwnsLook => true;
        protected override bool Run(InputDriver d)
        {
            var tok = d.Token("head_wheel");
            if (tok == null) return true;
            int hold = d.Config.WheelHoldFrames;
            if (d._trace) GD.Print($"WHEELTRACE head={_head} F={F} open={d._d.Hud.WheelOpen} look={Input.GetVector("look_left", "look_right", "look_up", "look_down")} lb={Input.IsActionPressed("head_wheel")} cur={d._d.Capa.Current}");
            if (F == 0)
            {
                // Insan da once sag cubugu birakir, sonra LB'ye basar: nisandan kalan cubuk degeri carka sizmasin.
                if (d.Device == InputDevice.Pad) { d.Pair("look_left", "look_right", 0); d.Pair("look_up", "look_down", 0); }
                d.Button(tok, true); d.Wheels++; return false;
            }
            if (F <= hold)
            {
                // Hud.WheelMotion: yuva i, ust ortadan saat yonunde i x 360/n derece; ekran y asagi.
                int n = Capa.HeadNames.Length;
                float a = _head * Mathf.Tau / n;
                var dir = new Vector2(Mathf.Sin(a), -Mathf.Cos(a));
                if (d.Device == InputDevice.Kbm)
                {
                    // Player._Input: kilitliyken fare hareketi carka gider (Hud.WheelMotion biriktirir, rel x 0.02).
                    if (F > 1) { d.Emit(new InputEventMouseMotion { Relative = dir * 60f, Device = 0 }); d.MouseMoves++; }
                    return false;
                }
                d.Pair("look_left", "look_right", dir.X);
                d.Pair("look_up", "look_down", dir.Y);
                return false;
            }
            if (F == hold + 1)
            {
                if (d.Device == InputDevice.Pad)
                {
                    d.Pair("look_left", "look_right", 0);
                    d.Pair("look_up", "look_down", 0);
                }
                d.Button(tok, false);
                return false;
            }
            if (F >= hold + 1 + d.Config.GapFrames)
            {
                if ((int)d._d.Capa.Current != _head) GD.Print($"INPUT wheel miss want={Capa.HeadNames[_head]} got={d._d.Capa.Current}");
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Acilis denetimi (zincirden once): eylemi yedek tusa/dugmeye ata, bas, eylemin geldigini, son aygitin ve glifin
    /// degistigini gor; birak, varsayilana don, baglamanin ve kalici dosyanin geri geldigini gor.
    /// </summary>
    sealed class ProbeOp : Op
    {
        string _act, _alt, _def, _glyphAlt;
        bool _pressedSeen;
        protected override bool Run(InputDriver d)
        {
            var c = d._c;
            var cfg = d.Config;
            if (F == 0)
            {
                _act = cfg.ProbeAction;
                _alt = d.Device == InputDevice.Kbm ? cfg.ProbeKb : cfg.ProbePad;
                var b = c.Bindings(_act, d.Device);
                _def = b.Count > 0 ? b[0] : null;
                if (_def == null || !c.Rebind(_act, d.Device, _alt)) { d.Error = $"probe rebind {_act}->{_alt}"; return true; }
                _glyphAlt = c.Glyph(_act, d.Device);
                if (c.UserPath != null && !(FileAccess.FileExists(c.UserPath) && FileAccess.GetFileAsString(c.UserPath).Contains(_alt)))
                { d.Error = $"probe user file {c.UserPath} has no {_alt}"; return true; }
                d.Button(_alt, true);
                return false;
            }
            if (F <= d.Config.HoldFrames + 1) { _pressedSeen |= Input.IsActionPressed(_act); return false; }
            if (F == d.Config.HoldFrames + 2)
            {
                d.Button(_alt, false);
                if (!_pressedSeen) { d.Error = $"probe {_alt} did not trigger {_act}"; return true; }
                if (c.Last != d.Device) { d.Error = $"probe last device {c.Last} != {d.Device}"; return true; }
                if (c.Expand("{act:" + _act + "}") != _glyphAlt) { d.Error = $"probe glyph {c.Expand("{act:" + _act + "}")} != {_glyphAlt}"; return true; }
                c.ResetAll();
                var now = c.Bindings(_act, d.Device);
                if (now.Count == 0 || now[0] != _def) { d.Error = $"probe reset {_act} -> {(now.Count > 0 ? now[0] : "-")}"; return true; }
                if (c.UserPath != null && FileAccess.GetFileAsString(c.UserPath).Contains("\"" + _alt + "\"")) { d.Error = "probe user file not cleared"; return true; }
                GD.Print($"INPUT probe ok device={d.Device} {_act}: {_def}->{_alt}->{_def} glyph {_glyphAlt}->{c.Glyph(_act, d.Device)} last={c.Last} user={c.UserPath ?? "-"}");
                return false;
            }
            return F >= d.Config.HoldFrames + 2 + d.Config.GapFrames;
        }
    }

    /// <summary>Kol: menu imlecini ✚ ile hedef satira tasi (kisa yoldan); menu kapanirsa vazgec.</summary>
    sealed class MenuNavOp : Op
    {
        readonly int _target;
        string _tok;
        int _tapF = -1, _taps;
        public MenuNavOp(int target) { _target = target; }
        protected override bool Run(InputDriver d)
        {
            if (F == 0) d.MenuNavs++;
            if (_tapF >= 0)
            {
                _tapF++;
                if (_tapF == d.Config.HoldFrames) d.Button(_tok, false);
                if (_tapF >= d.Config.HoldFrames + d.Config.GapFrames) _tapF = -1;
                return false;
            }
            if (!d._d.Hud.MenuOpen) return true;
            int n = d._d.Hud.MenuCount, cur = d._d.MenuCursorIndex;
            if (n == 0 || cur == _target || _target >= n) return true;
            if (_taps++ > 2 * n + 4) { GD.Print($"INPUT menu nav gave up target={_target} cur={cur}"); return true; }
            int down = ((_target - cur) % n + n) % n;
            _tok = d.Token(down <= n - down ? "ui_down" : "ui_up");
            if (_tok == null) return true;
            d.Button(_tok, true);
            _tapF = 0;
            return false;
        }
    }
}
