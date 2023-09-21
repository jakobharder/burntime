using System;
using System.Collections.Generic;
using System.Text;

namespace Burntime.Platform.Resource
{
    public class ResourceID
    {
        String id;
        String format;
        String file;
        String custom;
        int index;
        int endIndex;
        bool indexProvided;

        public String Format
        {
            get { return format; }
        }

        public String File
        {
            get { return file; }
        }

        public int Index
        {
            get { return index; }
        }

        public String Custom
        {
            get { return custom; }
        }

        public int EndIndex
        {
            get { return endIndex; }
        }

        public bool IndexProvided
        {
            get { return indexProvided; }
        }

        public bool HasMultipleFrames
        {
            get { return endIndex != index && indexProvided; }
        }

        public static implicit operator ResourceID(String Right)
        {
            Right = Right.ToLower();

            ResourceID id = new ResourceID();
            id.id = Right;
            id.endIndex = -1;

            string[] split = Right.Split(new char[] { '@' });
            if (split.Length == 2)
            {
                id.format = split[0];
                Right = split[1];
            }

            split = Right.Split(new char[] { '?' });
            if (split.Length >= 2)
            {
                Right = split[0];
                if (split.Length == 3)
                {
                    id.custom = split[2];
                }

                split = split[1].Split(new Char[] { '-' });
                if (split[0].Length > 0)
                {
                    id.index = int.Parse(split[0]);
                    if (split.Length == 2)
                        id.endIndex = int.Parse(split[1]);
                    id.indexProvided = true;
                }
                else
                {
                    id.index = 0;
                    id.endIndex = 0;
                    id.indexProvided = false;
                }
            }
            else
                id.indexProvided = false;

            id.file = Right;

            if (id.format == null || id.Format == "")
            {

                int dot = Right.LastIndexOf('.');
                if (dot >= 0)
                    id.format = Right.Substring(dot + 1);
            }

            return id;
        }

        public static implicit operator String(ResourceID Right)
        {
            return Right.id;
        }
    }
}
