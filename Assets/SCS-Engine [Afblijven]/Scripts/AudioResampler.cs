using System;
using System.Collections.Generic;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

// Herbemonstert audio wanneer de sample rate van een AudioClip niet overeenkomt met
// de sample rate waarop de ASIO-driver staat ingesteld (bv. clip op 44100Hz, Scarlett op 48000Hz).
public static class AudioResampler
{
    public static float[] Resample(float[] samples, int bronSampleRate, int doelSampleRate)
    {
        if (bronSampleRate == doelSampleRate) return samples;

        var bronFormat = WaveFormat.CreateIeeeFloatWaveFormat(bronSampleRate, 1);
        var bronProvider = new RawSampleProvider(samples, bronFormat);
        var resampler = new WdlResamplingSampleProvider(bronProvider, doelSampleRate);

        int geschatteCapaciteit = (int)((long)samples.Length * doelSampleRate / bronSampleRate) + 64;
        var uitvoer = new List<float>(geschatteCapaciteit);
        float[] buffer = new float[1024];
        int gelezen;
        while ((gelezen = resampler.Read(buffer, 0, buffer.Length)) > 0)
            for (int i = 0; i < gelezen; i++) uitvoer.Add(buffer[i]);
        return uitvoer.ToArray();
    }

    class RawSampleProvider : ISampleProvider
    {
        readonly float[] data;
        int positie;

        public RawSampleProvider(float[] data, WaveFormat format)
        {
            this.data = data;
            WaveFormat = format;
        }

        public WaveFormat WaveFormat { get; }

        public int Read(float[] buffer, int offset, int count)
        {
            int n = Math.Min(count, data.Length - positie);
            if (n <= 0) return 0;
            Array.Copy(data, positie, buffer, offset, n);
            positie += n;
            return n;
        }
    }
}
