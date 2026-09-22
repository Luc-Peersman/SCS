// ============================================================================
//  ShowControl.cs  —  Eén tijdlijn voor je hele show, over ALLE Nodes heen.
//
//  WAT IS DIT:
//   Zet dit script op één (leeg) GameObject in de scene. Daarna kun je in
//   Start() (of vanuit eender welk script) een hele reeks acties inplannen,
//   elk met een eigen tijdstip en een eigen doel-Node:
//
//      Cue.ZetServo(2, 3, 45);   // na 2 s: Node 3, servo naar 45°
//      Cue.ZetServo(5, 1, 80);   // na 5 s: Node 1, servo naar 80°
//      Cue.SocketAan(3, 2);      // na 3 s: Node 2, stekkerdoos AAN
//
//   Elke regel plant zijn eigen actie in — de tijden zijn onafhankelijk van
//   elkaar (dus GEEN "wacht op de vorige stap"), gewoon "op tijdstip X vanaf
//   het moment dat Start() draait".
//
//  HOE GEBRUIK JE HET:
//   1) Maak één (leeg) GameObject en sleep dit ShowControl-script erop.
//      (er mag er maar ÉÉN in de scene staan — via 'Cue' kun je hem overal
//      aanspreken zonder een referentie te moeten slepen)
//   2) De SerialController wordt automatisch gevonden (of sleep hem zelf in
//      het veld 'Verbinding').
//   3) Typ je tijdlijn in Start() als 'Cue.<methode>(...)'. Doe je dit vanuit
//      een ANDER script (zoals ShowList.cs), zet dan bovenaan dat script
//      'using static ShowControl;' — anders herkent C# de naam 'Cue' niet.
//
//  Werkt samen met SerialController (V003-protocol).
// ============================================================================
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShowControl : MonoBehaviour
{
    /// Wereldwijde toegang: vanuit eender welk script/methode gebruik je "Cue.ZetServo(...)".
    public static ShowControl Cue { get; private set; }

    [Header("Verbinding (leeg laten = zelf zoeken)")]
    [Tooltip("De SerialController in de scene. Laat dit leeg; dan zoekt ShowControl hem automatisch.")]
    public SerialController verbinding;

    [Header("Tijdlijn (ShowList-script waar de Geluiden-lijst in staat)")]
    [Tooltip("Het ShowList-script (bv. op GameObject 'ShowFile'). Laat dit leeg; dan zoekt ShowControl hem automatisch.")]
    public ShowList tijdlijn;

    [System.Serializable]
    public class Geluid
    {
        [Tooltip("De naam die je gebruikt in Cue.SpeelGeluid(tijd, uitgang, naam, kant).")]
        public string naam;
        public AudioClip clip;
        [Range(0f, 1f)]
        [Tooltip("Volume van dit geluid — handig om verschillende clips op elkaar af te stemmen.")]
        public float volume = 1f;
    }

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

    [Header("Audio-router (ASIO, voor SpeelGeluid)")]
    [Tooltip("Naam (of deel van de naam) van de ASIO-driver, zoals te zien in je ASIO-instellingen.")]
    public string asioDriverNaam = "Focusrite USB ASIO";
    [Tooltip("Sample rate waarop de ASIO-driver staat ingesteld (zie Focusrite Control 2).")]
    public int asioSampleRate = 48000;

    AsioUitgangRouter uitgangRouter;

    [Header("Lokale audio (testen zonder Scarlett/ASIO — bv. op je eigen laptop)")]
    [Tooltip("AudioSource voor Cue.SpeelGeluidLokaal. Laat leeg; wordt automatisch aangemaakt op dit GameObject.")]
    public AudioSource lokaleAudioBron;

    void Awake()
    {
        if (Cue != null && Cue != this)
        {
            Debug.LogWarning("[ShowControl] Er staat al een ShowControl in de scene — deze extra wordt genegeerd.", this);
            Destroy(this);
            return;
        }
        Cue = this;

        uitgangRouter = new AsioUitgangRouter();
        uitgangRouter.Start(asioDriverNaam, asioSampleRate);

        if (lokaleAudioBron == null) lokaleAudioBron = GetComponent<AudioSource>();
        if (lokaleAudioBron == null) lokaleAudioBron = gameObject.AddComponent<AudioSource>();
        lokaleAudioBron.playOnAwake = false;
        // 2D geluid: geen positie-gebaseerde panning/verzwakking, zodat elk kanaal altijd
        // even hard op beide boxjes klinkt, ongeacht waar dit GameObject in de scene staat.
        lokaleAudioBron.spatialBlend = 0f;

        foreach (NodeAfbeelding n in nodeAfbeeldingen)
            if (n.afbeelding != null) n.afbeelding.SetActive(false);
    }

    void OnEnable()
    {
        if (verbinding == null) verbinding = FindFirstObjectByType<SerialController>();
        if (verbinding == null)
            Debug.LogError("[ShowControl] Geen SerialController in de scene gevonden. " +
                           "Zet er één in de scene, of sleep hem in het veld 'Verbinding'.", this);

        if (tijdlijn == null) tijdlijn = FindFirstObjectByType<ShowList>();
        if (tijdlijn == null)
            Debug.LogError("[ShowControl] Geen ShowList in de scene gevonden. " +
                           "Zet dat script ergens in de scene, of sleep het in het veld 'Tijdlijn'.", this);
    }

    void OnDestroy()
    {
        if (Cue == this) Cue = null;
        uitgangRouter?.Dispose();
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

    /// Speelt na 'tijd' seconden het geluid met de gegeven naam af op Scarlett-uitgang 'uitgang'.
    /// 'kant' bepaalt welk kanaal van het (eventueel stereo) bronbestand wordt gebruikt — bij een
    /// mono bronbestand maakt 'kant' niet uit. Kant.BEIDE mixt links en rechts samen tot één mono signaal.
    public void SpeelGeluid(float tijd, int uitgang, string naamVanGeluid, Kant kant)
    {
        SpeelGeluidOpUitgangen(tijd, naamVanGeluid, kant, uitgang);
    }

    /// Speelt na 'tijd' seconden hetzelfde geluid gelijktijdig af op meerdere uitgangen — bv.
    /// Cue.SpeelGeluidOpUitgangen(1f, "Beam", Kant.RECHTS, 3, 4);
    public void SpeelGeluidOpUitgangen(float tijd, string naamVanGeluid, Kant kant, params int[] uitgangen)
    {
        StartCoroutine(WachtEnSpeelGeluidOpUitgangen(Mathf.Max(0f, tijd), naamVanGeluid, kant, uitgangen));
    }

    /// Speelt na 'tijd' seconden het geluid met de gegeven naam af via de normale audio-uitgang van
    /// deze computer (geen Scarlett/ASIO nodig) — handig om je tijdlijn te testen op je eigen laptop.
    /// Bij Kant.BEIDE hoor je gewone stereoweergave (links/rechts ongewijzigd). Bij Kant.LINKS of
    /// Kant.RECHTS hoor je het VOLLEDIGE geluid (links- en rechterkanaal samengevoegd) uit slechts
    /// één box — de andere box blijft stil.
    public void SpeelGeluidLokaal(float tijd, string naamVanGeluid, Kant kant)
    {
        StartCoroutine(WachtEnSpeelGeluidLokaal(Mathf.Max(0f, tijd), naamVanGeluid, kant));
    }

    IEnumerator WachtEnSpeelGeluidLokaal(float tijd, string naam, Kant kant)
    {
        if (tijd > 0f) yield return new WaitForSeconds(tijd);

        if (tijdlijn == null)
        {
            Debug.LogError("[ShowControl] Geen ShowList gekoppeld — kan de Geluiden-lijst niet opzoeken.", this);
            yield break;
        }
        Geluid gevonden = tijdlijn.geluiden.Find(g => string.Equals(g.naam, naam, System.StringComparison.OrdinalIgnoreCase));
        if (gevonden == null || gevonden.clip == null)
        {
            Debug.LogWarning($"[ShowControl] Geluid '{naam}' niet gevonden — controleer de lijst 'Geluiden' in de Inspector.", this);
            yield break;
        }

        if (kant == Kant.BEIDE)
        {
            // Normale stereoweergave: niets aanpassen aan het bronbestand.
            lokaleAudioBron.PlayOneShot(gevonden.clip, gevonden.volume);
        }
        else
        {
            AudioClip eenzijdig = MaakEenzijdigeClip(gevonden.clip, kant, naam);
            lokaleAudioBron.PlayOneShot(eenzijdig, gevonden.volume);
        }
    }

    // Zet het VOLLEDIGE geluid (links- en rechterkanaal samengevoegd) op één luidsprekerkant:
    // die kant krijgt het volledige signaal, de andere kant blijft stil. Wordt per aanroep vers
    // opgebouwd (i.p.v. AudioSource.panStereo) zodat overlappende geluiden met verschillende
    // Kant elkaars panning niet beïnvloeden.
    AudioClip MaakEenzijdigeClip(AudioClip bron, Kant kant, string naam)
    {
        float[] alle = new float[bron.samples * bron.channels];
        bron.GetData(alle, 0);

        float[] stereo = new float[bron.samples * 2];
        int actieveKanaal = kant == Kant.LINKS ? 0 : 1;
        for (int i = 0; i < bron.samples; i++)
        {
            float sample = bron.channels == 1
                ? alle[i]
                : 0.5f * (alle[i * bron.channels + 0] + alle[i * bron.channels + 1]);
            stereo[i * 2 + actieveKanaal] = sample;
            // de andere kant blijft op 0 (stil) — de array is al met nullen geïnitialiseerd.
        }

        AudioClip clip = AudioClip.Create($"{naam}_{kant}", bron.samples, 2, bron.frequency, false);
        clip.SetData(stereo, 0);
        return clip;
    }

    /// Stopt na 'tijd' seconden het geluid dat op 'uitgang' speelt. Speelt er niets, dan gebeurt er niets.
    public void StopGeluid(float tijd, int uitgang)
    {
        StartCoroutine(WachtEnStopGeluidOpUitgang(Mathf.Max(0f, tijd), uitgang));
    }

    IEnumerator WachtEnStopGeluidOpUitgang(float tijd, int uitgang)
    {
        if (tijd > 0f) yield return new WaitForSeconds(tijd);
        uitgangRouter?.StopOpKanaal(uitgang);
    }

    IEnumerator WachtEnSpeelGeluidOpUitgangen(float tijd, string naam, Kant kant, int[] uitgangen)
    {
        if (tijd > 0f) yield return new WaitForSeconds(tijd);

        if (tijdlijn == null)
        {
            Debug.LogError("[ShowControl] Geen ShowList gekoppeld — kan de Geluiden-lijst niet opzoeken.", this);
            yield break;
        }
        Geluid gevonden = tijdlijn.geluiden.Find(g => string.Equals(g.naam, naam, System.StringComparison.OrdinalIgnoreCase));
        if (gevonden == null || gevonden.clip == null)
        {
            Debug.LogWarning($"[ShowControl] Geluid '{naam}' niet gevonden — controleer de lijst 'Geluiden' in de Inspector.", this);
            yield break;
        }
        if (uitgangRouter == null || !uitgangRouter.IsActief)
        {
            // Geen Scarlett/ASIO aangesloten is geen fout (bv. een student die op zijn eigen
            // laptop oefent) — gewoon niets afspelen. Gebruik Cue.SpeelGeluidLokaal om zonder
            // audio-interface te testen.
            Debug.Log($"[ShowControl] Geen ASIO-uitgangrouter actief — '{naam}' wordt niet afgespeeld " +
                      "op de Scarlett. Gebruik Cue.SpeelGeluidLokaal om lokaal te testen.", this);
            yield break;
        }

        float[] mono = HaalKanaalUit(gevonden.clip, kant);
        mono = AudioResampler.Resample(mono, gevonden.clip.frequency, uitgangRouter.SampleRate);
        if (gevonden.volume != 1f)
            for (int i = 0; i < mono.Length; i++) mono[i] *= gevonden.volume;
        uitgangRouter.SpeelOpKanalen(mono, uitgangen);
    }

    float[] HaalKanaalUit(AudioClip clip, Kant kant)
    {
        float[] alle = new float[clip.samples * clip.channels];
        clip.GetData(alle, 0);

        if (clip.channels == 1) return alle;

        float[] mono = new float[clip.samples];
        if (kant == Kant.BEIDE)
        {
            for (int i = 0; i < clip.samples; i++)
                mono[i] = 0.5f * (alle[i * clip.channels + 0] + alle[i * clip.channels + 1]);
        }
        else
        {
            int gekozenIndex = kant == Kant.LINKS ? 0 : 1;
            for (int i = 0; i < clip.samples; i++)
                mono[i] = alle[i * clip.channels + gekozenIndex];
        }
        return mono;
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
}
