using System;

namespace Burntime.Data.BurnGfx
{
    public class CorruptDataException : Exception
    {
    }

    public class FileMissingException : Exception
    {
        string fileName;

        public FileMissingException(string fileName)
        {
            this.fileName = fileName;
        }

        public override string Message
        {
            get
            {
                return "missing burngfx file: " + fileName;
            }
        }
    }
}
