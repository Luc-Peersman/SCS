// ============================================================================
//  ShowManager.cs  —  Gemak voor wie met MEERDERE Nodes tegelijk werkt.
//
//  WAT IS DIT:
//   Een klein hulpscript bovenop SerialController. Het houdt bij welke Nodes
//   ONLINE zijn en biedt scene-brede gemakken, zoals "ping alle Nodes" en een
//   lijstje van wie er online is. Voor losse acties op één Node gebruik je
//   ShowNode; ShowManager gaat over het GEHEEL.
//
//  HOE GEBRUIK JE HET (3 stappen):
//   1) Maak één (leeg) GameObject in de scene en sleep dit ShowManager-script erop.
//   2) Niets in te stellen: de SerialController wordt automatisch gevonden
//      (of sleep hem zelf in het veld 'Verbinding').
//   3) Roep PingAlleNodes() aan vanuit een UI-knop, of vraag in code op welke
//      Nodes online zijn met WelkeNodesOnline() / HoeveelNodesOnline().
//
//  Werkt samen met SerialController (V003-protocol).
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

public class ShowManager : MonoBehaviour
{
    [Header("Verbinding (leeg laten = zelf zoeken)")]
    [Tooltip("De SerialController in de scene. Laat dit leeg; dan zoekt ShowManager hem automatisch.")]
    public SerialController verbinding;

    // Intern bijhouden welke adressen online zijn. (Studenten zien dit niet;
    //  gebruik de methodes hieronder.)
    readonly HashSet<byte> onlineNodes = new HashSet<byte>();

    // ══════════════════════ verbinding zoeken / abonneren ══════════════════════
    void OnEnable()
    {
        if (verbinding == null) verbinding = FindFirstObjectByType<SerialController>();
        if (verbinding == null)
        {
            Debug.LogError("[ShowManager] Geen SerialController in de scene gevonden. " +
                           "Zet er één in de scene, of sleep hem in het veld 'Verbinding'.", this);
            return;
        }

        verbinding.NodeOnline  += BijOnline;
        verbinding.NodeOffline += BijOffline;
    }

    void OnDisable()
    {
        if (verbinding == null) return;
        verbinding.NodeOnline  -= BijOnline;
        verbinding.NodeOffline -= BijOffline;
    }

    void BijOnline (byte adr) => onlineNodes.Add(adr);
    void BijOffline(byte adr) => onlineNodes.Remove(adr);

    // ════════════════════════ scene-brede gemakken ════════════════════════

    /// Ping ALLE Nodes (broadcast). Wie leeft, meldt zich → komt in de online-lijst.
    public void PingAlleNodes()
    {
        if (!HeeftVerbinding()) return;
        verbinding.PingAll();
    }

    /// Hoeveel Nodes zijn er nu online?
    public int HoeveelNodesOnline() => onlineNodes.Count;

    /// De adressen van alle Nodes die nu online zijn (oplopend gesorteerd).
    public int[] WelkeNodesOnline()
    {
        var lijst = new List<int>();
        foreach (byte a in onlineNodes) lijst.Add(a);
        lijst.Sort();
        return lijst.ToArray();
    }

    /// Print de online Nodes netjes in de Console (handig om snel te kijken).
    public void PrintOnlineNodes()
    {
        int[] nodes = WelkeNodesOnline();
        if (nodes.Length == 0)
        {
            Debug.Log("[ShowManager] Geen Nodes online. Tip: druk op PingAlleNodes().", this);
            return;
        }
        Debug.Log($"[ShowManager] {nodes.Length} Node(s) online: {string.Join(", ", nodes)}", this);
    }

    // ───────────────────────────── kleine helper ─────────────────────────────
    bool HeeftVerbinding()
    {
        if (verbinding != null) return true;
        Debug.LogError("[ShowManager] Geen SerialController gekoppeld. " +
                       "Zet er één in de scene of vul het veld 'Verbinding' in.", this);
        return false;
    }
}
