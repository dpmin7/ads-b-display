using System;

namespace ADS_B_Display.Map.MapSrc
{
    internal class ArcGISMap : AbstractMap
    {
        private readonly string folderName = "ArcGGIS";

        public override string GetUrl(int x, int y, int z)
        {
            int correct = (int)Math.Pow(2, z) - 1;
            int convertY = correct - y;
            return $"https://services.arcgisonline.com/ArcGIS/rest/services/World_Topo_Map/MapServer/tile/{z}/{convertY}/{x}";
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
