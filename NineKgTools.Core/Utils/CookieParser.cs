namespace NineKgTools.Utils;

public class CookieParser
{
    public static Dictionary<string, string> ParseCookies(string cookieHeader)
    {
        var cookies = new Dictionary<string, string>();
        var cookieParts = cookieHeader.Split(';');

        foreach (var part in cookieParts)
        {
            var cookieKeyValue = part.Split(new[] { '=' }, 2);
            if (cookieKeyValue.Length == 2)
            {
                var key = cookieKeyValue[0].Trim();
                var value = cookieKeyValue[1].Trim();
                cookies[key] = value;
            }
        }

        return cookies;
    }
}