using Android.Content;
using Android.Content.Res;

namespace Burntime.Android;

internal static class BundledAssets
{
    static readonly object preparationSync = new();

    internal static string Prepare(Context context, CancellationToken cancellation)
    {
        // A replacement activity may start loading before its predecessor cancels.
        lock (preparationSync)
            return PrepareLocked(context, cancellation);
    }

    static string PrepareLocked(Context context, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        // Installation time changes even for development rebuilds with the same version.
        long installed = context.PackageManager!.GetPackageInfo(context.PackageName!, 0)!.LastUpdateTime;
        string root = Path.Combine(context.FilesDir!.AbsolutePath, "bundled");
        string destination = Path.Combine(root, installed.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (System.IO.File.Exists(Path.Combine(destination, ".complete"))) return destination;

        Directory.CreateDirectory(root);
        string staging = Path.Combine(root, ".loading-" + Guid.NewGuid().ToString("N"));
        try
        {
            Copy(context.Assets!, "burntime", staging, cancellation);
            cancellation.ThrowIfCancellationRequested();
            System.IO.File.WriteAllText(Path.Combine(staging, ".complete"), "");
            Directory.Move(staging, destination);
        }
        finally
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
        }

        // Saves live outside this directory. Keep the previous installation until
        // the replacement has been copied successfully, then reclaim its storage.
        foreach (string previous in Directory.GetDirectories(root))
        {
            if (previous == destination) continue;
            try { Directory.Delete(previous, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        return destination;
    }

    static void Copy(AssetManager assets, string source, string destination, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        string[] children = assets.List(source) ?? [];
        if (children.Length > 0)
        {
            Directory.CreateDirectory(destination);
            foreach (string child in children)
                Copy(assets, source + "/" + child, Path.Combine(destination, child), cancellation);
            return;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        using Stream input = assets.Open(source);
        using Stream output = System.IO.File.Create(destination);
        input.CopyTo(output);
    }
}
