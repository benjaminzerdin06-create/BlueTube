namespace BlueTube.Core;

public static class Formatting
{
    /// <summary>"4:05", "1:02:03".</summary>
    public static string Duration(int seconds)
    {
        if (seconds <= 0) return "";
        var t = TimeSpan.FromSeconds(seconds);
        return t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{t.Minutes}:{t.Seconds:00}";
    }

    /// <summary>"now", "5m", "3h", "2d", "6w", "1y".</summary>
    public static string Age(DateTime publishedUtc, DateTime nowUtc)
    {
        var d = nowUtc - publishedUtc;
        if (d.TotalMinutes < 1) return "now";
        if (d.TotalHours < 1) return $"{(int)d.TotalMinutes}m";
        if (d.TotalDays < 1) return $"{(int)d.TotalHours}h";
        if (d.TotalDays < 14) return $"{(int)d.TotalDays}d";
        if (d.TotalDays < 365) return $"{(int)(d.TotalDays / 7)}w";
        return $"{(int)(d.TotalDays / 365)}y";
    }
}
