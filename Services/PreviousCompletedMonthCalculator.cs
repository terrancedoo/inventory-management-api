namespace InventoryApi.Services;

public static class PreviousCompletedMonthCalculator
{
    public static (
        DateOnly PeriodStart,
        DateOnly PeriodEnd
    ) GetPeriod(DateTime nowUtc)
    {
        // Use the existing 3 AM Singapore business-day rule.
        DateOnly currentBusinessDate =
            BusinessDateCalculator.GetBusinessDate(nowUtc);

        // Identify the month containing that business date.
        var currentMonthStart = new DateOnly(
            currentBusinessDate.Year,
            currentBusinessDate.Month,
            1);

        // The previous business month is fully completed.
        DateOnly previousMonthStart =
            currentMonthStart.AddMonths(-1);

        DateOnly previousMonthEnd =
            currentMonthStart.AddDays(-1);

        return (previousMonthStart, previousMonthEnd);
    }
}