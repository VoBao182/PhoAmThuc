namespace VinhKhanhTourDemo;

public static class AppConfig
{
    public static string ApiBaseUrl
    {
        get
        {
            if (DeviceInfo.Platform == DevicePlatform.Android)
                return "http://10.0.2.2:5118";

            return "http://localhost:5118";
        }
    }
}
