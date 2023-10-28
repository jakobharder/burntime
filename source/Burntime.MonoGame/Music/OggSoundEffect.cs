using Microsoft.Xna.Framework.Audio;
using NVorbis;
using System;
using System.IO;

namespace Burntime.MonoGame;

public static class OggSoundEffect
{
    public static SoundEffect FromStream(Stream fileStream)
    {
        using (var reader = new VorbisReader(fileStream, false))
        {
            var sampleCount = (int)reader.TotalSamples;
            var soundData = new float[sampleCount * reader.Channels];
            int readCount = reader.ReadSamples(soundData, 0, sampleCount * reader.Channels);

            var byteData = new byte[sampleCount * 2 * reader.Channels];
            CastBuffer(soundData, byteData, sampleCount * reader.Channels);

            return new SoundEffect(byteData, reader.SampleRate, reader.Channels == 2 ? AudioChannels.Stereo : AudioChannels.Mono);
        }
    }

    public static byte[] GetBuffer(this VorbisReader reader)
    {
        var sampleCount = (int)reader.TotalSamples;
        var soundData = new float[sampleCount * reader.Channels];
        int readCount = reader.ReadSamples(soundData, 0, sampleCount * reader.Channels);

        var byteData = new byte[sampleCount * 2 * reader.Channels];
        CastBuffer(soundData, byteData, sampleCount * reader.Channels);

        return byteData;
    }

    static void CastBuffer(float[] inBuffer, byte[] outBuffer, int length)
    {
        for (int i = 0; i < length; i++)
        {
            var temp = (int)(32767f * inBuffer[i]);
            if (temp > short.MaxValue) temp = short.MaxValue;
            else if (temp < short.MinValue) temp = short.MinValue;

            var bytes = BitConverter.GetBytes(temp);
            outBuffer[i * 2] = bytes[0];
            outBuffer[i * 2 + 1] = bytes[1];
        }
    }
}