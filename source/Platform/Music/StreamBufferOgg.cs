
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

using OggVorbisDecoder;

namespace Burntime.Platform.Music
{
    public class StreamBufferOgg : StreamBuffer, IDisposable
    {
        OggVorbisFileStream ogg;

        public StreamBufferOgg(string fileName)
        {
            ogg = new OggVorbisFileStream(fileName);

            InitializeBuffer(ogg.Info.Rate, ogg.Info.Channels);
        }

        void IDisposable.Dispose()
        {
            if (sbuffer[0] != null)
            {
                try
                {
                    sbuffer[0].Stop();
                }catch {}
                sbuffer[0].Dispose();
                sbuffer[0] = null;
            }

            if (sbuffer[1] != null)
            {
                try
                {
                    sbuffer[1].Stop();
                }
                catch { }
                sbuffer[1].Dispose();
                sbuffer[1] = null;
            }

            if (ogg != null)
            {
                ogg.Close();
                ogg.Dispose();
                ogg = null;
            }
        }

        public override bool FillBuffer(ref byte[] buffer)
        {
            if (ogg == null)
                return false;

            int read = 0;

            while (read < buffer.Length)
            {
                int bytes = ogg.Read(buffer, read, buffer.Length - read);
                if (bytes == 0)
                {
                    ogg.Close();
                    ogg = null;
                    return false;
                }

                read += bytes;
            }

            return true;
        }
    }
}
