/*
 * Original MDX-based code by "Dr Gary" (http://www.codeproject.com/KB/audio-video/MdxSoundEx.aspx)
 * Adapted to SlimDX by Christopher M. Park (http://www.christophermpark.com/)
 * Ogg Vorbis Decoder by Atachiants Roman (http://oggvorbisdecoder.codeplex.com/)
 * SlimDX by the SlimDX team (http://slimdx.org/)
 * 6/17/2009
 * */

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Threading;
using System.Diagnostics;

using SlimDX;
using SlimDX.DirectSound;
using SlimDX.Multimedia;
using System.Windows.Forms;

using OggVorbisDecoder;

namespace SlimDXOggStreamingSample
{
    public sealed class DirectSoundWrapper
    {
        #region Public/Internal Properties
        public static DirectSound Device
        {
            get { return device; }
        }
        #endregion

        #region Private Members
        private static DirectSound device;
        #endregion

        /// <summary>
        /// Initialize DirectSound for the specified Window
        /// </summary>
        /// <param name="HWnd">Handle of the window for which DirectSound is to be initialized</param>
        public static void Initialize(Control Parent)
        {
            device = new DirectSound();
            device.SetCooperativeLevel(Parent.Handle, CooperativeLevel.Priority);
            device.IsDefaultPool = false;
        }

        /// <summary>
        /// Create a playable sound buffer from a WAV file
        /// </summary>
        /// <param name="Filename">The WAV file to load</param>
        /// <returns>Playable sound buffer</returns>
        public static SecondarySoundBuffer CreateSoundBufferFromWave(string Filename)
        {
            // Load wave file
            using (WaveStream waveFile = new WaveStream(Filename))
            {
                SoundBufferDescription description = new SoundBufferDescription();
                description.Format = waveFile.Format;
                description.SizeInBytes = (int)waveFile.Length;
                description.Flags = BufferFlags.ControlVolume | BufferFlags.GlobalFocus;

                // Create the buffer.
                SecondarySoundBuffer buffer = new SecondarySoundBuffer(device, description);
                byte[] data = new byte[description.SizeInBytes];
                waveFile.Read(data, 0, (int)waveFile.Length);
                buffer.Write(data, 0, LockFlags.None);
                return buffer;
            }
        }

        public static SecondarySoundBuffer CreateSoundBufferFromOgg(string Filename)
        {
            // Load ogg file
            using (OggVorbisFileStream oggStream = new OggVorbisFileStream(Filename))
            {
                using (MemoryStream memStream = new MemoryStream())
                {
                    byte[] tempData = new byte[512];

                    int bytesReturned = -1;
                    int bytesRead = 0;
                    while (bytesReturned != 0)
                    {
                        bytesReturned = oggStream.Read(tempData, 0, tempData.Length);
                        memStream.Write(tempData, 0, bytesReturned);
                        bytesRead += bytesReturned;
                    }

                    WaveFormat format = new WaveFormat();
                    format.FormatTag = WaveFormatTag.Pcm;
                    format.SamplesPerSecond = oggStream.Info.Rate;
                    format.BitsPerSample = 16;
                    format.Channels = (short)oggStream.Info.Channels;
                    format.BlockAlignment = (short)(format.Channels * (format.BitsPerSample / 8));
                    format.AverageBytesPerSecond = format.SamplesPerSecond * format.BlockAlignment;

                    SoundBufferDescription description = new SoundBufferDescription();
                    description.Format = format;
                    description.SizeInBytes = (int)memStream.Length;
                    description.Flags = BufferFlags.ControlVolume | BufferFlags.GlobalFocus;

                    // Create the buffer.
                    SecondarySoundBuffer buffer = new SecondarySoundBuffer(device, description);
                    byte[] data = memStream.ToArray();
                    buffer.Write(data, 0, LockFlags.None);

                    return buffer;
                }
            }
        }
    }
}

namespace SlimDXOggStreamingSample
{
    /// <summary>
    /// Summary description for MusicPlayer.
    /// </summary>
    public class MusicPlayer : IDisposable
    {
        public WaveFormat Format
        {
            get { return format; }
        }

        public OggVorbisFileStream oggStream;

        // A note on buffer size: I assume CD audio; i.e., 16-bit stereo frames at 44100 frames/sec.
        // 64k * 4 bytes per frame is about 1.5 sec. There's about 370 msec between notification events.
        private const int StreamBufferSize = 262144;
        private const int NumberOfSectorsInBuffer = 4;

