using System;
using System.Collections.Generic;
using System.Text;

namespace MapEditor
{
    public interface IMapView
    {
        void UpdateTitle();
        void UpdateMap();
        void UpdateObjects();
    }
}
