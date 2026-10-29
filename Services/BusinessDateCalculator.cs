namespace InventoryApi.Services;

public static class BusinessDateCalculator
{
    private static readonly TimeZoneInfo SingaporeTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Singapore");

    public static DateOnly GetBusinessDate(DateTime utcTime)
    {
        if (utcTime.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "The supplied timestamp must be UTC.",
                nameof(utcTime));
        }

        // Convert UTC to Singapore local time.
        DateTime singaporeTime =
            TimeZoneInfo.ConvertTimeFromUtc(
                utcTime,
                SingaporeTimeZone);

        // A business day begins at 3 AM Singapore time.
        DateTime adjustedTime = singaporeTime.AddHours(-3);

        return DateOnly.FromDateTime(adjustedTime);
    }
}