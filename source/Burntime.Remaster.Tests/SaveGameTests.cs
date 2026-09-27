using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Burntime.Framework;
using Burntime.Platform.IO;
using IOFile = System.IO.File;
using VirtualFile = Burntime.Platform.IO.File;

namespace Burntime.Remaster.Tests;

static class SaveGameTests
{
    internal static IEnumerable<Program.Case<bool>> SaveCases()
    {
        yield return Program.Bool("replace existing save only on commit", true,
            () => CheckSave(existing: true, commit: true));
        yield return Program.Bool("publish new save only on commit", true,
            () => CheckSave(existing: false, commit: true));
        yield return Program.Bool("partial serialization preserves existing save", true,
            () => CheckSave(existing: true, commit: false));
        yield return Program.Bool("partial serialization does not publish new save", true,
            () => CheckSave(existing: false, commit: false));
        yield return Program.Bool("failed replacement preserves existing save", true,
            () => CheckSave(existing: true, commit: true, fault: Fault.Replace));
        yield return Program.Bool("failed flush preserves existing save", true,
            () => CheckSave(existing: true, commit: true, fault: Fault.Flush));
        yield return Program.Bool("failed header write preserves existing save", true,
            () => CheckSave(existing: true, commit: true, fault: Fault.Write));
        yield return Program.Bool("failed close preserves existing save", true,
            () => CheckSave(existing: true, commit: true, fault: Fault.Close));
    }

    enum Fault { None, Replace, Flush, Write, Close }

    static bool CheckSave(bool existing, bool commit, Fault fault = Fault.None)
    {
        string directory = Path.Combine(Path.GetTempPath(), "burntime-save-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(directory, "saves"));
        // Exercise the VFS's case-insensitive lookup while retaining disk casing.
        string destination = Path.Combine(directory, "saves", existing ? "MixedCase.sav" : "mixedcase.sav");
        byte[] original = [1, 2, 3, 4, 5];
        if (existing)
            IOFile.WriteAllBytes(destination, original);
        FileSystem.AddPackage("user", new FaultPackage(FileSystem.OpenPackage(directory), fault));
        try
        {
            var save = new SaveGame("saves/mixedcase.sav", "classic", "test");
            try
            {
                Program.Equal(fault != Fault.Write, save.IsValid, "header written");
                AssertOriginal();
                if (save.IsValid)
                {
                    save.Stream!.WriteByte(42);
                    AssertOriginal();
                    if (commit)
                        Program.Equal(fault == Fault.None, save.Commit(), "commit result");
                }
            }
            finally
            {
                save.Close();
                save.Close(); // Cleanup must be safe after commit or abort.
            }

            if (commit && fault == Fault.None)
            {
                var loaded = new SaveGame("user:saves/mixedcase.sav");
                try
                {
                    Program.Equal(true, loaded.IsValid, "read committed save through VFS");
                    Program.Equal("classic", loaded.Game, "game header");
                    Program.Equal("test", loaded.Version, "version header");
                    Program.Equal(42, loaded.Stream!.ReadByte(), "complete payload");
                    Program.Equal(-1, loaded.Stream.ReadByte(), "no old trailing data");
                }
                finally { loaded.Close(); }
            }
            else
                AssertOriginal();

            Program.Equal(commit && fault == Fault.None || existing ? 1 : 0,
                Directory.GetFiles(Path.Combine(directory, "saves")).Length, "no temporary files on disk");
            Program.Equal(0, FileSystem.GetFileNames("user:saves/", "*.tmp").Length,
                "no temporary files in VFS");
            return true;
        }
        finally
        {
            FileSystem.RemovePackage("user");
            Directory.Delete(directory, recursive: true);
        }

        void AssertOriginal()
        {
            Program.Equal(existing, IOFile.Exists(destination), "destination exists");
            Program.Equal(existing, FileSystem.ExistsFile("user:saves/mixedcase.sav"), "VFS destination exists");
            if (existing)
                Program.Equal(true, original.SequenceEqual(IOFile.ReadAllBytes(destination)), "original bytes preserved");
        }
    }

    sealed class FaultPackage(IPackage inner, Fault fault) : IPackage
    {
        public string Name => inner.Name;
        public ICollection<string> Files => inner.Files;
        public bool ExistsFile(FilePath path) => inner.ExistsFile(path);
        public bool AddFile(FilePath path) => inner.AddFile(path);
        public bool RemoveFile(FilePath path) => inner.RemoveFile(path);
        public bool ReplaceFile(FilePath source, FilePath target) =>
            fault != Fault.Replace && inner.ReplaceFile(source, target);
        public void Close() => inner.Close();
        public VirtualFile GetFile(FilePath path, FileOpenMode mode)
        {
            VirtualFile file = inner.GetFile(path, mode);
            return mode == FileOpenMode.Write && fault is Fault.Flush or Fault.Write or Fault.Close
                ? new VirtualFile(new FaultStream(file.Stream, fault)) : file;
        }
    }

    sealed class FaultStream(Stream inner, Fault fault) : Stream
    {
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => inner.CanWrite;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => inner.Position = value; }
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void SetLength(long value) => inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (fault == Fault.Write) throw new IOException("Injected header write failure");
            inner.Write(buffer, offset, count);
        }
        public override void Flush()
        {
            if (fault == Fault.Flush) throw new IOException("Injected flush failure");
            inner.Flush();
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) inner.Dispose();
            base.Dispose(disposing);
            if (disposing && fault == Fault.Close) throw new IOException("Injected close failure");
        }
    }
}
