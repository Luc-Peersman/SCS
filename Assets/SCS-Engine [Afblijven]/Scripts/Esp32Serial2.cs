using UnityEngine;
using System.IO.Ports;
using System.Threading;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System;

public class EspSerial2 : MonoBehaviour
{
    public string portName = "COM6";
    public int baudRate = 115200;

    private SerialPort sp;
    private Thread readThread;
    private volatile bool running = false;

    private ConcurrentQueue<string> incomingLines = new();

    public Dictionary<int, NodeState> nodes = new();

    [System.Serializable]
    public class NodeState
    {
        public int address;
        public int fwVersion;
        public bool online;
        public float lastSeen;
    }

    void Start()
    {
        sp = new SerialPort(portName, baudRate)
        {
            ReadTimeout = 2000,
            WriteTimeout = 500,
            DtrEnable = false,
            RtsEnable = false,
            NewLine = "\n"
        };

        try
        {
            sp.Open();
            Debug.Log($"[SCS] Poort {portName} open.");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SCS] Kan poort niet openen: {e.Message}");
            return;
        }

        running = true;
        readThread = new Thread(ReadLoop) { IsBackground = true };
        readThread.Start();

        Invoke(nameof(SendInitialPing), 5f);
    }

    void SendInitialPing()
    {
        SendCommand("FF PING");
        Debug.Log("[SCS] FF PING verzonden");

        // Stuur 3 seconden later nog een ping
        Invoke(nameof(SendSecondPing), 3f);
    }

    void SendSecondPing()
    {
        SendCommand("FF PING");
        Debug.Log("[SCS] FF PING #2 verzonden");
    }

    void ReadLoop()
    {
        Thread.Sleep(500);

        while (running && sp != null && sp.IsOpen)
        {
            try
            {
                string line = sp.ReadLine();
                if (!string.IsNullOrWhiteSpace(line))
                    incomingLines.Enqueue(line.Trim());
            }
            catch (TimeoutException)
            {
                // Geen data, ga door
            }
            catch (System.Exception e)
            {
                if (running)
                    incomingLines.Enqueue($"[LEESFOUT] {e.Message}");
                break;
            }
        }
    }

    void Update()
    {
        while (incomingLines.TryDequeue(out string line))
        {
            ParseLine(line);
        }
    }

    void ParseLine(string line)
    {
        if (line.StartsWith("["))
        {
            Debug.Log($"[NEXUS] {line}");
            return;
        }

        Debug.Log($"[SCS RAW] {line}");

        line = line.TrimEnd(';');
        string[] parts = line.Split(' ');
        if (parts.Length < 2) return;

        int addr;
        try { addr = System.Convert.ToInt32(parts[0], 16); }
        catch { return; }

        string msgType = parts[1].ToUpper();

        switch (msgType)
        {
            case "HELLO":
                int fw = 0;
                if (parts.Length > 2 && parts[2].StartsWith("v"))
                    int.TryParse(parts[2].Substring(1), out fw);
                RegisterNode(addr, fw);
                Debug.Log($"[SCS] Node 0x{addr:X2} meldt zich: firmware v{fw}");
                break;

            case "ONLINE":
                if (!nodes.ContainsKey(addr)) RegisterNode(addr, 0);
                nodes[addr].online = true;
                nodes[addr].lastSeen = Time.time;
                Debug.Log($"[SCS] Node 0x{addr:X2} ONLINE");
                break;

            case "OFFLINE":
                if (nodes.ContainsKey(addr))
                {
                    nodes[addr].online = false;
                    Debug.Log($"[SCS] Node 0x{addr:X2} OFFLINE");
                }
                break;

            case "ACK":
                Debug.Log($"[SCS] Node 0x{addr:X2} ACK {(parts.Length > 2 ? parts[2] : "?")}");
                break;

            case "BTN":
                int btn = parts.Length > 2 ? int.Parse(parts[2]) : 0;
                Debug.Log($"[SCS] Node 0x{addr:X2} KNOP {btn}");
                break;

            case "STAT":
                Debug.Log($"[SCS] Node 0x{addr:X2} STAT: {line}");
                break;

            default:
                Debug.Log($"[SCS] Node 0x{addr:X2} onbekend bericht: {msgType}");
                break;
        }
    }

    void RegisterNode(int addr, int fw)
    {
        if (!nodes.ContainsKey(addr))
            nodes[addr] = new NodeState { address = addr };
        nodes[addr].fwVersion = fw;
        nodes[addr].online = true;
        nodes[addr].lastSeen = Time.time;
    }

    public void SendCommand(string cmd)
    {
        if (sp == null || !sp.IsOpen) return;
        try { sp.WriteLine(cmd); }
        catch (System.Exception e) { Debug.LogWarning($"[SCS] Schrijffout: {e.Message}"); }
    }

    public void PingAll() => SendCommand("FF PING");
    public void SetServo(int node, int angle) => SendCommand($"{node:X2} SRV {angle}");
    public void SetSocket(int node, int val) => SendCommand($"{node:X2} SOCKET {val}");
    public void RequestStat(int node) => SendCommand($"{node:X2} STAT");

    void OnDestroy()
    {
        running = false;
        readThread?.Join(500);
        if (sp != null && sp.IsOpen) sp.Close();
    }
}