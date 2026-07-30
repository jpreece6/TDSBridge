namespace TDSBridge;

public static class DateExtensions
{
    public static string FormatDateTime(this DateTime dateTime)
    {
        return dateTime.ToString("yyyyMMdd HH:mm:ss.ffffff");

    }
}