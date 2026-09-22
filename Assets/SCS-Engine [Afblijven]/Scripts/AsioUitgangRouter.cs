using System;
using System.Collections.Generic;
using NAudio.Wave;
using UnityEngine;

// Stuurt losse mono-geluidsfragmenten naar een specifiek fysiek ASIO-uitgangskanaal
// (bv. Scarlett-uitgang 3), buiten Unity's eigen AudioSource-systeem om.
public class AsioUitgangRouter : IDisposable
{
    class Stem
    {
        public float[] samples;
        public int positie;
        public int kanaalIndex; // 0-based
    }

    AsioOut asio;
    int kanaalAantal;
    readonly List<Stem> stemmen = new List<Stem>();
    readonly object vergrendel = new object();

    public int SampleRate { get; private set; }
    public bool IsActief { get; private set; }

    public bool Start(string driverNaamDeel, int sampleRate)
    {
        SampleRate = sampleRate;

        string[] drivers = AsioOut.GetDriverNames();
        string gekozenDriver = Array.Find(drivers,
            d => d.IndexOf(driverNaamDeel, StringComparison.OrdinalIgnoreCase) >= 0);

        if (gekozenDriver == null)
        {
            // Geen fout — een laptop zonder Scarlett/ASIO-interface is een normale, ondersteunde
            // situatie (bv. studenten die oefenen met Cue.SpeelGeluidLokaal). Cue.SpeelGeluid(...)
            // doet dan gewoon niets, zonder foutmelding.
            Debug.Log($"[AsioUitgangRouter] Geen ASIO-driver gevonden die '{driverNaamDeel}' bevat — " +
                      "SpeelGeluid/SpeelGeluidOpUitgangen doen niets. Gebruik Cue.SpeelGeluidLokaal " +
                      "om zonder audio-interface te testen.");
            return false;
        }

        try
        {
            asio = new AsioOut(gekozenDriver);
            kanaalAantal = asio.DriverOutputChannelCount;
            var format = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, kanaalAantal);
            asio.InitRecordAndPlayback(new RoutingProvider(this, format), 0, sampleRate);
            asio.Play();
            IsActief = true;
            Debug.Log($"[AsioUitgangRouter] ASIO-driver '{gekozenDriver}' actief met {kanaalAantal} " +
                      $"uitgangskanalen op {sampleRate}Hz.");
            return true;
        }
        catch (System.Exception e)
        {
            // Bv. driver wel geïnstalleerd maar interface niet aangesloten/aan. Ook dan geen crash.
            Debug.Log($"[AsioUitgangRouter] Kon ASIO-driver '{gekozenDriver}' niet starten ({e.Message}) — " +
                      "SpeelGeluid/SpeelGeluidOpUitgangen doen niets.");
            asio?.Dispose();
            asio = null;
            IsActief = false;
            return false;
        }
    }

    public void SpeelOpKanaal(float[] samples, int kanaalEenGebaseerd)
    {
        if (!IsActief) return;

        int idx = kanaalEenGebaseerd - 1;
        if (idx < 0 || idx >= kanaalAantal)
        {
            Debug.LogWarning($"[AsioUitgangRouter] Uitgang {kanaalEenGebaseerd} bestaat niet " +
                              $"(apparaat heeft {kanaalAantal} kanalen).");
            return;
        }

        lock (vergrendel) stemmen.Add(new Stem { samples = samples, positie = 0, kanaalIndex = idx });
    }

    // Speelt hetzelfde sample-array gelijktijdig af op meerdere kanalen: alle stemmen worden
    // binnen dezelfde lock toegevoegd, zodat er geen tijdsverschil tussen de kanalen kan ontstaan.
    public void SpeelOpKanalen(float[] samples, int[] kanalenEenGebaseerd)
    {
        if (!IsActief) return;

        lock (vergrendel)
        {
            foreach (int kanaalEenGebaseerd in kanalenEenGebaseerd)
            {
                int idx = kanaalEenGebaseerd - 1;
                if (idx < 0 || idx >= kanaalAantal)
                {
                    Debug.LogWarning($"[AsioUitgangRouter] Uitgang {kanaalEenGebaseerd} bestaat niet " +
                                      $"(apparaat heeft {kanaalAantal} kanalen).");
                    continue;
                }
                stemmen.Add(new Stem { samples = samples, positie = 0, kanaalIndex = idx });
            }
        }
    }

    // Stopt alle nog spelende geluiden op het gegeven kanaal. Speelt er niets, dan gebeurt er niets.
    public void StopOpKanaal(int kanaalEenGebaseerd)
    {
        if (!IsActief) return;

        int idx = kanaalEenGebaseerd - 1;
        lock (vergrendel)
        {
            for (int i = stemmen.Count - 1; i >= 0; i--)
                if (stemmen[i].kanaalIndex == idx) stemmen.RemoveAt(i);
        }
    }

    internal void Lees(float[] buffer, int frames)
    {
        Array.Clear(buffer, 0, frames * kanaalAantal);
        lock (vergrendel)
        {
            for (int i = stemmen.Count - 1; i >= 0; i--)
            {
                Stem s = stemmen[i];
                int n = Math.Min(frames, s.samples.Length - s.positie);
                for (int f = 0; f < n; f++)
                    buffer[f * kanaalAantal + s.kanaalIndex] += s.samples[s.positie + f];
                s.positie += n;
                if (s.positie >= s.samples.Length) stemmen.RemoveAt(i);
            }
        }
    }

    public void Dispose()
    {
        if (asio == null) return;
        asio.Stop();
        asio.Dispose();
        asio = null;
        IsActief = false;
    }

    class RoutingProvider : IWaveProvider
    {
        readonly AsioUitgangRouter router;
        float[] werkBuffer = Array.Empty<float>();

        public RoutingProvider(AsioUitgangRouter router, WaveFormat format)
        {
            this.router = router;
            WaveFormat = format;
        }

        public WaveFormat WaveFormat { get; }

        public int Read(byte[] buffer, int offset, int count)
        {
            int kanalen = WaveFormat.Channels;
            int frames = count / (4 * kanalen); // 4 bytes per float-sample
            if (werkBuffer.Length < frames * kanalen) werkBuffer = new float[frames * kanalen];
            router.Lees(werkBuffer, frames);
            Buffer.BlockCopy(werkBuffer, 0, buffer, offset, frames * kanalen * 4);
            return frames * kanalen * 4;
        }
    }
}
