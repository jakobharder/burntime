namespace Burntime.Platform.IO;

public class SystemFile : File
{
    String path;
    public override bool HasFullPath { get { return true; } }
    public override String FullPath { get { return path; } }

    String name;
    String package;
    public override bool HasName { get { return true; } }
    public override String Name { get { return name; } }
    public override String PackageName { get { return package; } }
    public override String FullName { get { return package + ":" + name; } }

    public SystemFile(String Path, String Name, bool WriteAccess)
    {
        stream = new System.IO.FileStream(Path, System.IO.FileMode.OpenOrCreate, WriteAccess ? System.IO.FileAccess.ReadWrite : System.IO.FileAccess.Read);
        path = System.IO.Path.GetFullPath(Path);

        String[] token = Name.Split(new Char[] { ':' });
        name = token[1];
        package = token[0];
    }
}
