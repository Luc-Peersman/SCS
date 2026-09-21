// ============================================================================
//  ShowNode.cs  —  Koppel één Unity-GameObject aan één ESP-NOW Node.
//
//  WAT IS DIT:
//   Een eenvoudige laag BOVENOP SerialController. Zet dit script op een
//   GameObject en geef het het adres van jouw Node. Daarna kun je:
//    • ZONDER code reageren op wat de Node doet (knop / IR / pin / online) door
//      reacties in de Events hieronder te slepen (drag-and-drop in de Inspector);
//    • de Node aansturen (servo / socket / pin / ident) met simpele methodes die
//      je aan een UI-knop (OnClick) hangt of vanuit een kort scriptje aanroept.
//
//  HOE GEBRUIK JE HET (3 stappen):
//   1) Maak een (leeg) GameObject en sleep dit ShowNode-script erop.
//   2) Vul bij "Node Adres" het nummer van jouw Node in (1 t/m 63 — dat is het
//      DIP-adres dat ook op de Node staat ingesteld).
//   3) Reageren? Sleep onder een Event (bv. "On Button Pressed") een object +
//      functie. Aansturen? Hang ZetServo / SocketAan / SocketUit / ZetPin /
//      StuurIdent aan een UI-knop (OnClick) of roep ze aan vanuit code.
//
//  De SerialController in de scene wordt automatisch gevonden (of vul hem zelf
//  in bij "Verbinding"). Werkt samen met SerialController (V003-protocol).
// ============================================================================
using UnityEngine;
using UnityEngine.Events;

public class ShowNode : MonoBehaviour
{
    // ── Eigen Event-typen zodat ze met parameters in de Inspector verschijnen ──
    //   (jij hoeft hier niets mee te doen; ze zorgen alleen dat je het knopnummer,
    //    de IR-code of de pin-waarde kunt meekrijgen als je dat wilt.)
    [System.Serializable] public class KnopEvent : UnityEvent<int>      { }  // knopnummer
    [System.Serializable] public class IrEvent   : UnityEvent<string>  { }  // IR-code als tekst, bv "0x00FF6897"
    [System.Serializable] public class PinEvent  : UnityEvent<int, int> { } // pin, waarde (0/1)

    [Header("Welke Node hoort bij dit object?")]
    [Tooltip("Het adres van jouw Node: 1 t/m 63. Hetzelfde nummer als het DIP-adres op de Node.")]
    [Range(1, 63)]
    public int nodeAdres = 1;

    [Header("Verbinding (leeg laten = zelf zoeken)")]
    [Tooltip("De SerialController in de scene. Laat dit leeg; dan zoekt ShowNode hem automatisch.")]
    public SerialController verbinding;

    [Header("Reacties op DEZE Node — sleep hier je acties in")]
    [Tooltip("Vuurt als er op de Node een knop wordt ingedrukt. Geeft het knopnummer mee.")]
    public KnopEvent OnButtonPressed;
    [Tooltip("Vuurt als de Node een IR-code ontvangt. Geeft de code als tekst mee, bv '0x00FF6897'.")]
    public IrEvent OnIrReceived;
    [Tooltip("Vuurt als een ingang (pin) van de Node verandert. Geeft pinnummer en waarde (0/1) mee.")]
    public PinEvent OnPinChanged;
    [Tooltip("Vuurt zodra de Node online komt (zich meldt).")]
    public UnityEvent OnNodeOnline;
    [Tooltip("Vuurt zodra de Node offline gaat (meer dan ~10 s stil).")]
    public UnityEvent OnNodeOffline;

    // ══════════════════════ verbinding zoeken / abonneren ══════════════════════

