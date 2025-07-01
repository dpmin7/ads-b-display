using ADS_B_Display.Map.MapSrc;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

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

    public class BoolToNotConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool val)
                return !val;

            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class DevideConverter : IValueConverter
    {
        public double Denominator { get; set; } = 1.0; // 기본값
        public double Numerator { get; set; } = 1.0; // 자식 요소의 크기 조정 비율
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double d && Denominator != 0) // 0으로 나누는 것을 방지
            {
                return  d * (Numerator / Denominator);
            }
            return value; // 변환할 수 없는 경우 원래 값을 반환
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class DoubleToLatitude : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double lat)
            {
                return DMS.DegreesMinutesSecondsLat(lat);
            }

            return "NA";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class DoubleToLongitude : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double lon)
            {
                return DMS.DegreesMinutesSecondsLon(lon);
            }

            return "NA";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class StringToUri : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value is string uriSoruce && string.IsNullOrEmpty(uriSoruce) == false)
                {
                    return new BitmapImage(new Uri($"pack://application:,,,/Images/Flags/{value}.png"));
                }
            }
            catch
            {
                return null;
            }
            
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToBrushConverter : IValueConverter
    {
        public Brush TrueBrush { get; set; } = Brushes.Red;
        public Brush FalseBrush { get; set; } = Brushes.Gray;
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool val)
            {
                return val ? TrueBrush : FalseBrush;
            }

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
