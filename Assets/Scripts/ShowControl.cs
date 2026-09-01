// ============================================================================
//  ShowControl.cs  —  Eén tijdlijn voor je hele show, over ALLE Nodes heen.
//
//  WAT IS DIT:
//   Zet dit script op één (leeg) GameObject in de scene. Daarna kun je in
//   Start() (of vanuit eender welk script) een hele reeks acties inplannen,
//   elk met een eigen tijdstip en een eigen doel-Node:
//
//      Event.ZetServo(2, 3, 45);   // na 2 s: Node 3, servo naar 45°
//      Event.ZetServo(5, 1, 80);   // na 5 s: Node 1, servo naar 80°
//      Event.SocketAan(3, 2);      // na 3 s: Node 2, stekkerdoos AAN
//
//   Elke regel plant zijn eigen actie in — de tijden zijn onafhankelijk van
//   elkaar (dus GEEN "wacht op de vorige stap"), gewoon "op tijdstip X vanaf
//   het moment dat Start() draait".
//
//  HOE GEBRUIK JE HET:
//   1) Maak één (leeg) GameObject en sleep dit ShowControl-script erop.
//      (er mag er maar ÉÉN in de scene staan — via 'Event' kun je hem overal
//      aanspreken zonder een referentie te moeten slepen)
//   2) De SerialController wordt automatisch gevonden (of sleep hem zelf in
//      het veld 'Verbinding').
//   3) Typ je tijdlijn in Start() als 'Event.<methode>(...)', of roep de
//      methodes vanuit een eigen script aan.
//
//  Werkt samen met SerialController (V003-protocol).
// ============================================================================
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class ShowControl : MonoBehaviour
{
    /// Wereldwijde toegang: vanuit eender welk script/methode gebruik je "Event.ZetServo(...)".
    public static ShowControl Event { get; private set; }

    [Header("Verbinding (leeg laten = zelf zoeken)")]
    [Tooltip("De SerialController in de scene. Laat dit leeg; dan zoekt ShowControl hem automatisch.")]
    public SerialController verbinding;

    [System.Serializable]
    public class Geluid
    {
        [Tooltip("De naam die je gebruikt in Event.Speelgeluid(tijd, naam).")]
        public string naam;
        public AudioClip clip;
    }

    [Header("Geluiden (geef elk geluid een naam en sleep er een AudioClip op)")]
    public List<Geluid> geluiden = new List<Geluid>();

    [System.Serializable]
    public class NodeAfbeelding
    {
        [Tooltip("Vaste naam — niet aanpassen. Hoort bij het Node-nummer.")]
        public string naam;
        [Tooltip("Sleep hier de afbeelding (GameObject) uit de Scene die deze Node simuleert.")]
        public GameObject afbeelding;
    }

    [Header("Node-afbeeldingen (sleep per Node de bijbehorende afbeelding uit de Scene)")]
    public List<NodeAfbeelding> nodeAfbeeldingen = new List<NodeAfbeelding>
    {
        new NodeAfbeelding { naam = "Node1" },
        new NodeAfbeelding { naam = "Node2" },
        new NodeAfbeelding { naam = "Node3" },
        new NodeAfbeelding { naam = "Node4" },
        new NodeAfbeelding { naam = "Node5" },
        new NodeAfbeelding { naam = "Node6" },
        new NodeAfbeelding { naam = "Node7" },
        new NodeAfbeelding { naam = "Node8" },
    };

    AudioSource audioBron;

    void Start(){
        ShowList();
    }


    void Awake()
    {
        if (Event != null && Event != this)
        {
            Debug.LogWarning("[ShowControl] Er staat al een ShowControl in de scene — deze extra wordt genegeerd.", this);
            Destroy(this);
            return;
        }
        Event = this;
        audioBron = GetComponent<AudioSource>();

        foreach (NodeAfbeelding n in nodeAfbeeldingen)
            if (n.afbeelding != null) n.afbeelding.SetActive(false);
    }

    void OnEnable()
    {
        if (verbinding == null) verbinding = FindFirstObjectByType<SerialController>();
        if (verbinding == null)
            Debug.LogError("[ShowControl] Geen SerialController in de scene gevonden. " +
                           "Zet er één in de scene, of sleep hem in het veld 'Verbinding'.", this);
    }

    void OnDestroy()
    {
        if (Event == this) Event = null;
    }

    // ════════════════ Tijdlijn-acties — plan hier je show mee in ════════════════

    /// Zet na 'tijd' seconden, op 'node', de servo op een hoek tussen 0 en 180 graden.
    public void ZetServo(float tijd, int node, int hoek)
    {
        if (hoek < 0 || hoek > 180)
        {
            Debug.LogWarning($"[ShowControl] Servohoek {hoek} valt buiten 0..180 — ik kort hem af.", this);
            hoek = Mathf.Clamp(hoek, 0, 180);
        }
        Plan(tijd, node, addr => verbinding.SendServo(addr, hoek));
        PlanServoHoek(tijd, node, hoek);
    }

    /// Zet na 'tijd' seconden, op 'node', de stekkerdoos (socket) AAN.
    public void SocketAan(float tijd, int node)
    {
        Plan(tijd, node, addr => verbinding.SendSocket(addr, true));
        PlanAfbeelding(tijd, node, true);
    }

    /// Zet na 'tijd' seconden, op 'node', de stekkerdoos (socket) UIT.
    public void SocketUit(float tijd, int node)
    {
        Plan(tijd, node, addr => verbinding.SendSocket(addr, false));
        PlanAfbeelding(tijd, node, false);
    }

    /// Zet na 'tijd' seconden, op 'node', een uitgang (pin 0..4) hoog (aan = true) of laag (aan = false).
    public void ZetPin(float tijd, int node, int pin, bool aan)
    {
        if (pin < 0 || pin > 4)
        {
            Debug.LogWarning($"[ShowControl] Pin {pin} bestaat niet (gebruik 0 t/m 4) — niets ingepland.", this);
            return;
        }
        Plan(tijd, node, addr => verbinding.SendPin(addr, pin, aan));
    }

    /// Laat na 'tijd' seconden, op 'node', de Node zich identificeren (LEDs knipperen even).
    public void StuurIdent(float tijd, int node)
    {
        Plan(tijd, node, addr => verbinding.Ident(addr));
    }

    /// Speelt na 'tijd' seconden het geluid met de gegeven naam af (zie de lijst 'Geluiden' in de Inspector).
    public void Speelgeluid(float tijd, string naamVanGeluid)
    {
        StartCoroutine(WachtEnSpeelGeluid(Mathf.Max(0f, tijd), naamVanGeluid));
    }

    IEnumerator WachtEnSpeelGeluid(float tijd, string naam)
    {
        if (tijd > 0f) yield return new WaitForSeconds(tijd);

        Geluid gevonden = geluiden.Find(g => string.Equals(g.naam, naam, System.StringComparison.OrdinalIgnoreCase));
        if (gevonden == null || gevonden.clip == null)
        {
            Debug.LogWarning($"[ShowControl] Geluid '{naam}' niet gevonden — controleer de lijst 'Geluiden' in de Inspector.", this);
            yield break;
        }
        audioBron.PlayOneShot(gevonden.clip);
    }

    // Simuleert in de Scene wat er bij een Node gebeurt: zet de bijbehorende afbeelding
    // na 'tijd' seconden aan (zichtbaar) of uit (onzichtbaar).
    void PlanAfbeelding(float tijd, int node, bool zichtbaar)
    {
        StartCoroutine(WachtEnZetAfbeelding(Mathf.Max(0f, tijd), node, zichtbaar));
    }

    IEnumerator WachtEnZetAfbeelding(float tijd, int node, bool zichtbaar)
    {
        if (tijd > 0f) yield return new WaitForSeconds(tijd);

        GameObject afbeelding = VindNodeAfbeelding(node);
        if (afbeelding == null)
        {
            Debug.LogWarning($"[ShowControl] Geen afbeelding gekoppeld aan 'Node{node}' — niets zichtbaar gemaakt.", this);
            yield break;
        }
        afbeelding.SetActive(zichtbaar);
    }

    // Draait in de Scene de afbeelding van een Node mee met de servohoek (in graden), zodat
    // je ziet wat de servo fysiek zou doen.
    void PlanServoHoek(float tijd, int node, int hoek)
    {
        StartCoroutine(WachtEnZetServoHoek(Mathf.Max(0f, tijd), node, hoek));
    }

    IEnumerator WachtEnZetServoHoek(float tijd, int node, int hoek)
    {
        if (tijd > 0f) yield return new WaitForSeconds(tijd);

        GameObject afbeelding = VindNodeAfbeelding(node);
        if (afbeelding == null)
        {
            Debug.LogWarning($"[ShowControl] Geen afbeelding gekoppeld aan 'Node{node}' — servohoek niet getoond.", this);
            yield break;
        }
        afbeelding.transform.localRotation = Quaternion.Euler(0f, 0f, hoek);
    }

    GameObject VindNodeAfbeelding(int node)
    {
        string naam = $"Node{node}";
        NodeAfbeelding gevonden = nodeAfbeeldingen.Find(n => n.naam == naam);
        return gevonden?.afbeelding;
    }

    // ───────────────────────────── kleine helpers ─────────────────────────────

    // Plan één actie in: wacht 'tijd' seconden, controleer dan de verbinding en het
    // Node-adres, en voer 'actie' pas op dat moment uit.
    void Plan(float tijd, int node, System.Action<byte> actie)
    {
        if (!HeeftVerbinding()) return;
        byte addr = (byte)Mathf.Clamp(node, 1, 63);
        StartCoroutine(WachtEnVoerUit(Mathf.Max(0f, tijd), addr, actie));
    }

    IEnumerator WachtEnVoerUit(float tijd, byte addr, System.Action<byte> actie)
    {
        if (tijd > 0f) yield return new WaitForSeconds(tijd);
        actie(addr);
    }

    bool HeeftVerbinding()
    {
        if (verbinding != null) return true;
        Debug.LogError("[ShowControl] Geen SerialController gekoppeld. " +
                       "Zet er één in de scene of vul het veld 'Verbinding' in.", this);
        return false;
    }

    void ShowList()
    {
        Event.SocketAan(1f,1);
        Event.ZetServo(2f,1,30);
        Event.ZetServo(4f,1,60);
        Event.ZetServo(6f,1,90);

        Event.SocketUit(8f,1);
        Event.SocketAan(3f,2);
        Event.SocketUit(6f,2);
        Event.Speelgeluid(5f,"Shh");
        Event.Speelgeluid(2f,"Piano");
    }
    
}
