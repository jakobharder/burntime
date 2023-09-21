﻿using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;

namespace Burntime.Platform.Music
{
    public sealed class MusicPlayback
    {
        SlimDXOggStreamingSample.MusicPlayer music;

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

        bool isMuted = false;
        public bool IsMuted
        {
            get { return isMuted; }
            set 
            { 
                isMuted = value;
                Volume = volume;
            }
        }

        float volume;
        public float Volume
        {
            get { if (music == null) return 0; return music.Volume * 0.01f; }
            set
            {
                volume = value;
                if (music != null)
                {
                    int old = music.Volume;
                    if (!isMuted)
                        music.Volume = (int)(value * 100);
                    else
                        music.Volume = 0;
                    if (old != music.Volume)
                        music.UpdateVolume();
                }
            }
        }

        public MusicPlayback(Control form)
        {
            //AIWar.DirectSoundWrapper.Initialize(form);
            SlimDXOggStreamingSample.DirectSoundWrapper.Initialize(form);
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
                if (music != null)
                {
                    music.Stop();
                    music.Dispose();
                    music = null;
                }
            }
        }

        internal void RunThread()
        {
            stop = false;
            musicThread = new Thread(new ThreadStart(MusicThread));
            musicThread.Start();
        }

        internal void StopThread()
        {
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
                    if (music == null)
                    {
                        sleep = 200;

                        string next = GetNextTitle();
                        if (next == null)
                            continue;

                        music = new SlimDXOggStreamingSample.MusicPlayer(Burntime.Platform.IO.FileSystem.GetFile(next));
                    }
                    else
                    {
                        if (music.State == SlimDXOggStreamingSample.BufferPlayState.Complete)
                        {
                            music.Dispose();
                            music = null;
                            sleep = 0;
                        }
                        else
                            sleep = 50;
                    }
                }
            }

            lock (this)
            {
                if (music != null)
                    music.Dispose();
            }

            SlimDXOggStreamingSample.DirectSoundWrapper.Device.Dispose();
        }
    }
}