        private NotificationPosition[] NotificationPositionArray =
            new NotificationPosition[NumberOfSectorsInBuffer];

        private NotificationPosition[] EndNotificationPosition = new NotificationPosition[1];
        private int PredictedEndIndex;

        private AutoResetEvent NotificationEvent;

        private const int SectorSize = StreamBufferSize / NumberOfSectorsInBuffer;
        private byte[] TransferBuffer = new byte[StreamBufferSize];
        private Thread DataTransferThread;

        private bool MoreWaveDataAvailable;
        private bool AbortDataTransfer;
        private int DataBytesSoFar; // data bytes transferred to the SecondaryBuffer so far
        private int SecondaryBufferWritePosition; // byte index to start next Write()
        private int NumberOfDataSectorsTransferred;

        public BufferPlayState State;

        public WaveFormat format;
        public string Filename;
        public SecondarySoundBuffer buffer = null;

        public int FullLength = 0;
        public int Volume = 0;

        #region PercentComplete
        public double PercentComplete
        {
            get
            {
                if (buffer == null)
                    return 0;

                return ((double)this.DataBytesSoFar / (double)this.FullLength) * 100.0;
            }
        }
        #endregion

        /// <summary>
        /// Initializes the soundbuffers
        /// </summary>
        /// <param name="FileName">The filename from which to pull the data to initialize the 
        /// soundbuffers</param>
        public MusicPlayer(Burntime.Platform.IO.File file)
        {
            this.Filename = file.Name;
            Burntime.Platform.Log.Debug("prepare to play " + Filename);
            oggStream = new OggVorbisFileStream(file.Stream);
            FullLength = (int)oggStream.Length;

            format = new WaveFormat();
            format.FormatTag = WaveFormatTag.Pcm;
            format.SamplesPerSecond = oggStream.Info.Rate;
            format.BitsPerSample = 16;
            format.Channels = (short)oggStream.Info.Channels;
            format.BlockAlignment = (short)(format.Channels * (format.BitsPerSample / 8));
            format.AverageBytesPerSecond = format.SamplesPerSecond * format.BlockAlignment;

            SoundBufferDescription description = new SoundBufferDescription();
            description.Format = format;
            description.SizeInBytes = StreamBufferSize;

            //if ( GameForm.Instance.GameSettings.ForceSoftwareMusicMixing )
            description.Flags = BufferFlags.ControlVolume | BufferFlags.GlobalFocus |
                BufferFlags.ControlPositionNotify | BufferFlags.GetCurrentPosition2 |
                BufferFlags.Software;

            //Create the buffer.
            buffer = new SecondarySoundBuffer(DirectSoundWrapper.Device, description);
            SecondaryBufferWritePosition = 0;

            this.UpdateVolume();

            // Create a notification Event object, to fire at each notify position
            NotificationEvent = new AutoResetEvent(false);

            // Preset as much of the EndNotificationPosition array as possible to avoid doing it 
            // in real-time.
            EndNotificationPosition[0].Event = NotificationEvent;
            PredictedEndIndex = (int)(oggStream.Length % StreamBufferSize); //[bytes]

            // ready to go:
            MoreWaveDataAvailable = true;
            State = BufferPlayState.Idle;

            Play();
        }

        #region Notifications Methods
        /// <summary>
        /// Create & initialize the Notifier object and notification positions in the 
        /// streaming SecondaryBuffer.  These fill positions persist through multiple
        /// Play/Pause events; however, they are replaced by the single endPosition 
        /// at end of data, and are reloaded by Play() if Stopped (position == 0).
        /// </summary>
        /// <param name="numberOfSectors">number of equal-sized sectors in the buffer</param>
        private void SetFillNotifications(int numberOfSectors)
        {
            // Set up the fill-notification positions at last byte of each sector.
            // All use the same event, in contrast to recipe in DX9.0 SDK Aug 2005 titled 
            // "DirectSound Buffers | Using Streaming Buffers" 
            for (int i = 0; i < numberOfSectors; i++)
            {
                NotificationPositionArray[i].Offset = (i + 1) * SectorSize - 1;
                NotificationPositionArray[i].Event = NotificationEvent;
            }

            // set the buffer to fire events at the notification positions
            buffer.SetNotificationPositions(NotificationPositionArray);
        }

        private void SetEndNotification(int indexInBuffer)
        {
            EndNotificationPosition[0].Offset = indexInBuffer;

            // do this quickly to avoid possible sound glitch
            buffer.Stop();
            buffer.SetNotificationPositions(EndNotificationPosition);
            buffer.Play(0, PlayFlags.Looping);
        }
        #endregion

