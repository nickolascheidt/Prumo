using SaaS_BasePlatform.Domain.Common;

namespace SaaS_BasePlatform.Tests.Domain;

public class PagedResultTests
{
    [Fact]
    public void TotalPages_ShouldCalculateCorrectly()
    {
        var result = new PagedResult<string>([], count: 25, pageNumber: 1, pageSize: 10);

        Assert.Equal(3, result.TotalPages);
    }

    [Fact]
    public void TotalPages_ShouldReturnOne_WhenItemsFitInSinglePage()
    {
        var result = new PagedResult<string>([], count: 5, pageNumber: 1, pageSize: 10);

        Assert.Equal(1, result.TotalPages);
    }

    [Fact]
    public void TotalPages_ShouldReturnZero_WhenNoItems()
    {
        var result = new PagedResult<string>([], count: 0, pageNumber: 1, pageSize: 10);

        Assert.Equal(0, result.TotalPages);
    }

    [Fact]
    public void HasPreviousPage_ShouldBeFalse_WhenOnFirstPage()
    {
        var result = new PagedResult<string>([], count: 25, pageNumber: 1, pageSize: 10);

        Assert.False(result.HasPreviousPage);
    }

    [Fact]
    public void HasPreviousPage_ShouldBeTrue_WhenOnSecondPage()
    {
        var result = new PagedResult<string>([], count: 25, pageNumber: 2, pageSize: 10);

        Assert.True(result.HasPreviousPage);
    }

    [Fact]
    public void HasNextPage_ShouldBeTrue_WhenNotOnLastPage()
    {
        var result = new PagedResult<string>([], count: 25, pageNumber: 1, pageSize: 10);

        Assert.True(result.HasNextPage);
    }

    [Fact]
    public void HasNextPage_ShouldBeFalse_WhenOnLastPage()
    {
        var result = new PagedResult<string>([], count: 25, pageNumber: 3, pageSize: 10);

        Assert.False(result.HasNextPage);
    }

    [Fact]
    public void PaginationParameters_ShouldCapPageSizeAtMax()
    {
        var parameters = new PaginationParameters { PageSize = 200 };

        Assert.Equal(100, parameters.PageSize);
    }

    [Fact]
    public void PaginationParameters_ShouldDefaultToPageOne()
    {
        var parameters = new PaginationParameters();

        Assert.Equal(1, parameters.PageNumber);
    }

    [Fact]
    public void PaginationParameters_ShouldDefaultPageSizeToTen()
    {
        var parameters = new PaginationParameters();

        Assert.Equal(10, parameters.PageSize);
    }
}
