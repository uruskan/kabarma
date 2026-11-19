using System.Linq;
using Godot;
using Kabarma.Sim;
using T = Kabarma.Game.UiTheme;

namespace Kabarma.Game;

/// <summary>
/// Harita (GDD §21; dunya2): M ile acilir, Esc ya da M ile kapanir. Rust tarzi duz panel (SANAT-SES §8.1): kuzey (+Z)
/// yukari, dogu (-X) saga. Zemin dokusu dunyadan bir kez (yukseklik golgesi, biyom rengi, gol, patika); ustunde
/// acilmamis alan sisi (Exploration bitleri). Bulunan kesif noktalari, mancinik duraklari, Ocak, oyuncu oku ve
/// oyuncunun isaretleri. Sol tik: isaret koy; sag tik: en yakin isareti kaldir.
/// </summary>
public partial class MapPanel : Control
{
    readonly Director _d;
    ImageTexture _base, _fog;
    Image _fogImg;
    int _fogVersion = -1;
    Input.MouseModeEnum _prevMouse;
    bool _prevLook;

    public bool Open => Visible;

    public MapPanel(Director d)
    {
        _d = d;
        Name = "MapPanel";
        Visible = false;
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsPreset(LayoutPreset.FullRect);
    }

    World W => _d.World;
    Exploration X => _d.State.Explore;
    float S => W.Size;

    public override void _Process(double delta)
    {
        if (Input.IsActionJustPressed("map") && (Visible || !_d.Hud.MenuOpen)) Toggle();
        else if (Visible && Input.IsActionJustPressed("ui_cancel")) Toggle();
        if (Visible) QueueRedraw();
    }

    public void Toggle()
    {
        Visible = !Visible;
        if (Visible)
        {
            _base ??= BuildBase();
            _prevMouse = Input.MouseMode; _prevLook = _d.Player.LookLocked;
            if (_d.Bot == null) Input.MouseMode = Input.MouseModeEnum.Visible;
            _d.Player.LookLocked = true;
            GD.Print($"MAP open percent={X.Percent:0.0} found={X.Found.Count}");
        }
        else
        {
            if (_d.Bot == null) Input.MouseMode = _prevMouse;
            _d.Player.LookLocked = _prevLook;
        }
    }

    // ---------- Dunya <-> harita ----------

    Rect2 MapRect()
    {
        var vp = GetViewportRect().Size;
        float side = Mathf.Min(vp.Y * 0.82f, vp.X * 0.62f);
        return new Rect2((vp.X - side) / 2f, (vp.Y - side) / 2f + T.Pxf(10), side, side);
    }

    /// <summary>Kuzey yukari, dogu saga: ekran x = -dunya X, ekran y = -dunya Z.</summary>
    Vector2 ToMap(Rect2 r, float wx, float wz) =>
        r.Position + new Vector2((W.Center.X + S / 2f - wx) / S * r.Size.X, (W.Center.Y + S / 2f - wz) / S * r.Size.Y);

    Vector2 ToWorld(Rect2 r, Vector2 m)
    {
        var u = (m - r.Position) / r.Size;
        return new Vector2(W.Center.X + S / 2f - u.X * S, W.Center.Y + S / 2f - u.Y * S);
    }

    // ---------- Dokular ----------

    ImageTexture BuildBase()
    {
        const int res = 512;
        var img = Image.CreateEmpty(res, res, false, Image.Format.Rgb8);
        Color P(BiomeDef b, string k, Color f) => b.Palette.TryGetValue(k, out var h) ? new Color(h) : f;
        // Her biyomun harita tonu: baslangic biyomu (Cayir) zemin+bitki, digerleri bitki+zemin; agirlikla karisir (M4: altı biyom).
        var order = W.Map.Order;
        var tones = order.Select(id => { var b = W.Biomes.Biomes[id]; return id == W.Biomes.World.Biome ? P(b, "ground", Palette.Ground).Lerp(P(b, "plant", Palette.Plant), 0.45f) : P(b, "plant", Palette.Plant).Lerp(P(b, "ground", Palette.Ground), 0.5f); }).ToArray();
        var wbuf = new float[order.Count];
        var light = new Vector3(0.5f, 0.75f, -0.45f).Normalized();
        for (int j = 0; j < res; j++)
        for (int i = 0; i < res; i++)
        {
            float wx = W.Center.X + S / 2f - (i + 0.5f) / res * S, wz = W.Center.Y + S / 2f - (j + 0.5f) / res * S;
            float h = W.HeightAt(wx, wz);
            W.Map.Weights(wx, wz, wbuf);
            var c = new Color(0, 0, 0);
            for (int bi = 0; bi < tones.Length; bi++) c += tones[bi] * wbuf[bi];
            c.A = 1f;
            // Yukseklik golgesi.
            float dx = W.HeightAt(wx - 1f, wz) - W.HeightAt(wx + 1f, wz), dz = W.HeightAt(wx, wz - 1f) - W.HeightAt(wx, wz + 1f);
            var n = new Vector3(dx * 0.5f, 1f, dz * 0.5f).Normalized();
            float shade = Mathf.Clamp(n.Dot(light) * 1.25f, 0.45f, 1.15f);
            c = c * shade;
            c = c.Lerp(Colors.White, Mathf.Clamp(h / 60f, 0f, 0.25f));
            if (W.IsWater(new Vector3(wx, h, wz))) c = new Color("2E5580").Lerp(new Color("6FBFB5"), 0.3f);
            img.SetPixel(i, j, c);
        }
        return ImageTexture.CreateFromImage(img);
    }

