using System;
using System.Diagnostics;
using System.Text;
using System.Windows;

namespace ADS_B_Display
{
    public partial class AreaMonitorPopup : Window
    {
        private static AreaMonitorPopup _instance;

        public AreaMonitorPopup()
        {
            InitializeComponent();
            _instance = this; // static 참조 등록

            this.Closed += (s, e) =>
            {
                _instance = null;
            };
        }

        public static void WriteLog(string message)
        {
            _instance?.AppendLog(message);
        }

        private void AppendLog(string message)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => AppendLog(message));
                return;
            }

            LogTextBox.AppendText(message + Environment.NewLine);
            LogTextBox.ScrollToEnd();
        }
    }

    public class TextBoxTraceListener : TraceListener
    {
        private readonly System.Windows.Controls.TextBox _textBox;

        public TextBoxTraceListener(System.Windows.Controls.TextBox textBox)
        {
            _textBox = textBox;
        }



        public override void Write(string message)
        {
            AppendText(message);
        }

        public override void WriteLine(string message)
        {
            AppendText(message + Environment.NewLine);
        }

        private void AppendText(string message)
        {
            if (_textBox.Dispatcher.CheckAccess())
            {
                _textBox.AppendText(message);
                _textBox.ScrollToEnd();
            }
            else
            {
                _textBox.Dispatcher.Invoke(() => {
                    _textBox.AppendText(message);
                    _textBox.ScrollToEnd();
                });
            }
        }
    }
}
