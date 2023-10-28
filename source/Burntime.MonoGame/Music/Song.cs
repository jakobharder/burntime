using Burntime.Platform.IO;
using Microsoft.Xna.Framework.Audio;
using NVorbis;
using System;

namespace Burntime.MonoGame.Music;

internal class LoopableSong : IDisposable
{
    public static LoopableSong? FromFileName(string fileName)
    {
        string? intro = null;
        string loop = fileName;

        if (fileName.Contains(';'))
        {
            var split = loop.Split(';');
            loop = split[1];
            intro = split[0];
        }

        var loopFile = FileSystem.GetFile(loop);
        if (loopFile is null)
            return null;

        if (intro is not null)
            return new LoopableSong(loopFile, FileSystem.GetFile(intro));

        return new LoopableSong(loopFile);
    }

    readonly byte[]? _loopBuffer;
    DynamicSoundEffectInstance? _music;

    public LoopableSong(File loop, File? intro = null)
    {
        using var ogg = new VorbisReader((intro ?? loop).Stream, false);
        _music = new DynamicSoundEffectInstance(ogg.SampleRate, ogg.Channels == 2 ? AudioChannels.Stereo : AudioChannels.Mono);
        _music.SubmitBuffer(ogg.GetBuffer());

        if (intro is not null)
        {
            using var introOgg = new VorbisReader(loop.Stream, false);

            if (introOgg.SampleRate != ogg.SampleRate || introOgg.Channels != ogg.Channels)
                return;

            _loopBuffer = introOgg.GetBuffer();
            _music.BufferNeeded += BufferNeeded;
        }
    }

    private void BufferNeeded(object? sender, EventArgs e)
    {
        _music?.SubmitBuffer(_loopBuffer);
    }

    public void Dispose()
    {
        if (_music is not null)
        {
            _music.Stop();
            if (_loopBuffer is not null)
                _music.BufferNeeded -= BufferNeeded;
            _music.Dispose();
            _music = null;
        }
    }

    public void Play() => _music?.Play();
    public void Stop() => _music?.Stop();

    public float Volume
    {
        get => _music?.Volume ?? 0;
        set { if (_music is not null) _music.Volume = value; }
    }

    public bool IsPlaying => _music?.State == SoundState.Playing;
}
