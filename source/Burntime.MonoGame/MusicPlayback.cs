using Burntime.Platform;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Media;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Burntime.MonoGame
{
    public sealed class MusicPlayback : IMusic
    {
        SoundEffect _effect;
        SoundEffectInstance _music;
        List<string> playList = new List<string>();
        Thread musicThread;

        bool stop;
        bool enabled;
        bool playOnce;

        public bool Enabled
        {
            get { return enabled; }
            set { enabled = value; }
        }

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
            playList.Clear();
            Stop();
        }

        public void AddPlayList(string fileName)
        {
            if (!enabled)
                return;

            playList.Add(fileName);
        }

        public void Play(string fileName)
        {
            if (!enabled)
                return;

            playOnce = false;
            playList.Clear();
            Stop();
            playList.Add(fileName);
        }

        public void PlayOnce(string fileName)
        {
            if (!enabled)
                return;

            playList.Clear();
            Stop();
            playList.Add(fileName);
            playOnce = true;
        }

        public void Stop()
        {
            playList.Clear();

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
            musicThread = new Thread(new ThreadStart(MusicThread));
            musicThread.Start();
        }

        public void StopThread()
        {
            Stop();

            if (musicThread != null)
            {
                stop = true;
                musicThread.Join();
                musicThread = null;
            }
        }

        private string GetNextTitle()
        {
            if (playList.Count == 0)
                return null;

            string next = playList[0];
            playList.RemoveAt(0);
            if (!playOnce)
                playList.Add(next);

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

                        string next = GetNextTitle();
                        if (next == null)
                            continue;

                        _effect = OggSoundEffect.FromStream(Burntime.Platform.IO.FileSystem.GetFile(next).Stream);
                        _music = _effect.CreateInstance();
                        _music.Volume = _volume;
                        _music.Play();
                    }
                    else
                    {
                        if (_music.State == SoundState.Stopped)
                        {
                            _music.Dispose();
                            _effect.Dispose();
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
                _effect?.Dispose();
            }

            //SlimDXOggStreamingSample.DirectSoundWrapper.Device.Dispose();
        }
    }
}