    private void Start()
    {
        StuurIdent();
        ZetServo(45);

    }
    void OnEnable()
    {
        // 1) verbinding bepalen: zelf gekozen in de Inspector, anders automatisch zoeken.
        if (verbinding == null) verbinding = FindFirstObjectByType<SerialController>();
        if (verbinding == null)
        {
            Debug.LogError($"[ShowNode] Geen SerialController in de scene gevonden voor Node {nodeAdres}. " +
                           "Zet een SerialController in de scene, of sleep hem in het veld 'Verbinding'.", this);
            return;
        }

        // 2) abonneren op de inkomende gebeurtenissen (we filteren straks op ons eigen adres).
        verbinding.ButtonPressed += BijKnop;
        verbinding.IrReceived    += BijIr;
        verbinding.PinChanged    += BijPin;
        verbinding.NodeOnline    += BijOnline;
        verbinding.NodeOffline   += BijOffline;
    }

    void OnDisable()
    {
        // afmelden — anders krijg je na een script-reload dubbele/geesten-callbacks.
        if (verbinding == null) return;
        verbinding.ButtonPressed -= BijKnop;
        verbinding.IrReceived    -= BijIr;
        verbinding.PinChanged    -= BijPin;
        verbinding.NodeOnline    -= BijOnline;
        verbinding.NodeOffline   -= BijOffline;
    }

    // ── handlers: vuren op de main-thread (Unity-veilig) en alleen voor ONZE Node ──
    void BijKnop   (byte adr, int knop)              { if (adr != Adres()) return; OnButtonPressed?.Invoke(knop); }
    void BijIr     (byte adr, string proto, uint code){ if (adr != Adres()) return; OnIrReceived?.Invoke($"0x{code:X8}"); }
    void BijPin    (byte adr, int pin, int waarde)   { if (adr != Adres()) return; OnPinChanged?.Invoke(pin, waarde); }
    void BijOnline (byte adr)                        { if (adr != Adres()) return; OnNodeOnline?.Invoke(); }
    void BijOffline(byte adr)                        { if (adr != Adres()) return; OnNodeOffline?.Invoke(); }

    // ════════════════ UITGAANDE acties — hang deze aan een knop/script ════════════════

    /// Zet de servo op een hoek tussen 0 en 180 graden.
    public void ZetServo(int hoek)
    {
        if (!HeeftVerbinding()) return;
        if (hoek < 0 || hoek > 180)
        {
            Debug.LogWarning($"[ShowNode] Servohoek {hoek} valt buiten 0..180 — ik kort hem af.", this);
            hoek = Mathf.Clamp(hoek, 0, 180);
        }
        verbinding.SendServo(Adres(), hoek);
    }

    /// Zet de stekkerdoos (socket) AAN.
    public void SocketAan()
    {
        if (!HeeftVerbinding()) return;
        verbinding.SendSocket(Adres(), true);
    }

    /// Zet de stekkerdoos (socket) UIT.
    public void SocketUit()
    {
        if (!HeeftVerbinding()) return;
        verbinding.SendSocket(Adres(), false);
    }

    /// Zet een uitgang (pin 0..4) hoog (aan = true) of laag (aan = false).
    public void ZetPin(int pin, bool aan)
    {
        if (!HeeftVerbinding()) return;
        if (pin < 0 || pin > 4)
        {
            Debug.LogWarning($"[ShowNode] Pin {pin} bestaat niet (gebruik 0 t/m 4) — niets verstuurd.", this);
            return;
        }
        verbinding.SendPin(Adres(), pin, aan);
    }

    /// Laat de Node zich identificeren (LEDs knipperen even).
    public void StuurIdent()
    {
        if (!HeeftVerbinding()) return;
        verbinding.Ident(Adres());
    }

    // ───────────────────────────── kleine helpers ─────────────────────────────

    // Het node-adres als byte. We houden het netjes binnen 1..63.
    byte Adres() => (byte)Mathf.Clamp(nodeAdres, 1, 63);

    // Controleer of er een verbinding is; zo niet, leg duidelijk uit wat er mis is.
    bool HeeftVerbinding()
    {
        if (verbinding != null) return true;
        Debug.LogError($"[ShowNode] Geen SerialController gekoppeld voor Node {nodeAdres}. " +
                       "Zet er één in de scene of vul het veld 'Verbinding' in.", this);
        return false;
    }
}
