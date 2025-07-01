using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

namespace ADS_B_Display.Map.MapSrc
{
    internal class GoogleMap : IMapProvider
    {
        private readonly string folderName = "GoogleMap";

        public override string GetUrl(int x, int y, int z)
        {
            int correct = (int)Math.Pow(2, z) - 1;
            int convertY = correct - y;
            return $"http://mt1.google.com/vt/lyrs=y&x={x}&y={convertY}&z={z}";
        }

        public override bool IsInternet()
        {
            return true;
        }

        protected override string GetFolderName()
        {
            return folderName;
        }
    }
}
