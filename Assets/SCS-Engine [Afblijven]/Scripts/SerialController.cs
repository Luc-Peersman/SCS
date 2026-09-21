// ============================================================================
//  SerialController.cs  —  ESP32 Show Control System (Unity-kant, V003)
//  De ENIGE MonoBehaviour die de seriële poort naar de Nexus beheert.
//
//  WAT VOER JE IN  (verzenden): roep de Send*-methodes aan vanuit je eigen
//                  MonoBehaviour. Elke methode schrijft één tekstregel + '\n'.
//  WAT KRIJG JE TERUG (ontvangen): abonneer je op de C#-events hieronder.
//                  Ze vuren op de MAIN-thread (vanuit Update) → Unity-API is veilig.
//
//  Vereist in Unity: Project Settings → Player → Api Compatibility Level =
//  ".NET Framework" (niet .NET Standard) zodat System.IO.Ports beschikbaar is.
//
//  Geverifieerd tegen Nexus_V003.ino / Node_V003.ino / SCSMMProtocol.h.
// ============================================================================
using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO.Ports;
using System.Text;
using System.Threading;
using UnityEngine;

public class SerialController : MonoBehaviour
{
    [Header("Seriële poort (naar de Nexus)")]
    public string portName = "COM6";   // Windows: COMx · macOS: /dev/tty.usbmodem* · Linux: /dev/ttyACM*
    public int baudRate = 115200;

    // ───────────────────── OUTPUT — events die je abonneert ─────────────────────
    //  (addr is het Node-adres als byte; op de draad/serieel hex, hier gewoon een getal)
    public event Action<byte> NodeOnline;     // "AA ONLINE;"
    public event Action<byte> NodeOffline;    // "AA OFFLINE;"  (>10 s stil)
    public event Action<byte, int> Hello;          // "AA HELLO v8;"  (boot/wake/PING)
    public event Action<byte, string, int> Ack;            // "AA ACK SRV 90;"
    public event Action<byte, string> NoAck;          // "AA NOACK SOCKET;" (kritiek cmd, V003)
    public event Action<byte, int> ButtonPressed;  // "AA BTN 1;"
    public event Action<byte, int, int> PinChanged;     // "AA PIN 4 1;"  (pin, waarde)
    public event Action<byte, string, uint> IrReceived;     // "AA IR NEC 0x00FF6897;"
    public event Action<byte, StatInfo> Status;         // "AA STAT SRV=.. SOCKET=.. PINS=0x.. DROPS=..;"
    public event Action<string> DebugLine;      // "[NEXUS] ..."  (alleen log)
    public event Action<string> RawLine;        // elke regel ongefilterd (logvenster)

    public struct StatInfo
    {
        public int servo;    // SRV=    (0..180)
        public int socket;   // SOCKET= (0/1, = bit 0 van pins)
        public uint pins;     // PINS=0x.. (bitmasker van de 5 externe pinnen)
        public int drops;    // DROPS=  (gedropte berichten, verzadigd 0..255)
    }

    // ───────────────────────────── intern ─────────────────────────────
    SerialPort _port;
    Thread _reader;
    volatile bool _running;
    readonly ConcurrentQueue<string> _rx = new ConcurrentQueue<string>();
    readonly object _writeLock = new object();

    void OnEnable()
    {
        try
        {
            _port = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
            {
                ReadTimeout = 100,   // ms — korte timeout zodat de thread netjes kan stoppen
                WriteTimeout = 100,
                DtrEnable = true,  // zet op false als jouw Nexus bij verbinden reset
                RtsEnable = false
            };
            _port.Open();
            Debug.Log($"Poort {portName} open");
      
        }
        catch (Exception e)
        {
            Debug.LogError($"[SerialController] kan poort '{portName}' niet openen: {e.Message}");
            return;
        }

        _running = true;
        _reader = new Thread(ReadLoop) { IsBackground = true, Name = "SerialReader" };
        _reader.Start();

       // PingAll();   // ontdek welke Nodes er zijn → ze antwoorden met HELLO
     //   Debug.Log("PingAll() Called");
    }

    void OnDisable()
    {
        _running = false;
        try { _reader?.Join(300); } catch { /* ignore */ }
        try { if (_port != null && _port.IsOpen) _port.Close(); } catch { /* ignore */ }
        _port = null;
    }

    // ══════════════════ INPUT — methodes die JIJ aanroept ══════════════════
    //  addr = Node-adres (0x01..0x3F) of 0xFF voor broadcast (alle Nodes).
    public void SendServo(byte addr, int angle) => Send($"{addr:X2} SRV {Mathf.Clamp(angle, 0, 180)}");
    public void SendSocket(byte addr, bool on) => Send($"{addr:X2} SOCKET {(on ? 1 : 0)}");
    public void SendPinMode(byte addr, int pin, bool output) => Send($"{addr:X2} PINMODE {pin} {(output ? "OUT" : "IN")}");
    public void SendPin(byte addr, int pin, bool high) => Send($"{addr:X2} PIN {pin} {(high ? 1 : 0)}");
    public void SendIr(byte addr, uint necCode) => Send($"{addr:X2} IRSEND NEC 0x{necCode:X8}");
    public void Ident(byte addr) => Send($"{addr:X2} IDENT");
    public void RequestStat(byte addr) => Send($"{addr:X2} STAT");
    public void Ping(byte addr) => Send($"{addr:X2} PING");
    public void PingAll() => Send("FF PING");

