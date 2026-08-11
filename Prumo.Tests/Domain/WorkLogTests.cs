using Prumo.Domain.Entities;

namespace Prumo.Tests.Domain;

public class WorkLogTests
{
    [Fact]
    public void CalculateTotalAmount_MultipliesHoursAndRate()
    {
        var result = WorkLog.CalculateTotalAmount(8m, 15.50m);
        Assert.Equal(124.00m, result);
    }

    [Fact]
    public void CalculateTotalAmount_ZeroHours_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => WorkLog.CalculateTotalAmount(0m, 15m));
    }

    [Fact]
    public void CalculateTotalAmount_ZeroRate_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => WorkLog.CalculateTotalAmount(8m, 0m));
    }

    [Fact]
    public void CalculateTotalAmount_FractionalHours_RoundsToTwoDecimalPlaces()
    {
        var result = WorkLog.CalculateTotalAmount(7.5m, 11.33m);
        Assert.Equal(84.98m, result);
    }
}
