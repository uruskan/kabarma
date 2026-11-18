using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Kayit kopru: kayit sozlesmesinin (TEKNIK 9.1, Director.Save) dunya kapsamli Sim bolumleri ortak oyunda da bolum olur
/// ("save:kimlik"). Host degisince saniyede bir yollar; katilan her istemci (sonradan katilan dahil) hepsini bastan alir
/// ve kayit yukler gibi sessizce uygular (TEKNIK 5.4: Sim durumu, arazi ve yapi farklari). Boylece yeni bir Sim sistemi
/// kayit bolumunu yazinca ortak oyuna da kendiliginden girer.
/// Disarida kalanlar: kisisel bolumler (canta, Capa: GDD 22.1), Spawn sirasindakiler (dunyadaki dugumleri NetSync.World
/// kurar) ve net.json "saveBridgeSkip" (set'i canli oyunda nesne sifirlayanlar: "night" Night'i null yapar).
/// Bu bolumler istemcide de islemeye devam eder (ayni kural), bu yuzden ozet karsilastirmasina girmez (oynak).
/// </summary>
public partial class NetSync
{
    readonly HashSet<string> _volatile = new();
    /// <summary>Istemci: her bolumun kac kez uygulandigi (sinama).</summary>
    public readonly Dictionary<string, int> Applied = new();
    double _saveAcc;
    bool _saveTick;
    public int BridgedSections => _volatile.Count;

    sealed class SaveBridge : INetSection
    {
        readonly ISaveSection _s;
        readonly NetSync _n;
        public SaveBridge(ISaveSection s, NetSync n) { _s = s; _n = n; }
        public string Id => "save:" + _s.Id;
        public bool PerPeer => false;
        public void Write(NetWriter w, int peer) => w.Blob(_s.Write());
        public void Read(NetReader r)
        {
            _s.Read(r.Blob());
            _s.AfterRestore();
            _n.Applied[Id] = _n.Applied.GetValueOrDefault(Id) + 1;
        }
    }

    void RegisterSaveBridge()
    {
        var skip = new HashSet<string>(Cfg.SaveBridgeSkip);
        foreach (var s in D.Save.Sections
                     .Where(s => s.Scope == SaveScope.World && s.Order >= SaveOrder.State && !skip.Contains(s.Id))
                     .OrderBy(s => s.Order).ThenBy(s => s.Id, System.StringComparer.Ordinal))
        {
            var b = new SaveBridge(s, this);
            Register(b);
            _volatile.Add(b.Id);
        }
        GD.Print($"NET save bridge: {string.Join(",", _volatile)}");
    }

    /// <summary>Oynak bolum (istemcide de isler): ozet karsilastirmasina girmez, saniyede bir gonderilir.</summary>
    public bool IsVolatile(string id) => _volatile.Contains(id);

    void TickSaveBridge(double dt)
    {
        _saveAcc += dt;
        _saveTick = _saveAcc >= 1.0;
        if (_saveTick) _saveAcc = 0;
    }
}
