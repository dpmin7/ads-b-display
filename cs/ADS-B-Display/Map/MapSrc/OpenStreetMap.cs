using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADS_B_Display.Map.MapSrc
{
    internal class OpenStreetMap : IMapProvider
    {
        private readonly string folderName = "OpenStreet";

        public override string GetUrl(int x, int y, int z)
        {
            int tileX = x;
            int tileZ = z;
            int maxY = (1 << tileZ) - 1;
            int tileY = maxY - y;  // Y 반전

            return $"https://tile.openstreetmap.org/{tileZ}/{tileX}/{tileY}.png";
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
