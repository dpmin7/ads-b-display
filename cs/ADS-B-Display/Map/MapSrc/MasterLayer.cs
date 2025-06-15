
using System.Collections.Generic;

namespace ADS_B_Display_NET.Map.MapSrc
{
    /// <summary>
    /// Base class for core geographical data handlers.
    /// Converted from MasterLayer.h/.cpp
    /// </summary>
    public abstract class MasterLayer : ILayer
    {
        private readonly List<SlaveLayer> _slaveLayers = new List<SlaveLayer>();

        protected MasterLayer()
        {
        }

        /// <summary>
        /// Render one specific region of earth surface.
        /// Must be implemented by derived master layers.
        /// </summary>
        public abstract void RenderRegion(Region rgn);

        /// <summary>
        /// Bind a slave layer to this master layer.
        /// </summary>
        public void BindSlaveLayer(SlaveLayer layer)
        {
            _slaveLayers.Add(layer);
        }

        /// <summary>
        /// Unbind all slave layers.
        /// </summary>
        public void ClearSlaveLayers()
        {
            _slaveLayers.Clear();
        }

        /// <summary>
        /// Render any slave layers that support overdraw.
        /// </summary>
        protected void RenderOverdraw(Region rgn)
        {
            const int SLAVELAYERCAP_OVERDRAW = 0x01;
            foreach (var slave in _slaveLayers) {
                if (slave.GetCap(SLAVELAYERCAP_OVERDRAW))
                    slave.Overdraw(rgn);
            }
        }
    }
}