        #region Data Transfer Thread code

        /// <summary>
        /// Instantiate and start the server thread.  It will catch events from the Notify object, 
        /// and call TransferBlockToSecondaryBuffer() each time a Notification position is crossed.
        /// Thread terminates at end of playback, or upon Stop event.
        /// </summary>
        private void CreateDataTransferThread()
        {
            // No thread should exist yet.
            if (DataTransferThread != null)
                throw new Exception("CreateDataTransferThread() saw thread non-null.");

            AbortDataTransfer = false;
            MoreWaveDataAvailable = (oggStream.Length > DataBytesSoFar);
            NumberOfDataSectorsTransferred = 0;

            // Create a thread to monitor the notify events.
            DataTransferThread = new Thread(new ThreadStart(DataTransferActivity));
            DataTransferThread.Name = "DataTransferThread";
            DataTransferThread.Priority = ThreadPriority.Highest;
            DataTransferThread.IsBackground = true;
            DataTransferThread.Start();

            // thread will wait here for the first notification event
        }

        /// <summary>
        /// The DataTransferThread's work function.  Returns, and thread ends,
        /// when wave data transfer has ended and buffer has played out, or 
        /// when Stop event does EndDataTransferThread().
        /// </summary>
        private void DataTransferActivity()
        {
            // load all sectors
            for (int i = 0; i < NumberOfSectorsInBuffer - 1; i++)
            {
                // get a block of bytes, possibly including zero-fill
                TransferBlockToSecondaryBuffer();
            }

            buffer.CurrentPlayPosition = 0;
            this.UpdateVolume();
            buffer.Play(0, PlayFlags.Looping);
            State = BufferPlayState.Playing;

            int endWaveSector = 0;
            while (MoreWaveDataAvailable)
            {
                if (AbortDataTransfer)
                {
                    return;
                }
                //wait here for a notification event
                NotificationEvent.WaitOne(Timeout.Infinite, true);
                endWaveSector = SecondaryBufferWritePosition / SectorSize;
                MoreWaveDataAvailable = TransferBlockToSecondaryBuffer();
            }

            // Fill one more sector with silence, to avoid playing old data during the
            // time between end-event-notification and SecondaryBuffer.Stop().
            Array.Clear(TransferBuffer, 0, TransferBuffer.Length);
            NotificationEvent.WaitOne(Timeout.Infinite, true);
            int silentSector;
            silentSector = SecondaryBufferWritePosition / SectorSize;
            WriteBlockToSecondaryBuffer();

            // No more blocks to write: Remove fill-notify points, and mark end of data.
            int dataEndInBuffer = DataBytesSoFar % StreamBufferSize;
            SetEndNotification(dataEndInBuffer);

            bool notificationWithinEndSectors = false;	// end of data or the silent sector
            // Wait for play to reach the end
            while (!notificationWithinEndSectors)
            {
                NotificationEvent.WaitOne(Timeout.Infinite, true);

                int currentPlayPos = buffer.CurrentPlayPosition;
                int currentPlaySector = currentPlayPos / SectorSize;

                notificationWithinEndSectors = currentPlaySector == endWaveSector
                                                | currentPlaySector == silentSector;
            }
            buffer.Stop();
            State = BufferPlayState.Complete;
        }

        static int iterationCount = 0;
        /// <summary>
        /// Reads and transfers a block of data from the file, beginning at m_dataBytesSoFar,
        /// into the transfer buffer; then writes to the secondary buffer, beginning at 
        /// m_secondaryBufferWritePosition.
        /// </summary>
        /// <returns>true if the whole block is wave data, false if end of data with zero-fill</returns>
        private bool TransferBlockToSecondaryBuffer()
        {
            // If the file has run out of wave data, the block was zero-filled to the end. 
            int amountToRead = ((int)oggStream.Length - DataBytesSoFar);
            if (amountToRead > SectorSize)
                amountToRead = SectorSize;

            iterationCount++;

            int currentAmount = oggStream.Read(TransferBuffer, 0, SectorSize);
            int dataBytesThisTime = currentAmount;

            while (currentAmount > 0 && dataBytesThisTime < SectorSize)
            {
                currentAmount = oggStream.Read(TransferBuffer, dataBytesThisTime, SectorSize - dataBytesThisTime);
                dataBytesThisTime += currentAmount;
            }

            for (int j = dataBytesThisTime; j < SectorSize; j++)
                TransferBuffer[j] = 0;

            DataBytesSoFar += SectorSize;
            WriteBlockToSecondaryBuffer();
            NumberOfDataSectorsTransferred++;

            if (dataBytesThisTime < SectorSize)
            {
                return false;
            }
            return true;
        }

