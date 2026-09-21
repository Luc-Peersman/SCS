// ============================================================================
//  ShowControlTester.cs  —  Test Node + Nexus volledig vanuit Unity.
//  Sleep dit script op HETZELFDE GameObject als SerialController.
//
//  WAT KAN JE HIERMEE:
//   1) DASHBOARD       : zie live welke Nodes ONLINE zijn (groen lampje + "Xs geleden").
//   2) HANDMATIG TESTEN: knoppen voor servo / socket / pin / IR / PING / STAT / IDENT.
//   3) ZELFTEST        : 1 klik stuurt een hele reeks commando's en controleert per stap
//                        of het juiste ACK terugkomt -> PASS (groen) of FAIL/timeout (rood).
//   4) LOGVENSTER      : alle binnenkomende regels, kleur-gecodeerd.
//
//  GEEN Canvas nodig: alles wordt met OnGUI getekend. Druk op Play.
//  LET OP: sluit de seriële monitor van de Nexus voordat je in Unity op Play drukt,
//          anders is de COM-poort bezet.
//
//  Werkt met Nexus_V004 / Node_V004 (protocol identiek aan V003).
// ============================================================================
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

[RequireComponent(typeof(SerialController))]
public class ShowControlTester : MonoBehaviour
{
    [Header("Doel-Node (DIP-adres in HEX, bv. 01)")]
    public byte targetNode = 0x01;

    [Header("Hoe lang wachten op een antwoord (seconden)")]
    public float responseTimeout = 1.0f;   // ACK_WINDOW op de Nexus is 600 ms -> 1 s geeft marge

    SerialController serial;

    // ───────── online-administratie (gevoed door de events) ─────────
    readonly Dictionary<byte, float> onlineNodes = new Dictionary<byte, float>(); // addr -> tijd laatst gezien
    readonly HashSet<byte> isOnline = new HashSet<byte>();

    // ───────── laatste antwoorden (gebruikt door de zelftest) ─────────
    bool ackArrived; byte ackAddr; string ackCmd = ""; int ackVal;
    bool helloArrived; byte helloAddr; int helloVer;
    bool statArrived; byte statAddr;

    // ───────── logvenster ─────────
    readonly List<string> logLines = new List<string>();
    Vector2 logScroll;
    const int MAX_LOG = 200;
    bool showNexusDebug = true;        // toon ook "[NEXUS] ..."-regels (diag, NODES-lijst)

    // ───────── zelftest-status ─────────
    bool testRunning;
    struct Step { public string name; public int status; }   // status: 0=bezig/leeg, 1=PASS, 2=FAIL
    readonly List<Step> steps = new List<Step>();

    // ───────── GUI-velden ─────────
    string addrText = "01";
    int servoAngle = 90;
    bool socketState;
    int pinNr = 1;
    string irHex = "00FF6897";
    string rawCmd = "01 PING";
    GUIStyle logStyle, dotStyle;

    void Awake() => serial = GetComponent<SerialController>();

    // ───────── abonneren / afmelden ─────────
    void OnEnable()
    {
        addrText = targetNode.ToString("X2");
        serial.NodeOnline += OnOnline;
        serial.NodeOffline += OnOffline;
        serial.Hello += OnHello;
        serial.Ack += OnAck;
        serial.NoAck += OnNoAck;
        serial.ButtonPressed += OnBtn;
        serial.PinChanged += OnPin;
        serial.IrReceived += OnIr;
        serial.Status += OnStat;
        serial.DebugLine += OnDebug;
    }
    void OnDisable()
    {
        serial.NodeOnline -= OnOnline;
        serial.NodeOffline -= OnOffline;
        serial.Hello -= OnHello;
        serial.Ack -= OnAck;
        serial.NoAck -= OnNoAck;
        serial.ButtonPressed -= OnBtn;
        serial.PinChanged -= OnPin;
        serial.IrReceived -= OnIr;
        serial.Status -= OnStat;
        serial.DebugLine -= OnDebug;
    }

