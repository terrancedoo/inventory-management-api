using InventoryApi.Services;
using Xunit;

namespace InventoryApi.Tests;

public class PreviousCompletedMonthCalculatorTests
{
    [Theory]
    // Dec 1 at 2:59 AM Singapore: October is last completed.
    [InlineData(2026, 11, 30, 18, 59, 2026, 10, 31)]

    // Dec 1 at 3:00 AM Singapore: November is complete.
    [InlineData(2026, 11, 30, 19, 0, 2026, 11, 30)]

    // Jan 1 at 2:59 AM Singapore: November is last completed.
    [InlineData(2026, 12, 31, 18, 59, 2026, 11, 30)]

    // Jan 1 at 3:00 AM Singapore: December is complete.
    [InlineData(2026, 12, 31, 19, 0, 2026, 12, 31)]

    // March 1 at 3 AM Singapore, in a leap year.
    [InlineData(2024, 2, 29, 19, 0, 2024, 2, 29)]
    public void GetPeriod_ReturnsLastCompletedBusinessMonth(
        int year,
        int month,
        int day,
        int hour,
        int minute,
        int expectedYear,
        int expectedMonth,
        int expectedLastDay)
    {
        // Arrange: an exact UTC timestamp.
        var utc = new DateTime(
            year, month, day,
            hour, minute, 0,
            DateTimeKind.Utc);

        // Act.
        var (start, end) =
            PreviousCompletedMonthCalculator.GetPeriod(utc);

        // Assert.
        Assert.Equal(
            new DateOnly(expectedYear, expectedMonth, 1),
            start);

        Assert.Equal(
            new DateOnly(
                expectedYear,
                expectedMonth,
                expectedLastDay),
            end);
    }
}