        // Write a block from the Transfer Buffer to the Secondary Buffer, 
        // possibly including zero-fill.
        private void WriteBlockToSecondaryBuffer()
        {
            buffer.Write(TransferBuffer, 0, SectorSize, SecondaryBufferWritePosition, LockFlags.None);
            SecondaryBufferWritePosition += SectorSize;
            SecondaryBufferWritePosition %= StreamBufferSize;
        }

        // Safely end the DataTransferThread if it is alive.  Sets ref to null.
        // Called from Play(), Stop(), and Dispose()ie Cleanup()
        private void EndDataTransferThread()
        {
            if (DataTransferThread == null)
            {
                return;
            }
            if (DataTransferThread.IsAlive)
            {
                AbortDataTransfer = true;
                DataTransferThread.Join();
            }
            DataTransferThread = null;
        }
        #endregion

        #region Play
        /// <summary>
        /// If in Idle state, attempts to play from the beginning.  
        /// If Paused, resumes play from current position.  Otherwise has no effect.
        /// </summary>
        public void Play()
        {
            Burntime.Platform.Log.Debug("play " + Filename);

            if (State == BufferPlayState.Paused && DataTransferThread != null &&
                DataTransferThread.IsAlive)
            {
                UnPause(); // toggle to unpaused state
                return;
            }
            else if (State == BufferPlayState.Idle)
            {
                EndDataTransferThread();
                if (DataTransferThread != null)
                    throw new Exception("DataTransferThread != null");
                SetFillNotifications(NumberOfSectorsInBuffer);	// includes clearing end notify
                DataBytesSoFar = 0;
                SecondaryBufferWritePosition = 0;
                buffer.CurrentPlayPosition = 0;
                MoreWaveDataAvailable = (oggStream.Length > DataBytesSoFar);
                CreateDataTransferThread();
                return;
            }
        }
        #endregion

        #region Pause
        /// <summary>
        /// Pause playing the sound file from Playing state, or resume playing from Paused state.
        /// If state is not Playing nor Paused, has no effect.
        /// </summary>
        /// <returns>Buffer.PlayPosition at time of call [bytes]</returns>
        public int Pause()
        {
            int playPosition = buffer.CurrentPlayPosition;
            if (State == BufferPlayState.Playing)
            {
                buffer.Stop();
                State = BufferPlayState.Paused;
            }
            //else if ( State == BufferPlayState.Paused )
            //{
            //    buffer.Play( 0, PlayFlags.Looping );
            //    State = BufferPlayState.Playing;
            //}
            return playPosition;
        }
        #endregion

        #region UnPause
        public void UnPause()
        {
            if (State == BufferPlayState.Paused)
            {
                buffer.Play(0, PlayFlags.Looping);
                State = BufferPlayState.Playing;
            }
        }
        #endregion

        #region Stop
        public void Stop()
        {
            Burntime.Platform.Log.Debug("stop " + Filename);

            //UnPause();
            EndDataTransferThread();
            buffer.Stop();
            State = BufferPlayState.Idle;
            return;
        }
        #endregion

        #region UpdateVolume
        public void UpdateVolume()
        {
            if (buffer != null)
            {
                if (Volume == 0)
                    buffer.Volume = (int)SlimDX.DirectSound.Volume.Minimum;
                else if (Volume == 100)
                    buffer.Volume = 0;
                else
                    buffer.Volume = (int)((20.0 * System.Math.Log10(Volume / 100.0)) * 100.0);
            }
        }
        #endregion

        #region Dispose
        public void Dispose()
        {
            try
            {
                Stop();
            }
            catch { }

            if (this.buffer != null)
            {
                try
                {
                    if (this.buffer.Status == BufferStatus.Playing)
                        this.buffer.Stop();

                }
                catch { }
                buffer.Dispose();
                this.buffer = null;
            }

            if (NotificationEvent != null)
            {
                NotificationEvent.Close();
                NotificationEvent = null;
            }

            this.ClearStreams();
        }
        #endregion

        #region ClearStreams
        public void ClearStreams()
        {
            if (oggStream != null)
            {
                this.oggStream.Close();
                this.oggStream.Dispose();
                this.oggStream = null;
            }
        }
        #endregion
    }

    public enum BufferPlayState
    {
        Idle,
        Paused,
        Playing,
        Complete
    }
}
