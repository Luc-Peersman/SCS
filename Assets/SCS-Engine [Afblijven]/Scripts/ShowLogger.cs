// ============================================================================
//  ShowLogger.cs  —  Zie alles wat de Nodes/Nexus terugsturen in de Console.
//
//  WAT IS DIT:
//   Een meeluister-scriptje bovenop SerialController. Het abonneert zich op
//   ALLE inkomende gebeurtenissen en schrijft ze als nette regels in de Unity-
//   Console (Window ▸ General ▸ Console). Handig om mee te kijken: je ziet o.a.
//   HELLO (een Node leeft), ONLINE/OFFLINE, ACK/NOACK, knoppen, pinnen, IR en
//   STAT voorbijkomen — zonder zelf code te schrijven.
//
//  HOE GEBRUIK JE HET (3 stappen):
//   1) Maak één (leeg) GameObject in de scene en sleep dit ShowLogger-script erop.
//   2) Niets in te stellen: de SerialController wordt automatisch gevonden
//      (of sleep hem zelf in het veld 'Verbinding').
//   3) Druk op Play en open de Console. Met de vinkjes hieronder kies je wat je
//      wel/niet wilt zien. Wil je maar één Node volgen? Vul 'Alleen Node' in.
//
//  Tip: problemen (NOACK / OFFLINE) komen als gele waarschuwing binnen, zodat ze
//  opvallen. Werkt samen met SerialController (V003-protocol).
// ============================================================================
using UnityEngine;

public class ShowLogger : MonoBehaviour
{
    [Header("Verbinding (leeg laten = zelf zoeken)")]
    [Tooltip("De SerialController in de scene. Laat dit leeg; dan zoekt ShowLogger hem automatisch.")]
    public SerialController verbinding;

    [Header("Welke Node loggen?  (0 = alle Nodes)")]
    [Tooltip("0 = alles tonen. Of vul 1 t/m 63 in om maar één Node te volgen.")]
    [Range(0, 63)]
    public int alleenNode = 0;

    [Header("Wat wil je in de Console zien?")]
    public bool toonHello         = true;   // "een Node leeft" (boot/wake/PING-antwoord)
    public bool toonOnlineOffline = true;   // Node komt online / valt weg
    public bool toonAck           = true;   // commando is aangekomen
    public bool toonNoAck         = true;   // commando NIET aangekomen (waarschuwing)
    public bool toonKnop          = true;   // knop op de Node ingedrukt
    public bool toonPin           = true;   // ingang (pin) veranderd
    public bool toonIr            = true;   // IR-code ontvangen
    public bool toonStat          = true;   // status opgevraagd
    public bool toonNexusDebug    = false;  // ruwe "[NEXUS] ..."-regels (technisch)

    // ══════════════════════ verbinding zoeken / abonneren ══════════════════════
    void OnEnable()
    {
        if (verbinding == null) verbinding = FindFirstObjectByType<SerialController>();
        if (verbinding == null)
        {
            Debug.LogError("[ShowLogger] Geen SerialController in de scene gevonden. " +
                           "Zet er één in de scene, of sleep hem in het veld 'Verbinding'.", this);
            return;
        }

        verbinding.Hello         += BijHello;
        verbinding.NodeOnline    += BijOnline;
        verbinding.NodeOffline   += BijOffline;
        verbinding.Ack           += BijAck;
        verbinding.NoAck         += BijNoAck;
        verbinding.ButtonPressed += BijKnop;
        verbinding.PinChanged    += BijPin;
        verbinding.IrReceived    += BijIr;
        verbinding.Status        += BijStat;
        verbinding.DebugLine     += BijNexusDebug;
    }

    void OnDisable()
    {
        if (verbinding == null) return;
        verbinding.Hello         -= BijHello;
        verbinding.NodeOnline    -= BijOnline;
        verbinding.NodeOffline   -= BijOffline;
        verbinding.Ack           -= BijAck;
        verbinding.NoAck         -= BijNoAck;
        verbinding.ButtonPressed -= BijKnop;
        verbinding.PinChanged    -= BijPin;
        verbinding.IrReceived    -= BijIr;
        verbinding.Status        -= BijStat;
        verbinding.DebugLine     -= BijNexusDebug;
    }

    // ── handlers: vuren op de main-thread (Unity-veilig) ──
    void BijHello  (byte a, int v)            { if (Negeer(a) || !toonHello) return; Debug.Log($"[Show] HELLO    0x{a:X2}  v{v}   (Node leeft)", this); }
    void BijOnline (byte a)                   { if (Negeer(a) || !toonOnlineOffline) return; Debug.Log($"[Show] ● ONLINE  0x{a:X2}", this); }
    void BijOffline(byte a)                   { if (Negeer(a) || !toonOnlineOffline) return; Debug.LogWarning($"[Show] ○ OFFLINE 0x{a:X2}   (Node reageert niet meer)", this); }
    void BijAck    (byte a, string c, int v)  { if (Negeer(a) || !toonAck) return; Debug.Log($"[Show] ACK      0x{a:X2}  {c} = {v}   (aangekomen ✓)", this); }
    void BijNoAck  (byte a, string c)         { if (Negeer(a) || !toonNoAck) return; Debug.LogWarning($"[Show] NOACK    0x{a:X2}  {c}   (NIET aangekomen ✗)", this); }
    void BijKnop   (byte a, int knop)         { if (Negeer(a) || !toonKnop) return; Debug.Log($"[Show] KNOP     0x{a:X2}  knop {knop}", this); }
    void BijPin    (byte a, int pin, int w)   { if (Negeer(a) || !toonPin) return; Debug.Log($"[Show] PIN      0x{a:X2}  pin{pin} = {w}", this); }
    void BijIr     (byte a, string p, uint c) { if (Negeer(a) || !toonIr) return; Debug.Log($"[Show] IR       0x{a:X2}  {p} 0x{c:X8}", this); }
    void BijStat   (byte a, SerialController.StatInfo s)
    {
        if (Negeer(a) || !toonStat) return;
        Debug.Log($"[Show] STAT     0x{a:X2}  SRV={s.servo}  SOCKET={s.socket}  PINS=0x{s.pins:X2}  DROPS={s.drops}", this);
    }
    void BijNexusDebug(string regel) { if (!toonNexusDebug) return; Debug.Log($"[Show] {regel}", this); }

    // Negeer dit adres? (true = niet loggen). Bij 'alleenNode = 0' tonen we alles.
    bool Negeer(byte a) => alleenNode != 0 && a != (byte)alleenNode;
}
