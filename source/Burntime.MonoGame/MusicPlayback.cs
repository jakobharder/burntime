using Burntime.Platform;
using System.Collections.Generic;
using System.Threading;

namespace Burntime.MonoGame;

public sealed class MusicPlayback : IMusic
{
    Music.LoopableSong? _music;
    List<string> _playlist = new();
    bool _repeat = false;
    Thread? _musicThread;

    bool stop;

    public bool Enabled { get; set; }

    bool isMuted = true;
    public bool IsMuted
    {
        get { return isMuted; }
        set
        {
            isMuted = value;
            Volume = _volume;
        }
    }

    float _volume = 0;
    public float Volume
    {
        get { if (_music == null) return 0; return _music.Volume; }
        set
        {
            _volume = value;
            if (_music != null)
                _music.Volume = isMuted ? 0 : value;
        }
    }

    public void ClearPlayList()
    {
        _playlist.Clear();
        Stop();
    }

    public void AddPlayList(string fileName)
    {
        if (!Enabled)
            return;

        _playlist.Add(fileName);
    }

    public void Play(string fileName, bool loop = true)
    {
        if (!Enabled)
            return;

        _playlist.Clear();
        Stop();
        _repeat = loop;
        _playlist.Add(fileName);
    }

    public void PlayOnce(string fileName) => Play(fileName, false);

    public void Stop()
    {
        _playlist.Clear();

        lock (this)
        {
            if (_music != null)
            {
                _music.Stop();
                _music.Dispose();
                _music = null;
            }
        }
    }

    public void RunThread()
    {
        stop = false;
        _musicThread = new Thread(new ThreadStart(MusicThread));
        _musicThread.Start();
    }

    public void StopThread()
    {
        Stop();

        if (_musicThread != null)
        {
            stop = true;
            _musicThread.Join();
            _musicThread = null;
        }
    }

    private string? GetNextTitle()
    {
        if (_playlist.Count == 0)
            return null;

        var next = _playlist[0];
        _playlist.RemoveAt(0);
        if (_repeat)
            _playlist.Add(next);

        return next;
    }

    private void MusicThread()
    {
        int sleep = 0;

        while (!stop)
        {
            if (sleep > 0)
                Thread.Sleep(sleep);

            lock (this)
            {
                if (_music == null)
                {
                    sleep = 200;

                    string? next = GetNextTitle();
                    if (next == null)
                        continue;

                    _music = Music.LoopableSong.FromFileName(next);
                    if (_music is null)
                        continue;

                    _music.Volume = _volume;
                    _music.Play();
                }
                else
                {
                    if (!_music.IsPlaying)
                    {
                        _music.Dispose();
                        _music = null;
                        sleep = 0;
                    }
                    else
                        sleep = 50;
                }
            }
        }

        lock (this)
        {
            _music?.Dispose();
        }
    }
}
