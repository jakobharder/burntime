using Burntime.Data.BurnGfx;
using Burntime.Platform.IO;
using System.Drawing.Imaging;
using System.Drawing;
using System.Runtime.InteropServices;
using System;

namespace BurnGfxRipper;

internal class TileExporter
{
    public void Export(string file, string dir, CommandParameter parameter)
    {
        if (!parameter.MegaTexture)
        {
            ExportAsSeparateFiles(file, dir, parameter);
        }
        else
        {
            ExportAsSingleFile(file, dir, parameter);
        }
    }

    void ExportAsSingleFile(string file, string dir, CommandParameter parameter)
    {
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(dir));

        int[] maplist = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18,
                            19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 80,
                            81, 82, 83, 84, 89 };

        File zei = FileSystem.GetFile(file);
        bool[] done = new bool[63];

        var sheet = new SpriteSheet(256, 32, 32, 64);

        foreach (int i in maplist)
        {
            File f = FileSystem.GetFile("mat_" + i.ToString("D3") + ".raw");
            Map map = new Map(f);
            f.Close();
            foreach (ushort tileid in map.data)
            {
                int set = tileid & 0xff;
                int id = tileid >> 8;

                if (!file.Equals("zei_" + set.ToString("D3") + ".raw", StringComparison.InvariantCultureIgnoreCase))
                    continue;
                if (done[id])
                    continue;

                done[id] = true;

                zei.Seek(id * (16 + 32 * 32), SeekPosition.Begin);
                Tile tile = new Tile(zei, i);

                //Bitmap bmp = new Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                //BitmapData loc = bmp.LockBits(new Rectangle(0, 0, 32, 32), ImageLockMode.ReadWrite, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
                System.IO.MemoryStream mem = new System.IO.MemoryStream();
                tile.Render(mem, 32 * 4);

                sheet.RenderSingle(mem, 32, 32, id);

                //Marshal.Copy(mem.ToArray(), 0, loc.Scan0, 32 * loc.Stride);

                //bmp.UnlockBits(loc);

                //TextureUtils.Save(bmp, dir + "\\" + set.ToString("D3") + "_" + id.ToString("D2") + ".png");

                if (tile.Mask != 0)
                {
                    bool[] bmask = new bool[16];

                    bool mask = false;
                    bool nomask = false;

                    int value = tile.Mask;
                    for (int k = 0; k < 16; k++, value >>= 1)
                    {
                        bmask[15 - k] = (value & 1) == 1;
                        mask |= bmask[15 - k];
                        nomask |= !bmask[15 - k];
                    }

                    if (mask)
                    {
                        String maskFile = dir + "\\" + set.ToString("D3") + "_" + id.ToString("D2") + ".txt";
                        System.IO.TextWriter writer = new System.IO.StreamWriter(maskFile);

                        for (int y = 0; y < 4; y++)
                        {
                            for (int x = 0; x < 4; x++)
                            {
                                writer.Write(bmask[y * 4 + x] ? "1" : "0");
                            }
                            writer.WriteLine();
                        }

                        writer.Close();
                    }
                }
            }

        }

        TextureUtils.Save(sheet.Bitmap, System.IO.Path.Combine(System.IO.Path.GetDirectoryName(dir), System.IO.Path.GetFileNameWithoutExtension(dir) + ".png"));
    }

    void ExportAsSeparateFiles(string file, string dir, CommandParameter parameter)
    {
        System.IO.Directory.CreateDirectory(dir);

        int[] maplist = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18,
                            19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 80,
                            81, 82, 83, 84, 89 };

        File zei = FileSystem.GetFile(file);

        bool[] done = new bool[63];

        foreach (int i in maplist)
        {
            File f = FileSystem.GetFile("mat_" + i.ToString("D3") + ".raw");
            Map map = new Map(f);
            f.Close();
            foreach (ushort tileid in map.data)
            {
                int set = tileid & 0xff;
                int id = tileid >> 8;

                if (!file.Equals("zei_" + set.ToString("D3") + ".raw", StringComparison.InvariantCultureIgnoreCase))
                    continue;
                if (done[id])
                    continue;

                done[id] = true;

                zei.Seek(id * (16 + 32 * 32), SeekPosition.Begin);
                Tile tile = new Tile(zei, i);

                Bitmap bmp = new Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                BitmapData loc = bmp.LockBits(new Rectangle(0, 0, 32, 32), ImageLockMode.ReadWrite, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
                System.IO.MemoryStream mem = new System.IO.MemoryStream();
                tile.Render(mem, loc.Stride);

                Marshal.Copy(mem.ToArray(), 0, loc.Scan0, 32 * loc.Stride);

                bmp.UnlockBits(loc);

                TextureUtils.Save(bmp, dir + "\\" + set.ToString("D3") + "_" + id.ToString("D2") + ".png");

                if (tile.Mask != 0)
                {
                    bool[] bmask = new bool[16];

                    bool mask = false;
                    bool nomask = false;

                    int value = tile.Mask;
                    for (int k = 0; k < 16; k++, value >>= 1)
                    {
                        bmask[15 - k] = (value & 1) == 1;
                        mask |= bmask[15 - k];
                        nomask |= !bmask[15 - k];
                    }

                    if (mask)
                    {

                        String maskFile = dir + "\\" + set.ToString("D3") + "_" + id.ToString("D2") + ".txt";
                        System.IO.TextWriter writer = new System.IO.StreamWriter(maskFile);

                        for (int y = 0; y < 4; y++)
                        {
                            for (int x = 0; x < 4; x++)
                            {
                                writer.Write(bmask[y * 4 + x] ? "1" : "0");
                            }
                            writer.WriteLine();
                        }

                        writer.Close();
                    }
                }
            }

        }
    }
}
