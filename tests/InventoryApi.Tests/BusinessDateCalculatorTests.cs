using InventoryApi.Services;
using Xunit;

namespace InventoryApi.Tests;

public class BusinessDateCalculatorTests
{
    [Theory]
    [InlineData(2026, 10, 30, 15, 0, 30)]
    [InlineData(2026, 10, 30, 18, 59, 30)]
    [InlineData(2026, 10, 30, 19, 0, 31)]
    [InlineData(2026, 10, 31, 0, 0, 31)]
    public void GetBusinessDate_UsesThreeAmCutoff(
        int year,
        int month,
        int day,
        int hour,
        int minute,
        int expectedDay)
    {
        DateTime utcTime = new DateTime(
            year, month, day, hour, minute, 0,
            DateTimeKind.Utc);

        DateOnly result =
            BusinessDateCalculator.GetBusinessDate(utcTime);

        Assert.Equal(
            new DateOnly(2026, 10, expectedDay),
            result);
    }
}