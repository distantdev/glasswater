namespace glasswater;

public static class FimCompletionFilter
{
    public static bool IsLowQuality(string? middle)
    {
        if (string.IsNullOrWhiteSpace(middle))
        {
            return true;
        }

        if (middle.Contains("0123456789", StringComparison.Ordinal) ||
            middle.Contains("abcdefghijklmnopqrstuvwxyz", StringComparison.Ordinal) ||
            middle.Contains("ABCDEFGHIJKLMNOPQRSTUVWXYZ", StringComparison.Ordinal))
        {
            return true;
        }

        if (middle.Length >= 12 &&
            middle.IndexOf(' ') < 0 &&
            middle.IndexOf('-') < 0 &&
            middle.IndexOf('$') < 0 &&
            middle.IndexOf('|') < 0 &&
            middle.IndexOf('.') < 0)
        {
            int alnum = 0;
            foreach (char c in middle)
            {
                if (char.IsLetterOrDigit(c))
                {
                    alnum++;
                }
            }

            if (alnum >= middle.Length * 0.95)
            {
                return true;
            }
        }

        return false;
    }
}
