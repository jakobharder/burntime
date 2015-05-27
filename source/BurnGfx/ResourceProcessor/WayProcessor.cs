using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform;
using Burntime.Platform.IO;
using Burntime.Platform.Resource;
using Burntime.Platform.Graphics;

namespace Burntime.Data.BurnGfx.ResourceProcessor
{
    public class WayProcessor : IDataProcessor
    {
        public DataObject Process(ResourceID ID, ResourceManager ResourceManager)
        {
            Save.SaveGame gamdat = new Save.SaveGame();
            gamdat.Open(FileSystem.GetFile(ID.File).Stream);
            WayData data = new WayData();
            data.DataName = ID;


            Dictionary<int, Way> ways = new Dictionary<int, Way>();

            data.Cross = new CrossWay[gamdat.Locations.Length];

            for (int i = 0; i < gamdat.Locations.Length; i++)
            {
                Save.Location city = gamdat.Locations[i];

                List<int> cross = new List<int>();

                for (int j = 0; j < Save.Location.MaxNeighborCount; j++)
                {
                    if (city.Neighbors[j] >= 0)
                    {
                        if (!ways.ContainsKey(city.Ways[j]))
                        {
                            Way way = new Way();
                            way.Start = i;
                            way.End = city.Neighbors[j];
                            ways.Add(city.Ways[j], way);

                        }
                        cross.Add(city.Ways[j]);
                    }
                }

                data.Cross[i].Ways = cross.ToArray();
            }

            Map map = new Map("mat_000.raw");

            //int pos = 28;

            data.Ways = new Way[ways.Count];
            for (int i = 0; i < ways.Count; i++)
            {
                data.Ways[i] = ways[i];

                data.Ways[i].Position = new Vector2();
                data.Ways[i].Position.x = System.Math.Min(map.Doors[data.Ways[i].Start].Area.Center.x, map.Doors[data.Ways[i].End].Area.Center.x);
                data.Ways[i].Position.y = System.Math.Min(map.Doors[data.Ways[i].Start].Area.Center.y, map.Doors[data.Ways[i].End].Area.Center.y);
            }

            //    Vector2 dist = map.Doors[data.Ways[i].End].Area.Center - map.Doors[data.Ways[i].Start].Area.Center;
            //    if (dist.x < 0)
            //        dist.x = map.Doors[data.Ways[i].Start].Area.Left - map.Doors[data.Ways[i].End].Area.Right;
            //    else
            //        dist.x = map.Doors[data.Ways[i].End].Area.Left - map.Doors[data.Ways[i].Start].Area.Right;
            //    //if (dist.x < 0)
            //    //{
            //    //    int tmp = data.Ways[i].Start;
            //    //    data.Ways[i].Start = data.Ways[i].End;
            //    //    data.Ways[i].End = tmp;
            //    //}
            //    data.Ways[i].Images = new Sprite[(Math.Abs(dist.x) + 31) / 32];
            //    for (int j = 0; j < data.Ways[i].Images.Length; j++)
            //    {
            //        data.Ways[i].Images[j] = ResourceManager.GetImage("syst.raw?" + (pos + j).ToString());
            //    }
            //    pos += data.Ways[i].Images.Length;
            //}

            data.Ways[0].Images = new Sprite[1];
            data.Ways[0].Images[0] = ResourceManager.GetImage("syst.raw?28".ToString(), ResourceLoadType.Delayed);
            data.Ways[0].Position += new Vector2(7, 5);
            data.Ways[1].Images = new Sprite[1];
            data.Ways[1].Images[0] = ResourceManager.GetImage("syst.raw?30".ToString(), ResourceLoadType.Delayed);
            data.Ways[1].Position += new Vector2(10, 6);
            data.Ways[2].Images = new Sprite[2];
            data.Ways[2].Images[0] = ResourceManager.GetImage("syst.raw?31".ToString(), ResourceLoadType.Delayed);
            data.Ways[2].Images[1] = ResourceManager.GetImage("syst.raw?32".ToString(), ResourceLoadType.Delayed);
            data.Ways[2].Position += new Vector2(20, 3);
            data.Ways[3].Images = new Sprite[3];
            data.Ways[3].Images[0] = ResourceManager.GetImage("syst.raw?33".ToString(), ResourceLoadType.Delayed);
            data.Ways[3].Images[1] = ResourceManager.GetImage("syst.raw?34".ToString(), ResourceLoadType.Delayed);
            data.Ways[3].Images[2] = ResourceManager.GetImage("syst.raw?35".ToString(), ResourceLoadType.Delayed);
            data.Ways[3].Position += new Vector2(12, 7);
            data.Ways[4].Images = new Sprite[3];
            data.Ways[4].Images[0] = ResourceManager.GetImage("syst.raw?36".ToString(), ResourceLoadType.Delayed);
            data.Ways[4].Images[1] = ResourceManager.GetImage("syst.raw?37".ToString(), ResourceLoadType.Delayed);
            data.Ways[4].Images[2] = ResourceManager.GetImage("syst.raw?38".ToString(), ResourceLoadType.Delayed);
            data.Ways[4].Position += new Vector2(10, -1);
            data.Ways[5].Images = new Sprite[1];
            data.Ways[5].Images[0] = ResourceManager.GetImage("syst.raw?39".ToString(), ResourceLoadType.Delayed);
            data.Ways[5].Position += new Vector2(14, 2);
            data.Ways[6].Images = new Sprite[1];
            data.Ways[6].Images[0] = ResourceManager.GetImage("syst.raw?40".ToString(), ResourceLoadType.Delayed);
            data.Ways[6].Position += new Vector2(6, 0);
            data.Ways[7].Images = new Sprite[2];
            data.Ways[7].Images[0] = ResourceManager.GetImage("syst.raw?41".ToString(), ResourceLoadType.Delayed);
            data.Ways[7].Images[1] = ResourceManager.GetImage("syst.raw?42".ToString(), ResourceLoadType.Delayed);
            data.Ways[7].Position += new Vector2(4, 6);
            data.Ways[8].Images = new Sprite[3];
            data.Ways[8].Images[0] = ResourceManager.GetImage("syst.raw?43".ToString(), ResourceLoadType.Delayed);
            data.Ways[8].Images[1] = ResourceManager.GetImage("syst.raw?44".ToString(), ResourceLoadType.Delayed);
            data.Ways[8].Images[2] = ResourceManager.GetImage("syst.raw?45".ToString(), ResourceLoadType.Delayed);
            data.Ways[8].Position += new Vector2(2, 4);
            data.Ways[9].Images = new Sprite[2];
            data.Ways[9].Images[0] = ResourceManager.GetImage("syst.raw?46".ToString(), ResourceLoadType.Delayed);
            data.Ways[9].Images[1] = ResourceManager.GetImage("syst.raw?47".ToString(), ResourceLoadType.Delayed);
            data.Ways[9].Position += new Vector2(7, 3);
            data.Ways[10].Images = new Sprite[1];
            data.Ways[10].Images[0] = ResourceManager.GetImage("syst.raw?48".ToString(), ResourceLoadType.Delayed);
            data.Ways[10].Position += new Vector2(5, 4);
            data.Ways[11].Images = new Sprite[2];
            data.Ways[11].Images[0] = ResourceManager.GetImage("syst.raw?49".ToString(), ResourceLoadType.Delayed);
            data.Ways[11].Images[1] = ResourceManager.GetImage("syst.raw?50".ToString(), ResourceLoadType.Delayed);
            data.Ways[11].Position += new Vector2(4, 7);
            data.Ways[12].Images = new Sprite[2];
            data.Ways[12].Images[0] = ResourceManager.GetImage("syst.raw?51".ToString(), ResourceLoadType.Delayed);
            data.Ways[12].Images[1] = ResourceManager.GetImage("syst.raw?52".ToString(), ResourceLoadType.Delayed);
            data.Ways[12].Position += new Vector2(3, 7);
            data.Ways[13].Images = new Sprite[2];
            data.Ways[13].Images[0] = ResourceManager.GetImage("syst.raw?53".ToString(), ResourceLoadType.Delayed);
            data.Ways[13].Images[1] = ResourceManager.GetImage("syst.raw?54".ToString(), ResourceLoadType.Delayed);
            data.Ways[13].Position += new Vector2(6, 2);
            data.Ways[14].Images = new Sprite[1];
            data.Ways[14].Images[0] = ResourceManager.GetImage("syst.raw?29".ToString(), ResourceLoadType.Delayed);
            data.Ways[14].Position += new Vector2(10, 3);
            data.Ways[15].Images = new Sprite[2];
            data.Ways[15].Images[0] = ResourceManager.GetImage("syst.raw?55".ToString(), ResourceLoadType.Delayed);
            data.Ways[15].Images[1] = ResourceManager.GetImage("syst.raw?56".ToString(), ResourceLoadType.Delayed);
            data.Ways[15].Position += new Vector2(3, 7);
            data.Ways[16].Images = new Sprite[1];
            data.Ways[16].Images[0] = ResourceManager.GetImage("syst.raw?57".ToString(), ResourceLoadType.Delayed);
            data.Ways[16].Position += new Vector2(5, 7);
            data.Ways[17].Images = new Sprite[1];
            data.Ways[17].Images[0] = ResourceManager.GetImage("syst.raw?58".ToString(), ResourceLoadType.Delayed);
            data.Ways[17].Position += new Vector2(5, 2);
            data.Ways[18].Images = new Sprite[2];
            data.Ways[18].Images[0] = ResourceManager.GetImage("syst.raw?59".ToString(), ResourceLoadType.Delayed);
            data.Ways[18].Images[1] = ResourceManager.GetImage("syst.raw?60".ToString(), ResourceLoadType.Delayed);
            data.Ways[18].Position += new Vector2(5, 0);
            data.Ways[19].Images = new Sprite[2];
            data.Ways[19].Images[0] = ResourceManager.GetImage("syst.raw?61".ToString(), ResourceLoadType.Delayed);
            data.Ways[19].Images[1] = ResourceManager.GetImage("syst.raw?62".ToString(), ResourceLoadType.Delayed);
            data.Ways[19].Position += new Vector2(12, 5);
            data.Ways[20].Images = new Sprite[1];
            data.Ways[20].Images[0] = ResourceManager.GetImage("syst.raw?63".ToString(), ResourceLoadType.Delayed);
            data.Ways[20].Position += new Vector2(8, 9);
            data.Ways[21].Images = new Sprite[3];
            data.Ways[21].Images[0] = ResourceManager.GetImage("syst.raw?64".ToString(), ResourceLoadType.Delayed);
            data.Ways[21].Images[1] = ResourceManager.GetImage("syst.raw?65".ToString(), ResourceLoadType.Delayed);
            data.Ways[21].Images[2] = ResourceManager.GetImage("syst.raw?66".ToString(), ResourceLoadType.Delayed);
            data.Ways[21].Position += new Vector2(8, -1);
            data.Ways[22].Images = new Sprite[2];
            data.Ways[22].Images[0] = ResourceManager.GetImage("syst.raw?68".ToString(), ResourceLoadType.Delayed);
            data.Ways[22].Images[1] = ResourceManager.GetImage("syst.raw?69".ToString(), ResourceLoadType.Delayed);
            data.Ways[22].Position += new Vector2(1, 7);
            data.Ways[23].Images = new Sprite[1];
            data.Ways[23].Images[0] = ResourceManager.GetImage("syst.raw?70".ToString(), ResourceLoadType.Delayed);
            data.Ways[23].Position += new Vector2(-4, 6);
            data.Ways[24].Images = new Sprite[2];
            data.Ways[24].Images[0] = ResourceManager.GetImage("syst.raw?71".ToString(), ResourceLoadType.Delayed);
            data.Ways[24].Images[1] = ResourceManager.GetImage("syst.raw?72".ToString(), ResourceLoadType.Delayed);
            data.Ways[24].Position += new Vector2(11, 3);
            data.Ways[25].Images = new Sprite[4];
            data.Ways[25].Images[0] = ResourceManager.GetImage("syst.raw?73".ToString(), ResourceLoadType.Delayed);
            data.Ways[25].Images[1] = ResourceManager.GetImage("syst.raw?74".ToString(), ResourceLoadType.Delayed);
            data.Ways[25].Images[2] = ResourceManager.GetImage("syst.raw?75".ToString(), ResourceLoadType.Delayed);
            data.Ways[25].Images[3] = ResourceManager.GetImage("syst.raw?76".ToString(), ResourceLoadType.Delayed);
            data.Ways[25].Position += new Vector2(8, 4);
            data.Ways[26].Images = new Sprite[1];
            data.Ways[26].Images[0] = ResourceManager.GetImage("syst.raw?77".ToString(), ResourceLoadType.Delayed);
            data.Ways[26].Position += new Vector2(-20, 10);
            data.Ways[27].Images = new Sprite[3];
            data.Ways[27].Images[0] = ResourceManager.GetImage("syst.raw?78".ToString(), ResourceLoadType.Delayed);
            data.Ways[27].Images[1] = ResourceManager.GetImage("syst.raw?79".ToString(), ResourceLoadType.Delayed);
            data.Ways[27].Images[2] = ResourceManager.GetImage("syst.raw?80".ToString(), ResourceLoadType.Delayed);
            data.Ways[27].Position += new Vector2(11, 7);
            data.Ways[28].Images = new Sprite[1];
            data.Ways[28].Images[0] = ResourceManager.GetImage("syst.raw?81".ToString(), ResourceLoadType.Delayed);
            data.Ways[28].Position += new Vector2(2, 7);
            data.Ways[29].Images = new Sprite[2];
            data.Ways[29].Images[0] = ResourceManager.GetImage("syst.raw?82".ToString(), ResourceLoadType.Delayed);
            data.Ways[29].Images[1] = ResourceManager.GetImage("syst.raw?83".ToString(), ResourceLoadType.Delayed);
            data.Ways[29].Position += new Vector2(20, 3);
            data.Ways[30].Images = new Sprite[1];
            data.Ways[30].Images[0] = ResourceManager.GetImage("syst.raw?84".ToString(), ResourceLoadType.Delayed);
            data.Ways[30].Position += new Vector2(-3, 8);
            data.Ways[31].Images = new Sprite[2];
            data.Ways[31].Images[0] = ResourceManager.GetImage("syst.raw?85".ToString(), ResourceLoadType.Delayed);
            data.Ways[31].Images[1] = ResourceManager.GetImage("syst.raw?86".ToString(), ResourceLoadType.Delayed);
            data.Ways[31].Position += new Vector2(10, 3);
            data.Ways[32].Images = new Sprite[1];
            data.Ways[32].Images[0] = ResourceManager.GetImage("syst.raw?87".ToString(), ResourceLoadType.Delayed);
            data.Ways[32].Position += new Vector2(-2, 8);
            data.Ways[33].Images = new Sprite[2];
            data.Ways[33].Images[0] = ResourceManager.GetImage("syst.raw?88".ToString(), ResourceLoadType.Delayed);
            data.Ways[33].Position += new Vector2(13, 4);
            data.Ways[34].Images = new Sprite[2];
            data.Ways[34].Images[0] = ResourceManager.GetImage("syst.raw?89".ToString(), ResourceLoadType.Delayed);
            data.Ways[34].Images[1] = ResourceManager.GetImage("syst.raw?90".ToString(), ResourceLoadType.Delayed);
            data.Ways[34].Position += new Vector2(10, 2);
            data.Ways[35].Images = new Sprite[1];
            data.Ways[35].Images[0] = ResourceManager.GetImage("syst.raw?91".ToString(), ResourceLoadType.Delayed);
            data.Ways[35].Position += new Vector2(5, 7);
            data.Ways[36].Images = new Sprite[1];
            data.Ways[36].Images[0] = ResourceManager.GetImage("syst.raw?92".ToString(), ResourceLoadType.Delayed);
            data.Ways[36].Position += new Vector2(3, 3);
            data.Ways[37].Images = new Sprite[2];
            data.Ways[37].Images[0] = ResourceManager.GetImage("syst.raw?93".ToString(), ResourceLoadType.Delayed);
            data.Ways[37].Images[1] = ResourceManager.GetImage("syst.raw?94".ToString(), ResourceLoadType.Delayed);
            data.Ways[37].Position += new Vector2(4, 4);
            data.Ways[38].Images = new Sprite[2];
            data.Ways[38].Images[0] = ResourceManager.GetImage("syst.raw?95".ToString(), ResourceLoadType.Delayed);
            data.Ways[38].Images[1] = ResourceManager.GetImage("syst.raw?96".ToString(), ResourceLoadType.Delayed);
            data.Ways[38].Position += new Vector2(13, -1);
            data.Ways[39].Images = new Sprite[1];
            data.Ways[39].Images[0] = ResourceManager.GetImage("syst.raw?97".ToString(), ResourceLoadType.Delayed);
            data.Ways[39].Position += new Vector2(-3, 8);
            data.Ways[40].Images = new Sprite[2];
            data.Ways[40].Images[0] = ResourceManager.GetImage("syst.raw?98".ToString(), ResourceLoadType.Delayed);
            data.Ways[40].Images[1] = ResourceManager.GetImage("syst.raw?99".ToString(), ResourceLoadType.Delayed);
            data.Ways[40].Position += new Vector2(19, 5);
            data.Ways[41].Images = new Sprite[1];
            data.Ways[41].Images[0] = ResourceManager.GetImage("syst.raw?100".ToString(), ResourceLoadType.Delayed);
            data.Ways[41].Position += new Vector2(8, 8);
            data.Ways[42].Images = new Sprite[2];
            data.Ways[42].Images[0] = ResourceManager.GetImage("syst.raw?101".ToString(), ResourceLoadType.Delayed);
            data.Ways[42].Images[1] = ResourceManager.GetImage("syst.raw?102".ToString(), ResourceLoadType.Delayed);
            data.Ways[42].Position += new Vector2(12, 6);
            data.Ways[43].Images = new Sprite[4];
            data.Ways[43].Images[0] = ResourceManager.GetImage("syst.raw?103".ToString(), ResourceLoadType.Delayed);
            data.Ways[43].Images[1] = ResourceManager.GetImage("syst.raw?104".ToString(), ResourceLoadType.Delayed);
            data.Ways[43].Images[2] = ResourceManager.GetImage("syst.raw?105".ToString(), ResourceLoadType.Delayed);
            data.Ways[43].Images[3] = ResourceManager.GetImage("syst.raw?106".ToString(), ResourceLoadType.Delayed);
            data.Ways[43].Position += new Vector2(12, 4);
            data.Ways[44].Images = new Sprite[2];
            data.Ways[44].Images[0] = ResourceManager.GetImage("syst.raw?107".ToString(), ResourceLoadType.Delayed);
            data.Ways[44].Images[1] = ResourceManager.GetImage("syst.raw?108".ToString(), ResourceLoadType.Delayed);
            data.Ways[44].Position += new Vector2(2, 7);
            data.Ways[45].Images = new Sprite[1];
            data.Ways[45].Images[0] = ResourceManager.GetImage("syst.raw?109".ToString(), ResourceLoadType.Delayed);
            data.Ways[45].Position += new Vector2(-2, 7);
            data.Ways[46].Images = new Sprite[2];
            data.Ways[46].Images[0] = ResourceManager.GetImage("syst.raw?110".ToString(), ResourceLoadType.Delayed);
            data.Ways[46].Images[1] = ResourceManager.GetImage("syst.raw?111".ToString(), ResourceLoadType.Delayed);
            data.Ways[46].Position += new Vector2(10, 7);

            return data;
        }

        string[] IDataProcessor.Names
        {
            get { return new string[] { "burngfxways" }; }
        }
    }
}
