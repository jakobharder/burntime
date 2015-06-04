
#region The MIT License (MIT) - 2015 Jakob Harder
/*
 * The MIT License (MIT)
 * 
 * Copyright (c) 2015 Jakob Harder
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Threading;
using System.Diagnostics;

using SlimDX;
using SlimDX.DirectSound;
using SlimDX.Multimedia;

using OggVorbisDecoder;

using Burntime.Platform.Music;

namespace AIWar
{
    /// <summary>
    /// Summary description for GameMusic.
    /// </summary>
    public class GameMusic : IDisposable
    {
        //public WaveFormat Format
        //{
        //    get { return format; }
        //}

        public OggVorbisFileStream oggStream;

        public string Filename;
        public bool IsLooping = false;
        public int MinBufferSize = 512000;
        public int TotalWorkingLoaded = 0;

        public bool IsLoading           = true;
        public bool HasStartedPlaying   = false;
        public bool IsPaused            = true;
        public bool IsPlaybackComplete  = false;
        public bool IsEndingSoon        = false;
        private Stopwatch positionCheckTimer = new Stopwatch();

        public StreamBuffer stream;

        /// <summary>
        /// Initializes the soundbuffers
        /// </summary>
        /// <param name="FileName">The filename from which to pull the data to initialize the 
        /// soundbuffers</param>
        public GameMusic( string Filename, bool Loop )
        {
            this.Filename = Filename;

            stream = new StreamBufferOgg(Filename);
            stream.Begin();
            this.MinBufferSize = 0;
            this.TotalWorkingLoaded = 0;
        }

        #region CheckSoundBuffer
        public void CheckSoundBuffer()
        {
            if (this.IsLoading)
            {
                //if (this.ReadNextBuffer())
                //    return;

                //if (this.TotalWorkingLoaded == 0)
                //{
                //    this.Dispose();
                //    this.IsPlaybackComplete = true;
                //    return;
                //}

                this.IsLoading = false;
                //SoundBufferDescription description = new SoundBufferDescription();
                //description.Format = format;
                //description.SizeInBytes = this.TotalWorkingLoaded;
                //description.Flags = BufferFlags.ControlVolume | BufferFlags.GlobalFocus;

                ////Create the buffer.
                //buffer = new SecondarySoundBuffer(DirectSoundWrapper.Device, description);
                //buffer.Write(workingData, 0, TotalWorkingLoaded, 0, LockFlags.None);
                this.ClearStreams();
                this.PlayInternal();
            }

            if ( this.IsPaused )
                return;

            if (!stream.IsFinished)
            {
                stream.Update();
            }
            else if (stream.CurrentBuffer.Status == BufferStatus.Playing)
            {
                this.HasStartedPlaying = true;

                //if ( !this.IsEndingSoon && !this.IsLooping )
                //{
                //    if ( !positionCheckTimer.IsRunning )
                //        positionCheckTimer.Start();
                //    else if ( positionCheckTimer.ElapsedMilliseconds > 1000 )
                //    {
                //        positionCheckTimer.Stop();
                //        int position = buffer.CurrentPlayPosition;
                //        if ( position > this.TotalWorkingLoaded -
                //            ( format.AverageBytesPerSecond * 5 ) )
                //            this.IsEndingSoon = true;
                //        else
                //        {
                //            positionCheckTimer.Reset();
                //            positionCheckTimer.Start();
                //        }
                //    }
                //}
            }
            else
            {
                if (this.HasStartedPlaying)
                {
                    if (this.IsLooping)
                        this.PlayInternal();
                    else
                        this.IsPlaybackComplete = true;
                }
            }
        }
        #endregion

        #region PlayInternal
        private void PlayInternal()
        {
            if ( this.IsPaused /*|| buffer == null*/ )
                return;

            //buffer.Volume = 100;// GameForm.Instance.GameSettings.EffectiveMusicVolume;
            stream.Play();
            
            this.HasStartedPlaying = false;
        }
        #endregion

        //#region Pause
        //public void Pause()
        //{
        //    if ( this.IsPaused )
        //        return;

        //    if ( buffer != null && buffer.Status == BufferStatus.Playing )
        //        buffer.Stop();
        //    this.IsPaused = true;
        //}
        //#endregion

        //#region UnPause
        //public void UnPause()
        //{
        //    if ( !this.IsPaused || buffer == null )
        //        return;

        //    this.IsPaused = false;
        //    this.PlayInternal();
        //}
        //#endregion

        #region UpdateVolume
        public void UpdateVolume()
        {
            //if ( buffer != null )
            //    buffer.Volume = 100;// GameForm.Instance.GameSettings.EffectiveMusicVolume;
        }
        #endregion

        //#region ReadNextBuffer
        //private bool ReadNextBuffer()
        //{
        //    if ( oggStream == null ) //if already read past end of file
        //        return false;

        //    int bytesReturned   = -1;
        //    int totalBytes      = 0;

        //    try
        //    {
        //        while ( bytesReturned != 0 )
        //        {
        //            bytesReturned = oggStream.Read( workingData, TotalWorkingLoaded,
        //                workingData.Length - TotalWorkingLoaded );
        //            if ( bytesReturned >= 0 )
        //            {
        //                totalBytes += bytesReturned;
        //                TotalWorkingLoaded += bytesReturned;
        //                if ( totalBytes >= MinBufferSize )
        //                    break;
        //            }
        //        }

        //        if ( oggStream.Position >= oggStream.Length - 1 || totalBytes == 0 )
        //        {
        //            oggStream.Close();
        //            oggStream = null;
        //            return false; //we've hit the end of the file
        //        }

        //        return true;

        //    }
        //    catch ( Exception e )
        //    {
        //        //File.AppendAllText( Configuration.GetLocalApplicationDataFolder() +
        //        //     "MusicLoadExceptions.txt", DateTime.Now + Environment.NewLine +
        //        //     "-----------------------------------" +
        //        //     "Filename: " + Filename +
        //        //     ", bytesReturned: " + bytesReturned +
        //        //     ", totalBytes: " + totalBytes +
        //        //     "-----------------------------------" +
        //        //     e.ToString() + Environment.NewLine );
        //        return false;
        //    }
        //}
        //#endregion

        #region Dispose
        public void Dispose()
        {            
            //if ( this.buffer != null )
            //{
            //    try
            //    {
            //        if ( this.buffer.Status == BufferStatus.Playing )
            //            this.buffer.Stop();

            //    }
            //    catch { }
            //    buffer.Dispose();
            //    this.buffer = null;
            //}

            this.ClearStreams();
        }
        #endregion

        #region ClearStreams
        public void ClearStreams()
        {
            if ( oggStream != null )
            {
                this.oggStream.Close();
                this.oggStream.Dispose();
                this.oggStream = null;
            }
        }
        #endregion
    }
}
