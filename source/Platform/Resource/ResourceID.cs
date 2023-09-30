using SlimDX.Direct3D9;
using System;
using System.Diagnostics;

namespace Burntime.Platform.Resource
{
    [DebuggerDisplay("ID = {id}")]
    public class ResourceID
    {
        private readonly string id;

        public string Format { get; init; }
        public string File { get; init; }
        public int Index { get; init; }
        public string Custom { get; init; }
        public int EndIndex { get; init; }
        public bool IndexProvided { get; init; }
        public bool HasMultipleFrames => EndIndex != Index && IndexProvided;

        public static implicit operator ResourceID(string right) => new(right);
        public static implicit operator string(ResourceID right) => right.id;
        public override string ToString() => id;

        public ResourceID(string path)
        {
            path = path.ToLower();

            id = path;
            EndIndex = -1;

            string[] split = path.Split(new char[] { '@' });
            if (split.Length == 2)
            {
                Format = split[0];
                path = split[1];
            }

            split = path.Split(new char[] { '?' });
            if (split.Length >= 2)
            {
                path = split[0];
                if (split.Length == 3)
                {
                    Custom = split[2];
                }

                split = split[1].Split(new Char[] { '-' });
                if (split[0].Length > 0)
                {
                    Index = int.Parse(split[0]);
                    if (split.Length == 2)
                        EndIndex = int.Parse(split[1]);
                    IndexProvided = true;
                }
                else
                {
                    Index = 0;
                    EndIndex = 0;
                    IndexProvided = false;
                }
            }
            else
                IndexProvided = false;

            File = path;

            if (string.IsNullOrEmpty(Format))
            {

                int dot = path.LastIndexOf('.');
                if (dot >= 0)
                    Format = path.Substring(dot + 1);
            }
        }
    }
}
