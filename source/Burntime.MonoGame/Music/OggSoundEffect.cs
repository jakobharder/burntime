using Microsoft.Xna.Framework.Audio;
using NVorbis;
using System;
using System.IO;

namespace Burntime.MonoGame;

public static class OggSoundEffect
{
    const float FadeSeconds = 0.2f;

    public static SoundEffect FromStream(Stream fileStream)
    {
        using var reader = new VorbisReader(fileStream, false);

        var sampleCount = (int)reader.TotalSamples;
        var soundData = new float[sampleCount * reader.Channels];
        _ = reader.ReadSamples(soundData, 0, sampleCount * reader.Channels);

        var byteData = new byte[sampleCount * 2 * reader.Channels];
        CastBuffer(soundData, byteData, sampleCount * reader.Channels);

        return new SoundEffect(byteData, reader.SampleRate, reader.Channels == 2 ? AudioChannels.Stereo : AudioChannels.Mono);
    }

    public static byte[] GetBuffer(this VorbisReader reader)
    {
        var sampleCount = (int)reader.TotalSamples;
        var soundData = new float[sampleCount * reader.Channels];
        _ = reader.ReadSamples(soundData, 0, sampleCount * reader.Channels);

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

    public static void ApplyFadeOut(byte[] buffer, int sampleRate, int channels)
    {
        int frameCount = buffer.Length / (sizeof(short) * channels);
        int fadeFrames = Math.Min((int)(sampleRate * FadeSeconds), frameCount);
        for (int frame = 0; frame < fadeFrames; frame++)
        {
            float factor = (float)frame / fadeFrames;
            ScaleFrame(buffer, frameCount - frame - 1, channels, factor);
        }
    }

    static void ScaleFrame(byte[] buffer, int frame, int channels, float factor)
    {
        int offset = frame * channels * sizeof(short);
        for (int channel = 0; channel < channels; channel++, offset += sizeof(short))
        {
            short sample = (short)(buffer[offset] | buffer[offset + 1] << 8);
            sample = (short)(sample * factor);
            buffer[offset] = (byte)sample;
            buffer[offset + 1] = (byte)(sample >> 8);
        }
    }
}
