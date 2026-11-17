using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Dunya: M1 yer imleri (SANAT-SES §9): sabit kamera konumu ve bakis yonu, oyun kamerasi yuksekliginde.
/// Anchor "ocak" olan yer imi (us) Ocak'in gercek konumuna gore kurulur: oyuncunun Ocak'i (0,0,0)'a tam oturmaz.
/// </summary>
public partial class World
{
    public BookmarkDef Bookmark(string id) => Biomes.World.Bookmarks.First(b => b.Id == id);

    /// <summary>Yer iminin kamera donusumu: goz zeminden EyeHeight yukarida, hedef zeminden y kadar yukarida.</summary>
    public Transform3D BookmarkTransform(BookmarkDef b, Vector3 anchor = default)
    {
        var a = b.Anchor == "ocak" ? new Vector3(anchor.X, 0, anchor.Z) : Vector3.Zero;
        var eye = Ground(a + new Vector3(b.Eye[0], 0, b.Eye[1])) + Vector3.Up * b.EyeHeight;
        var t = Ground(a + new Vector3(b.Target[0], 0, b.Target[2])) + Vector3.Up * b.Target[1];
        return new Transform3D(Basis.LookingAt(t - eye, Vector3.Up), eye);
    }

    /// <summary>Yer imi kamerasini kurar ya da tasir ve etkin yapar.</summary>
    public Camera3D ShowBookmark(BookmarkDef b, Camera3D cam = null, Vector3 anchor = default)
    {
        if (cam == null) { cam = new Camera3D { Name = "BookmarkCam" }; AddChild(cam); }
        cam.Fov = b.Fov;
        cam.GlobalTransform = BookmarkTransform(b, anchor);
        cam.MakeCurrent();
        return cam;
    }
}
