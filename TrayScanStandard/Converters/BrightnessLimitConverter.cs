using System;
using System.Globalization;
using System.Windows.Data;

namespace TrayScanStandard.Converters
{
    /// <summary>
    /// 限制用户输入的亮度值在 0-255 范围内
    /// </summary>
    public class BrightnessLimitConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int intValue)
            {
                return Math.Min(Math.Max(intValue, 0), 255).ToString();
            }
            return "0";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string strValue && int.TryParse(strValue, out int result))
            {
                return Math.Min(Math.Max(result, 0), 255);
            }
            return 0;
        }
    }
}