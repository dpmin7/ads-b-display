namespace ADS_B_Display.Map.MapSrc
{
    internal class SkyVectorMap : AbstractMap
    {
        private readonly string VFR = "VFR_Map";
        private readonly string IFR_LOW = "IFR_Low_Map";
        private readonly string IFR_HIGH = "IFR_High_Map";
        private const string SkyVectorKey = "V7pMh4xRihf1nr61";
        private const string SkyVectorEdition = "2504";

        private readonly TileServerType serverType;

        public SkyVectorMap(TileServerType serverType)
        {
            this.serverType = serverType;
        }

        public override string GetUrl(int x, int y, int z)
        {
            var _chart = "";
            switch (this.serverType)
            {
                case TileServerType.SkyVector_VFR:
                    _chart = "301";
                    break;
                case TileServerType.SkyVector_IFR_Low:
                    _chart = "302";
                    break;
                case TileServerType.SkyVector_IFR_High:
                    _chart = "304";
                    break;
            }
            
            return $"http://t.skyvector.com/tiles.aspx?x={x}&y={y}&z={z}&k={SkyVectorKey}&c={_chart}&e={SkyVectorEdition}";
        }

        public override bool IsInternet()
        {
            return false;
        }

        protected override string GetFolderName()
        {
            switch(this.serverType)
            {
                case TileServerType.SkyVector_VFR:
                    return VFR;
                case TileServerType.SkyVector_IFR_Low:
                    return IFR_LOW;
                case TileServerType.SkyVector_IFR_High:
                    return IFR_HIGH;
            }

            return "";
        }
    }
}
