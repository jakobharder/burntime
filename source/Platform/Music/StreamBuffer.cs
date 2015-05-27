/*
 *  Burntime Platform
 *  Copyright (C) 2009
 *
 *  This program is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with this program.  If not, see <http://www.gnu.org/licenses/>.
 * 
 *  authors: 
 *    Juernjakob Harder (yn.harada@gmail.com)
 * 
*/

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
