using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace ADS_B_Display.Views.Converters
{
    public class IsTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is string; // Header가 문자열인지 확인
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class SecToDateTime : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double sec) {
                return TimeSpan.FromSeconds(sec).ToString("hh\\:mm\\:ss");
            }

            return "00:00:00";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (TimeSpan.TryParse(value?.ToString(), out var ts))
                return ts.TotalSeconds;
            return TimeSpan.Zero.TotalSeconds;
        }
    }

    public class MsToDateTime : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is long ms) {
                return TimeFunctions.ConvertMsecToDateTime(ms);
            }
            return default(DateTime);
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("ConvertBack is not implemented for MsToDateTime converter.");
        }
    }

    public class BoolToStringConverter : IValueConverter
    {
        public string TrueString { get; set; } = "True";
        public string FalseString { get; set; } = "False";

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? TrueString : FalseString;
            }
            return FalseString; // 기본값
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string strValue)
            {
                return strValue.Equals("True", StringComparison.OrdinalIgnoreCase);
            }
            return false; // 기본값
        }
    }
}