    /// Onbewerkt een commando sturen. De enige plek die echt schrijft.
    public void Send(string line)
    {
        if (_port == null || !_port.IsOpen) return;
        lock (_writeLock)
        {
            try { _port.Write(line + "\n"); }   // '\n' is wat de Nexus verwacht — NIET WriteLine gebruiken
            catch (Exception e) { Debug.LogWarning($"[SerialController] schrijffout: {e.Message}"); }
        }
    }

    // ══════════════════ achtergrond-leesthread (geen Unity-API hier!) ══════════════════
    void ReadLoop()
    {
        var sb = new StringBuilder(128);
        while (_running)
        {
            try
            {
                int b = _port.ReadByte();              // blokkeert tot een byte of timeout
                if (b < 0) continue;
                char c = (char)b;
                if (c == '\n') { _rx.Enqueue(sb.ToString()); sb.Clear(); }
                else if (c != '\r') { sb.Append(c); }  // '\r' overal weggooien
            }
            catch (TimeoutException) { /* normaal: niets binnen 100 ms */ }
            catch (Exception) { if (_running) Thread.Sleep(5); }
        }
    }

    // ══════════════════ OUTPUT — verwerken op de MAIN-thread ══════════════════
    void Update()
    {
        // (V003: GEEN keepalive nodig — de Nexus broadcast zelf elke 1 s een beacon
        //  die alle Nodes uit failsafe houdt. Failsafe = de Nexus is weg.)
        while (_rx.TryDequeue(out string raw))
            Classify(raw);
    }

    void Classify(string raw)
    {
        string line = raw.Trim();
        if (line.Length == 0) return;
        RawLine?.Invoke(line);

        if (line[0] == '[') { DebugLine?.Invoke(line); return; }  // debug → negeren
        if (line[line.Length - 1] != ';') return;                               // onvolledig
        line = line.Substring(0, line.Length - 1);                                     // ';' eraf

        string[] t = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
        if (t.Length < 2) return;

        byte addr;
        try { addr = Convert.ToByte(t[0], 16); } catch { return; }   // adres = HEX

        switch (t[1])
        {
            case "ONLINE": NodeOnline?.Invoke(addr); break;
            case "OFFLINE": NodeOffline?.Invoke(addr); break;
            case "HELLO": Hello?.Invoke(addr, ParseVersion(Get(t, 2))); break;
            case "ACK": Ack?.Invoke(addr, Get(t, 2), ParseInt(t, 3)); break;
            case "NOACK": NoAck?.Invoke(addr, Get(t, 2)); break;
            case "BTN": ButtonPressed?.Invoke(addr, ParseInt(t, 2)); break;
            case "PIN": PinChanged?.Invoke(addr, ParseInt(t, 2), ParseInt(t, 3)); break;
            case "IR": IrReceived?.Invoke(addr, Get(t, 2), ParseHex(Get(t, 3))); break;
            case "STAT": Status?.Invoke(addr, ParseStat(t)); break;
                // onbekende protocolregels negeren we stilletjes
        }
    }

    // ───────────────────────────── parse-helpers ─────────────────────────────
    static string Get(string[] t, int i) => (i < t.Length) ? t[i] : "";

    static int ParseInt(string[] t, int i) =>
        (i < t.Length && int.TryParse(t[i], out int v)) ? v : 0;

    static int ParseVersion(string s)            // "v8" → 8
    {
        if (s.StartsWith("v") || s.StartsWith("V")) s = s.Substring(1);
        return int.TryParse(s, out int v) ? v : 0;
    }

    static uint ParseHex(string s)               // "0x00FF6897" → 0x00FF6897
    {
        if (s.StartsWith("0x") || s.StartsWith("0X")) s = s.Substring(2);
        return uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint v) ? v : 0u;
    }

    static StatInfo ParseStat(string[] t)        // SRV=90 SOCKET=1 PINS=0x05 DROPS=0
    {
        var s = new StatInfo();
        for (int i = 2; i < t.Length; i++)
        {
            int eq = t[i].IndexOf('=');
            if (eq < 0) continue;
            string k = t[i].Substring(0, eq);
            string v = t[i].Substring(eq + 1);
            switch (k)
            {
                case "SRV": int.TryParse(v, out s.servo); break;
                case "SOCKET": int.TryParse(v, out s.socket); break;
                case "DROPS": int.TryParse(v, out s.drops); break;
                case "PINS": s.pins = ParseHex(v); break;   // heel hex-token, nooit afkappen
            }
        }
        return s;
    }
}
