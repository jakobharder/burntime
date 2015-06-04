
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
using System.Windows.Forms;
using System.IO;

using SlimDX;
using SlimDX.Multimedia;
using SlimDX.DirectSound;

using OggVorbisDecoder;

namespace AIWar
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
        public static void Initialize( Control Parent )
        {
            device = new DirectSound();
            device.SetCooperativeLevel( Parent.Handle, CooperativeLevel.Priority );
            device.IsDefaultPool = false;
        }

        /// <summary>
        /// Create a playable sound buffer from a WAV file
        /// </summary>
        /// <param name="Filename">The WAV file to load</param>
        /// <returns>Playable sound buffer</returns>
        public static SecondarySoundBuffer CreateSoundBufferFromWave( string Filename )
        {
            // Load wave file
            using ( WaveStream waveFile = new WaveStream( Filename ) )
            {
                SoundBufferDescription description = new SoundBufferDescription();
                description.Format = waveFile.Format;
                description.SizeInBytes = (int)waveFile.Length;
                description.Flags = BufferFlags.ControlVolume | BufferFlags.GlobalFocus;

                // Create the buffer.
                SecondarySoundBuffer buffer = new SecondarySoundBuffer( device, description );
                byte[] data = new byte[description.SizeInBytes];
                waveFile.Read( data, 0, (int)waveFile.Length );
                buffer.Write( data, 0, LockFlags.None );
                return buffer;
            }
        }

        public static SecondarySoundBuffer CreateSoundBufferFromOgg( string Filename )
        {
            // Load ogg file
            using ( OggVorbisFileStream oggStream = new OggVorbisFileStream( Filename ) )
            {
                using ( MemoryStream memStream  = new MemoryStream() )
                {
                    byte[] tempData         = new byte[512];

                    int bytesReturned = -1;
                    int bytesRead = 0;
                    while ( bytesReturned != 0 )
                    {
                        bytesReturned = oggStream.Read( tempData, 0, tempData.Length );
                        memStream.Write( tempData, 0, bytesReturned );
                        bytesRead += bytesReturned;
                    }

                    WaveFormat format               = new WaveFormat();
                    format.FormatTag                = WaveFormatTag.Pcm;
                    format.SamplesPerSecond         = oggStream.Info.Rate;
                    format.BitsPerSample            = 16;
                    format.Channels                 = (short)oggStream.Info.Channels;
                    format.BlockAlignment           = (short)( format.Channels * ( format.BitsPerSample / 8 ) );
                    format.AverageBytesPerSecond    = format.SamplesPerSecond * format.BlockAlignment;

                    SoundBufferDescription description = new SoundBufferDescription();
                    description.Format = format;
                    description.SizeInBytes = (int)memStream.Length;
                    description.Flags = BufferFlags.ControlVolume | BufferFlags.GlobalFocus;

                    // Create the buffer.
                    SecondarySoundBuffer buffer = new SecondarySoundBuffer( device, description );
                    byte[] data = memStream.ToArray();
                    buffer.Write( data, 0, LockFlags.None );

                    return buffer;
                }
            }
        }
    }
}
