using System.Text;

namespace PoC.Shared.Common;

public static class Cursor
{
    public static string ToBase64(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            return string.Empty;
        }

        return Convert.ToBase64String(Encoding.UTF8.GetBytes(plainText));
    }

    public static string FromBase64(string base64Encoded)
    {
        if (string.IsNullOrEmpty(base64Encoded))
        {
            return string.Empty;
        }

        return Encoding.UTF8.GetString(Convert.FromBase64String(base64Encoded));
    }
}