    // ───────── event-handlers (vuren op de main-thread, veilig) ─────────
    void OnOnline(byte a) { onlineNodes[a] = Time.time; isOnline.Add(a); Log($"<color=#3ad13a>● ONLINE 0x{a:X2}</color>"); }
    void OnOffline(byte a) { isOnline.Remove(a); Log($"<color=#e05050>○ OFFLINE 0x{a:X2}</color>"); }
    void OnHello(byte a, int v) { Seen(a); helloArrived = true; helloAddr = a; helloVer = v; Log($"HELLO 0x{a:X2}  v{v}"); }
    void OnAck(byte a, string c, int v) { Seen(a); ackArrived = true; ackAddr = a; ackCmd = c; ackVal = v; Log($"<color=#7ec8ff>ACK 0x{a:X2}  {c} = {v}</color>"); }
    void OnNoAck(byte a, string c) { Log($"<color=#e05050>NOACK 0x{a:X2}  {c}  (niet aangekomen!)</color>"); }
    void OnBtn(byte a, int b) { Seen(a); Log($"BTN 0x{a:X2}  knop {b}"); }
    void OnPin(byte a, int p, int v) { Seen(a); Log($"PIN 0x{a:X2}  pin{p} = {v}"); }
    void OnIr(byte a, string proto, uint code) { Seen(a); Log($"IR 0x{a:X2}  {proto} 0x{code:X8}"); }
    void OnStat(byte a, SerialController.StatInfo s)
    {
        Seen(a); statArrived = true; statAddr = a;
        Log($"STAT 0x{a:X2}  SRV={s.servo} SOCKET={s.socket} PINS=0x{s.pins:X2} DROPS={s.drops}");
    }
    void OnDebug(string line) { if (showNexusDebug) Log($"<color=#a0a0a0>{line}</color>"); }

    void Seen(byte a) { onlineNodes[a] = Time.time; isOnline.Add(a); }

    void Log(string msg)
    {
        logLines.Add($"<color=#808080>{Time.time:0.0}s</color>  {msg}");
        if (logLines.Count > MAX_LOG) logLines.RemoveAt(0);
        logScroll.y = float.MaxValue;     // automatisch naar onderen scrollen
    }

    // ───────── helpers voor de zelftest ─────────
    void ClearAck() { ackArrived = false; ackCmd = ""; }
    bool AckIs(byte addr, string cmd, int val) =>
        ackArrived && ackAddr == addr && ackCmd == cmd && (val < 0 || ackVal == val);

    IEnumerator WaitUntil(System.Func<bool> cond, float timeout = -1f)
    {
        if (timeout < 0f) timeout = responseTimeout;
        float t = 0f;
        while (!cond() && t < timeout) { t += Time.deltaTime; yield return null; }
    }

    void SetStep(int i, bool ok)
    {
        if (i < 0 || i >= steps.Count) return;
        var s = steps[i]; s.status = ok ? 1 : 2; steps[i] = s;
    }
    void AddStep(string name) => steps.Add(new Step { name = name, status = 0 });

    IEnumerator SelfTest(byte addr)
    {
        testRunning = true;
        steps.Clear();
        AddStep("PING  -> HELLO");
        AddStep("STAT  -> status terug");
        AddStep("SERVO 0");
        AddStep("SERVO 90");
        AddStep("SERVO 180");
        AddStep("SOCKET aan");
        AddStep("SOCKET uit");
        AddStep("IDENT");
        Log($"<color=#ffd24d>── ZELFTEST gestart op 0x{addr:X2} ──</color>");
        int i = 0;

        // 1) PING -> verwacht HELLO (bewijst heen-en-weer-radio)
        helloArrived = false; serial.Ping(addr);
        yield return WaitUntil(() => helloArrived && helloAddr == addr);
        SetStep(i++, helloArrived && helloAddr == addr);

        // 2) STAT -> verwacht status
        statArrived = false; serial.RequestStat(addr);
        yield return WaitUntil(() => statArrived && statAddr == addr);
        SetStep(i++, statArrived && statAddr == addr);

        // 3..5) SERVO 0 / 90 / 180 -> verwacht ACK SRV=hoek
        foreach (int ang in new[] { 0, 90, 180 })
        {
            ClearAck(); serial.SendServo(addr, ang);
            yield return WaitUntil(() => AckIs(addr, "SRV", ang));
            SetStep(i++, AckIs(addr, "SRV", ang));
        }

        // 6) SOCKET aan -> ACK SOCKET=1
        ClearAck(); serial.SendSocket(addr, true);
        yield return WaitUntil(() => AckIs(addr, "SOCKET", 1));
        SetStep(i++, AckIs(addr, "SOCKET", 1));

        // 7) SOCKET uit -> ACK SOCKET=0
        ClearAck(); serial.SendSocket(addr, false);
        yield return WaitUntil(() => AckIs(addr, "SOCKET", 0));
        SetStep(i++, AckIs(addr, "SOCKET", 0));

        // 8) IDENT -> ACK IDENT (waarde negeren)
        //    LET OP: de Node draait eerst identBlink() (~960 ms blokkerende delay) en stuurt de
        //    ACK PAS DAARNA. Daarom hier een ruimere timeout dan de standaard responseTimeout.
        ClearAck(); serial.Ident(addr);
        yield return WaitUntil(() => AckIs(addr, "IDENT", -1), 2.5f);
        SetStep(i++, AckIs(addr, "IDENT", -1));

        serial.SendServo(addr, 90);   // veilige eindstand
        int pass = 0; foreach (var s in steps) if (s.status == 1) pass++;
        Log($"<color=#ffd24d>── ZELFTEST klaar: {pass}/{steps.Count} PASS ──</color>");
        testRunning = false;
    }

