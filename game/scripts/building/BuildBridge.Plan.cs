using Godot;

namespace Kabarma.Game;

/// <summary>
/// Insa Plani'nin E basili carki (GDD 7 "Yerinde yukseltme"; URETIM 6 M1 karari 23): plan eldeyken parcaya ya da
/// yerlestirilebilire bakip E basili tutulunca yukselt / onar / sok. Var olan Sok ve Onar mantigi (BuildBridge.Impl.cs)
/// aynen kullanilir; M1'de tek malzeme Ahsap oldugu icin yukseltme yok (M2'de Kerpic-Tas).
/// </summary>
public partial class BuildBridge
{
    /// <summary>Hedefin carktaki satirlari: (baslik, aciklama, yapilabilir mi). Sira: Yukselt, Onar, Sok.</summary>
    public (string title, string text, bool ok)[] PlanActions(Node3D t)
    {
        string onar = OnarPreview(t, out bool okOnar);
        string sok = SokPreview(t, out bool okSok);
        return new[]
        {
            ("Yükselt", "M1'de tek malzeme Ahşap: Kerpiç-Taş M2'de.", false),
            ("Onar", onar, okOnar),
            ("Sök", sok, okSok),
        };
    }

    /// <summary>Carktan secilen eylem (0 yukselt, 1 onar, 2 sok). Islediyse true.</summary>
    public bool RunPlanAction(Node3D t, int action)
    {
        if (t == null || !IsInstanceValid(t)) return false;
        bool done = action switch { 1 => Repair(t), 2 => Deconstruct(t), _ => false };
        if (!done) Sfx.Play("hit", null, -10f);
        Highlight(null, false);
        return done;
    }

    /// <summary>Carktayken hedefi vurgular (sok: kirmizi, onar: yesil).</summary>
    public void MarkPlanTarget(Node3D t, bool sok) => Highlight(t, sok);
}
