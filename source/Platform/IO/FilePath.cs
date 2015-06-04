
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

namespace Burntime.Platform.IO
{
    public class FilePath
    {
        string package;
        string fileName;
        string folder;
        string extension;
        string parameter;
        string dot;
        bool isValid;

        public bool IsValid
        {
            get { return isValid; }
        }

        public bool PackageSpecified
        {
            get { return package != null; }
        }

        public string FileName
        {
            get { return fileName + dot + extension; }
        }

        public string FileNameWithoutExtension
        {
            get { return fileName; }
        }

        public string Path
        {
            get { return ((package != null) ? package + ":" : "") + folder + fileName + dot + extension; }
        }

        public string Extension
        {
            get { return extension; }
        }

        public string Package
        {
            get { return package; }
            set { package = value.ToLower(); }
        }

        public string PathWithoutPackage
        {
            get { return folder + fileName + dot + extension; }
        }

        public bool HasParameter
        {
            get { return parameter != null; }
        }

        public string Parameter
        {
            get { return parameter; }
        }

        public string FullPath
        {
            get
            {
                string str = Path;
                if (HasParameter)
                    str += "?" + parameter;
                return str;
            }
        }

        public string Folder
        {
            get { return folder; }
        }
        
        public FilePath(string filePath)
        {
            filePath = filePath.ToLower();

            string[] split = filePath.Split(new char[] { ':' });
            if (split.Length > 2)
            {
                isValid = false;
                return;
            }

            package = null;

            string file;
            if (split.Length == 2)
            {
                package = split[0];
                file = split[1];
            }
            else
                file = split[0];

            int pos = file.LastIndexOfAny(new char[] { '/', '\\' });
            if (pos != -1)
            {
                folder = file.Substring(0, pos + 1);
                file = file.Substring(pos + 1);
            }
            else
                folder = "";

            pos = file.IndexOf('?');
            if (pos != -1)
            {
                parameter = file.Substring(pos + 1);
                file = file.Substring(0, pos);
            }

            pos = file.LastIndexOf('.');
            if (pos != -1)
            {
                extension = file.Substring(pos + 1);
                fileName = file.Substring(0, pos);
                dot = ".";
            }
            else
            {
                fileName = file;
                dot = "";
            }

            isValid = true;
        }

        public static implicit operator FilePath(string right)
        {
            return new FilePath(right);
        }

        public static implicit operator string(FilePath right)
        {
            return right == null ? "" : right.ToString();
        }

        public override string ToString()
        {
            return Path;
        }
    }
}
