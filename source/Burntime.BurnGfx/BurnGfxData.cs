using System;
using System.Collections.Generic;
using Burntime.Platform.IO;

namespace Burntime.Data.BurnGfx
{
    public class BurnGfxData
    {
        static BurnGfxData singleton = null;

        static public BurnGfxData Instance
        {
            get
            {
                if (singleton == null)
                {
                    singleton = new BurnGfxData();
                    singleton.Initialize();
                }
                return singleton;
            }
        }

        #region ColorTables
        Dictionary<String, ColorTable> colorTables = new Dictionary<string,ColorTable>();

        public ColorTable DefaultColorTable
        {
            get { return colorTables["map0"]; }
        }

        public ColorTable GetMapColorTable(int mapId)
        {
            if (colorTables.ContainsKey("map" + mapId.ToString()))
                return colorTables["map" + mapId.ToString()];
            else
                return DefaultColorTable;
        }

        public ColorTable GetRawColorTable(String Raw)
        {
            switch (Raw.ToLower())
            {
                case "opta.raw":
                case "opt.ani":
                    return colorTables["opti.pac"];
                case "film_00.ani":
                    return colorTables["film_00.pac"];
                case "film_01.ani":
                    return colorTables["film_01.pac"];
                case "film_05.ani":
                    return colorTables["film_05.pac"];
                case "film_06.ani":
                    return colorTables["film_06.pac"];
                case "film_10.ani":
                    return colorTables["film_10.pac"];
                case "bar.ani":
                    return colorTables["bar.pac"];
                case "pub1.ani":
                    return colorTables["pub1.pac"];
                case "koch.ani":
                    return colorTables["koch.pac"];
                case "arzt.ani":
                    return colorTables["arzt.pac"];
                case "intro2.ani":
                    return colorTables["map82"];
                case "intro3.ani":
                    return colorTables["map83"];
                case "intro4.ani":
                    return colorTables["map83"];
                default:
                    return DefaultColorTable;
            }
        }

        public void AddPacColorTable(String Pac, ColorTable Table)
        {
            if (colorTables.ContainsKey(Pac.ToLower()))
                return;

            colorTables[Pac.ToLower()] = Table;
        }

        void InitializeColorTables()
        {
        }
        #endregion

        //#region Tiles
        //static TileDB Tiles = TileDB.Singleton;
        //#endregion

        #region Maps
        void InitializeMap(int id)
        {
            string fileName = "mat_" + id.ToString("D3") + ".raw";
            File file = FileSystem.GetFile(fileName);
            if (file == null)
                throw new FileMissingException(fileName);

            colorTables.Add("map" + id.ToString(), new ColorTable(file));

            //file = FileSystem.GetFile(fileName);
            //Map map = new Map(file);
            
            //for (int i = 0; i < map.TileSetList.Count; i++)
            //{
            //    //if (!Tiles.TileSets.ContainsKey(map.TileSetList[i]))
            //    //    Tiles.AddTileSet(map.TileSetList[i], new TileSet("zei_" + map.TileSetList[i].ToString("D3") + ".raw", id));
            //}
        }

        void InitializeMaps()
        {
            for (int i = 0; i < 38; i++)
                InitializeMap(i);
            InitializeMap(80);
            InitializeMap(81);
            InitializeMap(82);
            InitializeMap(83);
            InitializeMap(84);
            InitializeMap(89);
        }
        #endregion

        public void Initialize()
        {
            Burntime.Platform.Log.Info("Initialize BurnGfx library");
            InitializeMaps();
            InitializeColorTables();
        }

    }
}
