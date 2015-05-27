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