    void UpdateFog()
    {
        if (_fogVersion == (_d.Explorer?.Version ?? 0) && _fog != null) return;
        _fogVersion = _d.Explorer?.Version ?? 0;
        int n = X.N;
        _fogImg ??= Image.CreateEmpty(n, n, false, Image.Format.La8);
        for (int cj = 0; cj < n; cj++)
        for (int ci = 0; ci < n; ci++)
            _fogImg.SetPixel(n - 1 - ci, n - 1 - cj, new Color(1, 1, 1, X.IsRevealed(ci, cj) ? 0f : 1f));
        if (_fog == null) _fog = ImageTexture.CreateFromImage(_fogImg); else _fog.Update(_fogImg);
    }

    // ---------- Cizim ----------

    public override void _Draw()
    {
        if (_base == null) return;
        UpdateFog();
        var vp = GetViewportRect().Size;
        DrawRect(new Rect2(Vector2.Zero, vp), new Color(0, 0, 0, 0.55f));
        var r = MapRect();
        float pad = T.Pxf(14);
        var frame = r.Grow(pad);
        frame.Position -= new Vector2(0, T.Pxf(34));
        frame.Size += new Vector2(0, T.Pxf(34) + T.Pxf(30));
        DrawRect(frame, T.A(T.Dialog, 0.94f));
        DrawRect(new Rect2(frame.Position, new Vector2(frame.Size.X, T.Pxf(3))), T.Teal);
        var title = T.Font(22, true, true);
        DrawString(title, frame.Position + new Vector2(pad, T.Pxf(30)), Loc.T("menu.map_title"), HorizontalAlignment.Left, -1, T.Px(22), T.Ink);
        var small = T.Font(14, false, true);
        DrawString(small, frame.Position + new Vector2(frame.Size.X - pad, T.Pxf(28)), Loc.T("menu.map_stats", X.Percent.ToString("0", System.Globalization.CultureInfo.InvariantCulture), X.Found.Count, W.Pois.Count), HorizontalAlignment.Right, -1, T.Px(14), T.A(T.Ink, 0.8f));

        DrawTextureRect(_base, r, false);
        // Sis: acilmamis alan (yumusak kenar icin dogrusal suzgec).
        var fogCol = new Color(_d.Data.Biomes.World.Map.Fog);
        DrawTextureRect(_fog, r, false, fogCol);
        // 64 m kafes.
        float grid = S > 1000f ? 200f : 64f;
        for (float g = 0f; g <= S + 0.1f; g += grid)
        {
            float u = g / S;
            DrawLine(new Vector2(r.Position.X + u * r.Size.X, r.Position.Y), new Vector2(r.Position.X + u * r.Size.X, r.End.Y), new Color(1, 1, 1, 0.05f), 1f);
            DrawLine(new Vector2(r.Position.X, r.Position.Y + u * r.Size.Y), new Vector2(r.End.X, r.Position.Y + u * r.Size.Y), new Color(1, 1, 1, 0.05f), 1f);
        }
        DrawRect(r, T.A(T.Ink, 0.35f), false, 1.5f);
        DrawString(small, new Vector2(r.Position.X + r.Size.X / 2f - T.Pxf(4), r.Position.Y - T.Pxf(4)), Loc.T("menu.map_north"), HorizontalAlignment.Left, -1, T.Px(14), T.Teal);

        // Kesif noktalari (bulunanlar).
        foreach (var poi in W.Pois.Where(p => X.Found.Contains(p.Id)))
        {
            var type = W.PoiType(poi.Type);
            var m = ToMap(r, poi.X, poi.Z);
            var col = string.IsNullOrEmpty(type.Color) ? T.Ink : new Color(type.Color);
            bool done = poi.Type == "seyir_kulesi" ? X.Climbed.Contains(poi.Id) : X.Uses.TryGetValue(poi.Id, out var u) && u.Left <= 0;
            DrawCircle(m, T.Pxf(7), new Color(0, 0, 0, 0.6f));
            DrawCircle(m, T.Pxf(5.5f), done ? T.A(col, 0.45f) : col);
            if (poi.Type == "seyir_kulesi") DrawRect(new Rect2(m - new Vector2(T.Pxf(2), T.Pxf(9)), new Vector2(T.Pxf(4), T.Pxf(6))), col);
            DrawString(small, m + new Vector2(T.Pxf(9), T.Pxf(5)), type.Name, HorizontalAlignment.Left, -1, T.Px(13), T.A(T.Ink, 0.9f));
        }
        // Mancinik duraklari.
        if (_d.Explorer != null)
            foreach (var s in _d.Explorer.Stops().Where(s => s.Open && s.Id.StartsWith("mancinik")))
            {
                var m = ToMap(r, s.X, s.Z);
                DrawRect(new Rect2(m - Vector2.One * T.Pxf(4), Vector2.One * T.Pxf(8)), T.Warning);
            }
        // Ocak.
        var o = _d.OcakPos;
        var om = ToMap(r, o.X, o.Z);
        DrawCircle(om, T.Pxf(7), new Color(0, 0, 0, 0.6f));
        DrawCircle(om, T.Pxf(5), Palette.Accent);
        DrawString(small, om + new Vector2(T.Pxf(9), T.Pxf(5)), "Ocak", HorizontalAlignment.Left, -1, T.Px(13), T.Ink);
        // Isaretler.
        foreach (var mk in X.Markers)
        {
            var m = ToMap(r, mk.X, mk.Z);
            DrawLine(m, m - new Vector2(0, T.Pxf(14)), T.Ink, 2f);
            DrawRect(new Rect2(m - new Vector2(0, T.Pxf(14)), new Vector2(T.Pxf(10), T.Pxf(7))), T.Red);
            DrawString(small, m + new Vector2(T.Pxf(6), T.Pxf(-2)), mk.Label, HorizontalAlignment.Left, -1, T.Px(13), T.Ink);
        }
        // Oyuncu oku: bakis yonu.
        var p = _d.Player.GlobalPosition;
        var pm = ToMap(r, p.X, p.Z);
        var fwd = -_d.Player.Yaw.GlobalBasis.Z;
        var dir = new Vector2(-fwd.X, -fwd.Z).Normalized();
        var side = new Vector2(-dir.Y, dir.X);
        float sz = T.Pxf(10);
        var tri = new[] { pm + dir * sz, pm - dir * sz * 0.6f + side * sz * 0.6f, pm - dir * sz * 0.6f - side * sz * 0.6f };
        DrawColoredPolygon(tri, Colors.White);
        DrawPolyline(new[] { tri[0], tri[1], tri[2], tri[0] }, new Color(0, 0, 0, 0.8f), 1.5f);

        // Alt satir: tuslar.
        DrawString(small, new Vector2(frame.Position.X + pad, frame.End.Y - T.Pxf(10)), Loc.T("menu.map_keys"), HorizontalAlignment.Left, -1, T.Px(13), T.A(T.Ink, 0.7f));
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is not InputEventMouseButton mb || !mb.Pressed) return;
        var r = MapRect();
        if (!r.HasPoint(mb.Position)) return;
        var w = ToWorld(r, mb.Position);
        if (mb.ButtonIndex == MouseButton.Left)
        {
            var m = X.AddMarker(w.X, w.Y, Loc.T("menu.map_marker", X.NextMarker - 1));
            GD.Print($"MAP marker add {m.Id} at={w.X:0},{w.Y:0}");
        }
        else if (mb.ButtonIndex == MouseButton.Right && X.Markers.Count > 0)
        {
            var near = X.Markers.OrderBy(m => new Vector2(m.X - w.X, m.Z - w.Y).Length()).First();
            if (new Vector2(near.X - w.X, near.Z - w.Y).Length() < S * 0.04f) X.RemoveMarker(near.Id);
        }
        AcceptEvent();
        QueueRedraw();
    }
}
