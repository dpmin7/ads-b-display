using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADS_B_Display.Map.MapSrc
{
    /// <summary>
    /// Base class for handling extra geo-bound data layers.
    /// Converted from SlaveLayer.h/.cpp
    /// </summary>
    public abstract class SlaveLayer : ILayer
    {
        protected int Caps { get; private set; }

        protected SlaveLayer()
        {
            Caps = 0;
        }

        /// <summary>
        /// Check whether capability is supported by this layer.
        /// </summary>
        public bool GetCap(int cap)
        {
            return (Caps & cap) != 0;
        }

        /// <summary>
        /// Render layer data as region overdraw.
        /// Called when OVERDRAW capability is set.
        /// </summary>
        public virtual void Overdraw(Region rgn)
        {
            // Default no-op
        }
    }
}
