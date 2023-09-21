using System;
using System.Collections.Generic;


namespace Burntime.Classic.AI
{
    interface IAiGoal
    {
        bool InProgress { get; }

        /// <summary>
        /// Score in days
        /// </summary>
        /// <returns></returns>
        float CalculateScore();

        void AlwaysDo();
        void Act();
    }
}
