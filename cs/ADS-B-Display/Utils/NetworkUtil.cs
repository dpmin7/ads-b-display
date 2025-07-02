using System.Net.NetworkInformation;

namespace ADS_B_Display.Utils
{
    public static class NetworkUtil
    {
        public static bool IsNetworkAvailable()
        {
            // 네트워크 인터페이스가 하나라도 사용 가능한지 확인
            return NetworkInterface.GetIsNetworkAvailable();
        }
    }
}
