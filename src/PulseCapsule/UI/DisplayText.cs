using System.Globalization;
using System.Text.RegularExpressions;

namespace PulseCapsule.UI;

public static class DisplayText
{
    public static string RelativeUpdate(DateTimeOffset? time, DateTimeOffset now)
    {
        if (time is null) return "尚未读取数据";
        var elapsed = now - time.Value;
        if (elapsed.TotalMinutes < 1) return "刚刚更新";
        if (elapsed.TotalHours < 1) return $"{(int)elapsed.TotalMinutes} 分钟前更新";
        if (elapsed.TotalHours < 24) return $"{(int)elapsed.TotalHours} 小时前更新";
        return LocalDate(time.Value, now) + " 更新";
    }
    public static string LocalDate(DateTimeOffset time, DateTimeOffset now)
    {
        var local = time.ToLocalTime(); var today = now.ToLocalTime();
        return local.Date == today.Date ? "今天 " + local.ToString("HH:mm")
            : local.ToString(local.Year == today.Year ? "M月d日 HH:mm" : "yyyy年M月d日 HH:mm", CultureInfo.InvariantCulture);
    }
    public static string? PlanDates(string? message)
        => message is null ? null : Regex.Replace(message, @"(?<=日期：|到期：)(\d{2}|\d{4})-(\d{2})-(\d{2})\b", match =>
        {
            // WgetCloud abbreviates current subscription years as yy-MM-dd.
            var year = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            if (match.Groups[1].Length == 2) year += 2000;
            var date = $"{year:0000}-{match.Groups[2].Value}-{match.Groups[3].Value}";
            return DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
                ? parsed.ToString("yyyy年M月d日", CultureInfo.InvariantCulture) : match.Value;
        }, RegexOptions.None, TimeSpan.FromMilliseconds(100));
}
