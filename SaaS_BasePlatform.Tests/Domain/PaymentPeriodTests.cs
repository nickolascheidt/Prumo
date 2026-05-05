using SaaS_BasePlatform.Domain.Entities;

namespace SaaS_BasePlatform.Tests.Domain;

public class PaymentPeriodTests
{
    [Fact]
    public void CalculateSummary_EmptyLogs_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() =>
            PaymentPeriod.CalculateSummary(Array.Empty<WorkLog>()));
    }

    [Fact]
    public void CalculateSummary_SingleLog_ReturnsTotals()
    {
        var logs = new List<WorkLog>
        {
            new() { HoursWorked = 8m, HourlyRateAtTime = 15m, TotalAmount = 120m }
        };

        var (totalHours, totalAmount) = PaymentPeriod.CalculateSummary(logs);

        Assert.Equal(8m, totalHours);
        Assert.Equal(120m, totalAmount);
    }

    [Fact]
    public void CalculateSummary_MultipleLogs_SumsAll()
    {
        var logs = new List<WorkLog>
        {
            new() { HoursWorked = 8m,   HourlyRateAtTime = 15m, TotalAmount = 120.00m },
            new() { HoursWorked = 7.5m, HourlyRateAtTime = 15m, TotalAmount = 112.50m },
            new() { HoursWorked = 6m,   HourlyRateAtTime = 15m, TotalAmount = 90.00m  }
        };

        var (totalHours, totalAmount) = PaymentPeriod.CalculateSummary(logs);

        Assert.Equal(21.5m, totalHours);
        Assert.Equal(322.50m, totalAmount);
    }
}
