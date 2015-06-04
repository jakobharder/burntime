
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
using System.Threading;

using AIWar;
using SlimDX;
using SlimDX.DirectSound;
using SlimDX.Multimedia;

namespace Burntime.Platform.Music
{
    public abstract class StreamBuffer
    {
        public byte[] buffer;
        public SecondarySoundBuffer[] sbuffer;
        public int current;
        public WaveFormat format;

        public int BufferSize;

        protected bool finished;
        NotificationPosition pos;

        public bool IsFinished
        {
            get { return finished; }
        }

        public SecondarySoundBuffer CurrentBuffer
        {
            get { return sbuffer[current]; }
        }

        public void InitializeBuffer(int rate, int channels)
        {
            format = new WaveFormat();
            format.FormatTag = WaveFormatTag.Pcm;
            format.SamplesPerSecond = rate;
            format.BitsPerSample = 16;
            format.Channels = (short)channels;
            format.BlockAlignment = (short)(format.Channels * (format.BitsPerSample / 8));
            format.AverageBytesPerSecond = format.SamplesPerSecond * format.BlockAlignment;

            BufferSize = format.AverageBytesPerSecond;
            BufferSize += 512 - (BufferSize % 512);
            
            buffer = new byte[BufferSize];

            SoundBufferDescription description = new SoundBufferDescription();
            description.Format = format;
            description.SizeInBytes = BufferSize;
            description.Flags = BufferFlags.ControlVolume | BufferFlags.GlobalFocus | BufferFlags.ControlPositionNotify;

            sbuffer = new SecondarySoundBuffer[2];
            sbuffer[0] = new SecondarySoundBuffer(DirectSoundWrapper.Device, description);
            sbuffer[1] = new SecondarySoundBuffer(DirectSoundWrapper.Device, description);
        }

        public void Begin()
        {
            current = 0;
            finished = false;
            FillBuffer(ref buffer);
            sbuffer[0].Write(buffer, 0, BufferSize, 0, LockFlags.None);
        }

        public void SwapBuffers()
        {
            current = (current + 1) % 2;
        }

        public void LoadNextBuffer()
        {
            finished = !FillBuffer(ref buffer);
            int next = (current + 1) % 2;
            sbuffer[next].Write(buffer, 0, BufferSize, 0, LockFlags.EntireBuffer);
        }

        public abstract bool FillBuffer(ref byte[] buffer);

        public void Play()
        {
            pos = new NotificationPosition();
            pos.Offset = BufferSize - 1;
            pos.Event = new AutoResetEvent(false);

            CurrentBuffer.SetNotificationPositions(new NotificationPosition[] { pos });

            CurrentBuffer.Play(0, PlayFlags.None);
        }

        public void Update()
        {
            pos.Event.WaitOne();

            SwapBuffers();
            Play();
            LoadNextBuffer();
        }
    }
}
