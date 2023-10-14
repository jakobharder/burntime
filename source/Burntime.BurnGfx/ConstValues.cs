using System;
using System.Collections.Generic;
using System.Text;

namespace Burntime.Data.BurnGfx
{
    public class ConstValues
    {
        public const int MainMap_TextOffsetY = -9;

        public static int GetValue(int ItemId)
        {
            if (ItemId < 0 || ItemId > 57)
                return 0;

            int[] values = new int[] { 
                9,//2,4,6,9,maggots}
                15,//3,7,11,15,rats}
                21,//5,10,15,21,snake}
                27,//6,13,20,27,meat}
                24,//6,12,18,24,water bottle}
                18,//4,9,13,18,bottle}
                36,//9,18,27,36,full canteen}
                27,//6,13,20,27,empty canteen}
                51,//12,25,38,51,full wineskin}
                36,//9,18,27,36,empty wineskin}
                45,//11,22,33,45,knife}
                60,//15,30,45,60,axe}
                81,//20,40,60,81,pitchfork}
                120,//30,60,90,120,loaded rifle}
                99,//24,49,74,99,unloaded rifle}
                60,//15,30,45,60,mine}
                90,//22,45,67,90,rat trap}
                150,//37,75,112,150,trap}
                120,//30,60,90,120,snake trap}
                123,//30,61,92,123,hand pump}
                174,//43,87,130,174,industrial pump}
                180,//45,90,135,180,mine detector}
                135,//33,67,101,135,two-way radio}
                51,//12,25,38,51,broken pump}
                63,//15,31,47,63,spare parts}
                75,//18,37,56,75,defective mine detector}
                45,//11,22,33,45,defective two-way radio}
                30,//7,15,22,30,electrical odds-and-ends}
                24,//6,12,18,24,batteries}
                24,//6,12,18,24,LCD-display}
                24,//6,12,18,24,wire}
                18,//4,9,13,18,woodpile}
                21,//5,10,15,21,screws}
                51,//12,25,38,51,tin}
                48,//12,24,36,48,spring}
                39,//9,19,29,39,hose}
                18,//4,9,13,18,rags}
                39,//9,19,29,39,iron pipe}
                21,//5,10,15,21,ammunition}
                75,//18,37,56,75,gas mask}
                195,//48,97,146,3,protective suit}
                24,//6,12,18,24,gloves}
                39,//9,19,29,39,protective overall}
                24,//6,12,18,24,boots}
                30,//7,15,22,30,rope}
                9,//2,4,6,9,iron bars}
                9,//2,4,6,9,bones}
                9,//2,4,6,9,skull}
                9,//2,4,6,9,gas canister}
                3,//0,1,2,3,gold}
                99,//24,49,74,99,tools}
                45,//11,22,33,45,bible}
                9,//2,4,6,9,fur}
                63,//15,31,47,63,leather jacket}
                6,//1,3,4,6,tires}
                51,//12,25,38,51,steel helmet}
                51,//12,25,38,51,sweater}
                45//11,22,33,45,pants}
            };

            return values[ItemId];
        }
    }
}
