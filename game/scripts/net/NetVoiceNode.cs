using System.Collections.Generic;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Sesli sohbet (GDD 22.1: yakinlik sesi, uzak mesafe icin telsiz; TEKNIK 5.4). Bas-konus: voice_ptt (T / Start) yakin
/// ses, voice_radio (Y / ✚↓) telsiz. Yakalama AudioEffectCapture ile ("NetMic" kanali, sessiz), 16 kHz'e indirilir,
/// mu-law ile 20 ms'lik cercevelere bolunur (NetVoice). Alici her konusan icin avatarin ustunde bir
/// AudioStreamPlayer3D + AudioStreamGenerator calar; Godot'un uzaklik zayiflamasi kapali, kazanc NetVoice.Gain'den.
/// Basliksiz kipte mikrofon yok: sinama <see cref="InjectTone"/> ile sentetik cerceve yollar.
/// Steam Voice kullanilmadi: ayni yol iki tasimada da calisir (Steam'de de mu-law).
/// </summary>
public partial class NetVoiceNode : Node
{
    readonly NetSync _net;
    AudioEffectCapture _capture;
    AudioStreamPlayer _mic;
    readonly List<float> _pending = new();
    ushort _seq;
    float _talkT;
    readonly Dictionary<byte, (AudioStreamPlayer3D player, AudioStreamGeneratorPlayback pb, bool radio)> _out = new();
    /// <summary>Alinan cerceve sayisi (es basina) ve gonderilen: sinama ve oyuncu listesi.</summary>
    public readonly Dictionary<byte, int> Received = new();
    public int Sent;
    public bool Talking => _talkT > 0;
    bool _radio;

    NetVoiceDef V => _net.Cfg.Voice;

    public NetVoiceNode(NetSync net) { _net = net; }
    public NetVoiceNode() { }

    public override void _Ready()
    {
        if (DisplayServer.GetName() == "headless" || Main.Args.ContainsKey("mute")) return; // mikrofon yok
        int bus = AudioServer.GetBusIndex("NetMic");
        if (bus < 0)
        {
            AudioServer.AddBus();
            bus = AudioServer.BusCount - 1;
            AudioServer.SetBusName(bus, "NetMic");
            AudioServer.SetBusMute(bus, true); // kendi sesini duymasin (Godot mikrofon ornegi gibi)
            AudioServer.AddBusEffect(bus, new AudioEffectCapture { BufferLength = 0.5f });
        }
        _capture = AudioServer.GetBusEffect(bus, 0) as AudioEffectCapture;
        _mic = new AudioStreamPlayer { Stream = new AudioStreamMicrophone(), Bus = "NetMic" };
        AddChild(_mic);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _talkT -= dt;
        bool ptt = InputMap.HasAction("voice_ptt") && Input.IsActionPressed("voice_ptt");
        bool radio = InputMap.HasAction("voice_radio") && Input.IsActionPressed("voice_radio");
        if (_capture != null)
        {
            bool on = (ptt || radio) && _net.Joined;
            if (on && !_mic.Playing) _mic.Play();
            if (!on && _mic.Playing) { _mic.Stop(); _capture.ClearBuffer(); _pending.Clear(); }
            if (on)
            {
                _radio = radio;
                int n = _capture.GetFramesAvailable();
                if (n > 0)
                {
                    var buf = _capture.GetBuffer(n);
                    var mono = new float[buf.Length];
                    for (int i = 0; i < buf.Length; i++) mono[i] = (buf[i].X + buf[i].Y) * 0.5f;
                    _pending.AddRange(NetVoice.Resample(mono, (int)AudioServer.GetMixRate(), V.SampleRate));
                    Flush();
                }
            }
        }
        // Alicilarin kazanci: dinleyenin (yerel oyuncu) uzakligina gore.
        foreach (var (peer, o) in _out)
        {
            if (!GodotObject.IsInstanceValid(o.player)) continue;
            float d = o.player.GlobalPosition.DistanceTo(_net.D.Player.GlobalPosition);
            float g = NetVoice.Gain(d, o.radio, V);
            o.player.VolumeDb = g <= 0.0001f ? -80f : Mathf.LinearToDb(g);
        }
    }

    void Flush()
    {
        int fs = V.FrameSamples;
        while (_pending.Count >= fs)
        {
            var frame = _pending.GetRange(0, fs).ToArray();
            _pending.RemoveRange(0, fs);
            if (!NetVoice.Gate(frame, V)) continue;
            SendFrame(frame, _radio);
        }
    }

    void SendFrame(float[] frame, bool radio)
    {
        _net.SendVoice(NetVoice.Packet(_net.LocalPeer, radio, _seq++, NetVoice.CodecMuLaw, NetVoice.Encode(frame)));
        Sent++;
        _talkT = 0.3f;
    }

    /// <summary>Sinama: n cerceve sinus (mikrofonsuz basliksiz kip).</summary>
    public void InjectTone(int frames, bool radio = false)
    {
        int fs = V.FrameSamples;
        for (int k = 0; k < frames; k++)
        {
            var f = new float[fs];
            for (int i = 0; i < fs; i++) f[i] = 0.4f * Mathf.Sin((k * fs + i) * Mathf.Tau * 440f / V.SampleRate);
            SendFrame(f, radio);
        }
    }

    public void Receive(byte peer, bool radio, ushort seq, byte codec, byte[] data)
    {
        Received[peer] = Received.GetValueOrDefault(peer) + 1;
        if (codec != NetVoice.CodecMuLaw || DisplayServer.GetName() == "headless") return;
        if (!_net.Avatars.TryGetValue(peer, out var av)) return;
        if (!_out.TryGetValue(peer, out var o) || !GodotObject.IsInstanceValid(o.player))
        {
            var p = new AudioStreamPlayer3D
            {
                Stream = new AudioStreamGenerator { MixRate = V.SampleRate, BufferLength = 0.4f },
                AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.Disabled,
                Position = new Vector3(0, 1.5f, 0),
                MaxDistance = 0,
            };
            av.AddChild(p);
            p.Play();
            o = (p, (AudioStreamGeneratorPlayback)p.GetStreamPlayback(), radio);
        }
        o.radio = radio;
        _out[peer] = o;
        var pcm = NetVoice.Decode(data);
        if (o.pb.GetFramesAvailable() < pcm.Length) return; // tampon dolu: cerceve atilir (gecikme birikmesin)
        var stereo = new Vector2[pcm.Length];
        for (int i = 0; i < pcm.Length; i++) stereo[i] = new Vector2(pcm[i], pcm[i]);
        o.pb.PushBuffer(stereo);
    }
}