    // ───────── adres parsen ─────────
    void ApplyAddr()
    {
        if (byte.TryParse(addrText, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b))
            targetNode = b;
    }

    // ════════════════════════════════ GUI ════════════════════════════════
    void OnGUI()
    {
        if (logStyle == null)
        {
            logStyle = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true, fontSize = 11 };
            dotStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 13 };
        }

        DrawControls();
        DrawDashboardAndLog();
    }

    void DrawControls()
    {
        GUILayout.BeginArea(new Rect(10, 10, 360, Screen.height - 20), GUI.skin.box);
        GUILayout.Label("<b>Show Control — Tester</b>", new GUIStyle(GUI.skin.label) { richText = true, fontSize = 15 });

        // doel-node
        GUILayout.BeginHorizontal();
        GUILayout.Label("Doel-Node (hex):", GUILayout.Width(120));
        string newAddr = GUILayout.TextField(addrText, 2, GUILayout.Width(50));
        if (newAddr != addrText) { addrText = newAddr; ApplyAddr(); }
        bool tgtOnline = isOnline.Contains(targetNode);
        GUILayout.Label(tgtOnline ? "<color=#3ad13a>● ONLINE</color>" : "<color=#e05050>● offline</color>",
            new GUIStyle(GUI.skin.label) { richText = true });
        GUILayout.EndHorizontal();

        GUILayout.Space(6);
        GUILayout.Label("<b>Handmatig</b>", new GUIStyle(GUI.skin.label) { richText = true });

        // servo
        GUILayout.BeginHorizontal();
        GUILayout.Label($"Servo: {servoAngle}°", GUILayout.Width(90));
        servoAngle = Mathf.RoundToInt(GUILayout.HorizontalSlider(servoAngle, 0, 180));
        if (GUILayout.Button("Stuur", GUILayout.Width(60))) serial.SendServo(targetNode, servoAngle);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("0°")) { servoAngle = 0; serial.SendServo(targetNode, 0); }
        if (GUILayout.Button("90°")) { servoAngle = 90; serial.SendServo(targetNode, 90); }
        if (GUILayout.Button("180°")) { servoAngle = 180; serial.SendServo(targetNode, 180); }
        GUILayout.EndHorizontal();

        // socket
        if (GUILayout.Button($"Socket  →  {(socketState ? "zet UIT" : "zet AAN")}"))
        {
            socketState = !socketState;
            serial.SendSocket(targetNode, socketState);
        }

        // pin
        GUILayout.BeginHorizontal();
        GUILayout.Label("Pin:", GUILayout.Width(30));
        if (GUILayout.Button("-", GUILayout.Width(25))) pinNr = Mathf.Max(0, pinNr - 1);
        GUILayout.Label(pinNr.ToString(), GUILayout.Width(20));
        if (GUILayout.Button("+", GUILayout.Width(25))) pinNr = Mathf.Min(4, pinNr + 1);
        if (GUILayout.Button("OUT", GUILayout.Width(45))) serial.SendPinMode(targetNode, pinNr, true);
        if (GUILayout.Button("IN", GUILayout.Width(40))) serial.SendPinMode(targetNode, pinNr, false);
        if (GUILayout.Button("1", GUILayout.Width(25))) serial.SendPin(targetNode, pinNr, true);
        if (GUILayout.Button("0", GUILayout.Width(25))) serial.SendPin(targetNode, pinNr, false);
        GUILayout.EndHorizontal();

        // IR
        GUILayout.BeginHorizontal();
        GUILayout.Label("IR 0x", GUILayout.Width(40));
        irHex = GUILayout.TextField(irHex, 8, GUILayout.Width(90));
        if (GUILayout.Button("IR zenden", GUILayout.Width(90)))
            if (uint.TryParse(irHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint code))
                serial.SendIr(targetNode, code);
        GUILayout.EndHorizontal();

        // losse query-knoppen
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("PING")) serial.Ping(targetNode);
        if (GUILayout.Button("STAT")) serial.RequestStat(targetNode);
        if (GUILayout.Button("IDENT")) serial.Ident(targetNode);
        GUILayout.EndHorizontal();

        GUILayout.Space(6);
        GUILayout.Label("<b>Nexus</b>", new GUIStyle(GUI.skin.label) { richText = true });
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("PING ALLE (FF)")) serial.PingAll();
        if (GUILayout.Button("NODES-lijst")) serial.Send("NODES");
        GUILayout.EndHorizontal();

        // ruwe regel
        GUILayout.BeginHorizontal();
        rawCmd = GUILayout.TextField(rawCmd, GUILayout.Width(220));
        if (GUILayout.Button("Send", GUILayout.Width(60))) serial.Send(rawCmd);
        GUILayout.EndHorizontal();

        GUILayout.Space(8);
        // zelftest
        GUI.enabled = !testRunning;
        if (GUILayout.Button(testRunning ? "Zelftest loopt..." : $"▶ START ZELFTEST op 0x{targetNode:X2}"))
            StartCoroutine(SelfTest(targetNode));
        GUI.enabled = true;

        foreach (var s in steps)
        {
            string mark = s.status == 1 ? "<color=#3ad13a>✓ PASS</color>"
                        : s.status == 2 ? "<color=#e05050>✗ FAIL</color>"
                        : "<color=#a0a0a0>· …</color>";
            GUILayout.Label($"{mark}  {s.name}", new GUIStyle(GUI.skin.label) { richText = true });
        }

        GUILayout.EndArea();
    }

    void DrawDashboardAndLog()
    {
        float x = 380, w = Screen.width - 390;
        if (w < 260) w = 260;
        GUILayout.BeginArea(new Rect(x, 10, w, Screen.height - 20), GUI.skin.box);

        // online-nodes
        GUILayout.Label("<b>Online Nodes</b>", new GUIStyle(GUI.skin.label) { richText = true, fontSize = 14 });
        if (isOnline.Count == 0)
            GUILayout.Label("<color=#e05050>(geen Node online — check DIP7, adres, poort)</color>", dotStyle);
        else
        {
            var sorted = new List<byte>(isOnline); sorted.Sort();
            foreach (byte a in sorted)
            {
                float ago = onlineNodes.TryGetValue(a, out float ts) ? Time.time - ts : 0f;
                GUILayout.Label($"<color=#3ad13a>●</color>  0x{a:X2}   <color=#808080>{ago:0.0}s geleden</color>", dotStyle);
            }
        }

        GUILayout.Space(8);
        GUILayout.BeginHorizontal();
        GUILayout.Label("<b>Log</b>", new GUIStyle(GUI.skin.label) { richText = true, fontSize = 14 });
        GUILayout.FlexibleSpace();
        showNexusDebug = GUILayout.Toggle(showNexusDebug, " toon [NEXUS]-regels");
        if (GUILayout.Button("wis", GUILayout.Width(50))) logLines.Clear();
        GUILayout.EndHorizontal();

        logScroll = GUILayout.BeginScrollView(logScroll);
        foreach (string line in logLines) GUILayout.Label(line, logStyle);
        GUILayout.EndScrollView();

        GUILayout.EndArea();
    }
}
