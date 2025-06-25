using System.IO;

namespace ADS_B_Display.Map.MapSrc
{
    public abstract class AbstractMap
    {
        public abstract string GetUrl(int x, int y, int z);
        public abstract bool IsInternet();

        protected abstract string GetFolderName();

        public virtual string GetFilePath()
        {
            // Determine base directory
            var homeDir = $"{Directory.GetCurrentDirectory()}\\Map";
            string subfolder = GetFolderName();

            return Path.Combine(homeDir, subfolder);
        }
    }
}
