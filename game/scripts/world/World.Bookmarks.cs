using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>Dunya: M1 yer imleri (SANAT-SES §9): sabit kamera konumu ve bakis yonu, oyun kamerasi yuksekliginde.</summary>
public partial class World
{
    public BookmarkDef Bookmark(string id) => Biomes.World.Bookmarks.First(b => b.Id == id);

    /// <summary>Yer iminin kamera donusumu: goz zeminden EyeHeight yukarida, hedef zeminden y kadar yukarida.</summary>
    public Transform3D BookmarkTransform(BookmarkDef b)
    {
        var eye = Ground(new Vector3(b.Eye[0], 0, b.Eye[1])) + Vector3.Up * b.EyeHeight;
        var t = Ground(new Vector3(b.Target[0], 0, b.Target[2])) + Vector3.Up * b.Target[1];
        return new Transform3D(Basis.LookingAt(t - eye, Vector3.Up), eye);
    }

    /// <summary>Yer imi kamerasini kurar ya da tasir ve etkin yapar.</summary>
    public Camera3D ShowBookmark(BookmarkDef b, Camera3D cam = null)
    {
        if (cam == null) { cam = new Camera3D { Name = "BookmarkCam" }; AddChild(cam); }
        cam.Fov = b.Fov;
        cam.GlobalTransform = BookmarkTransform(b);
        cam.MakeCurrent();
        return cam;
    }
}
