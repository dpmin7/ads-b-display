using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADS_B_Display_NET.Map.MapSrc
{
    /// <summary>
    /// Abstract interface for tile storage loaders/savers.
    /// </summary>
    public interface ITileStorage
    {
        void Enqueue(Tile tile);
    }
}
