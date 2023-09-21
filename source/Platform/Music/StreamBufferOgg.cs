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
