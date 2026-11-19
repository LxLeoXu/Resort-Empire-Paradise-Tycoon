using System;

/// <summary>Local calendar days; the high-water mark never moves backwards.</summary>
[Serializable]
public sealed class DailyRewardCalendar
{
    public int latestSeenDay = -1;
    public int lastClaimDay = -1;
    public static int Day(DateTime date) => (int)(date.Date.Ticks / TimeSpan.TicksPerDay);
    public bool Observe(DateTime date)
    {
        int day = Day(date);
        if (day <= latestSeenDay) return false;
        latestSeenDay = day;
        return true;
    }
    public bool CanClaim(DateTime date)
    {
        int day = Day(date);
        return day >= latestSeenDay && day > lastClaimDay;
    }
    public bool IsValid() => latestSeenDay >= -1 && latestSeenDay <= Day(DateTime.MaxValue) &&
        lastClaimDay >= -1 && lastClaimDay <= latestSeenDay;
}
