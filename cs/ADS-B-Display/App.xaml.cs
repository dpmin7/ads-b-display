using ADS_B_Display.Models.Settings;
using System;
using System.Reflection;
using System.Windows;

namespace ADS_B_Display
{
    /// <summary>
    /// App.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class App : Application
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        protected override void OnStartup(StartupEventArgs e)
        {   
            base.OnStartup(e);
            // 애플리케이션 시작 시 필요한 초기화 작업을 여기에 추가할 수 있습니다.
            // 예: 데이터베이스 연결, 설정 로드 등
            logger.Info("ADS-B-Display Start.");
            if (!Setting.Load()) {
                logger.Warn("Setting is invalid.");
            }
        }
        protected override void OnExit(ExitEventArgs e)
        {
            Setting.Save();
            logger.Info("ADS-B-Display End.");
            // 애플리케이션 종료 시 필요한 정리 작업을 여기에 추가할 수 있습니다.
            base.OnExit(e);
        }
    }
}
