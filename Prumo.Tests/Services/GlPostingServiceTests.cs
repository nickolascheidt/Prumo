using NSubstitute;
using Prumo.Application.DTOs.Finance;
using Prumo.Application.Services;
using Prumo.Domain.Enums;

namespace Prumo.Tests.Services;

public class GlPostingServiceTests
{
    private readonly ITenantGlSettingsService _settings = Substitute.For<ITenantGlSettingsService>();
    private readonly IJournalService _journal = Substitute.For<IJournalService>();
    private readonly GlPostingService _sut;

    public GlPostingServiceTests()
    {
        _sut = new GlPostingService(_settings, _journal);
    }

    [Fact]
    public async Task PostApPaymentAsync_WhenCashAccountIsNull_SkipsPosting()
    {
        var tenantId = Guid.NewGuid();
        _settings.GetSettingsAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(new TenantGlSettingsDto(tenantId, null, null, Guid.NewGuid(), "2.1.1"));

        await _sut.PostApPaymentAsync(tenantId, Guid.NewGuid(), "Invoice #1", 100m, DateTime.UtcNow, Guid.NewGuid());

        await _journal.DidNotReceive().CreateJournalEntryAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CreateJournalEntryRequestDto>(),
            Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PostApPaymentAsync_WhenApAccountIsNull_SkipsPosting()
    {
        var tenantId = Guid.NewGuid();
        _settings.GetSettingsAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(new TenantGlSettingsDto(tenantId, Guid.NewGuid(), "1.1.1", null, null));

        await _sut.PostApPaymentAsync(tenantId, Guid.NewGuid(), "Invoice #1", 100m, DateTime.UtcNow, Guid.NewGuid());

        await _journal.DidNotReceive().CreateJournalEntryAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CreateJournalEntryRequestDto>(),
            Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PostApPaymentAsync_WhenSettingsComplete_CreatesBalancedJournalEntry()
    {
        var tenantId  = Guid.NewGuid();
        var cashId    = Guid.NewGuid();
        var apId      = Guid.NewGuid();
        var userId    = Guid.NewGuid();
        var apEntryId = Guid.NewGuid();
        var paidAt    = new DateTime(2026, 4, 28, 0, 0, 0, DateTimeKind.Utc);

        _settings.GetSettingsAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(new TenantGlSettingsDto(tenantId, cashId, "1.1.1", apId, "2.1.1"));

        _journal.CreateJournalEntryAsync(
                default, default, default!, default, default, default)
            .ReturnsForAnyArgs(new JournalEntryDto(
                Guid.NewGuid(), tenantId, paidAt, "AP payment: Invoice #1",
                "AccountsPayable", apEntryId, userId, DateTime.UtcNow,
                Array.Empty<JournalLineDto>()));

        await _sut.PostApPaymentAsync(tenantId, apEntryId, "Invoice #1", 500m, paidAt, userId);

        await _journal.Received(1).CreateJournalEntryAsync(
            tenantId,
            userId,
            Arg.Is<CreateJournalEntryRequestDto>(r =>
                r.Lines.Count == 2 &&
                r.Lines.Any(l =>
                    l.AccountId == apId &&
                    l.EntryType == (int)JournalEntryType.Debit &&
                    l.Amount == 500m) &&
                r.Lines.Any(l =>
                    l.AccountId == cashId &&
                    l.EntryType == (int)JournalEntryType.Credit &&
                    l.Amount == 500m)),
            "AccountsPayable",
            apEntryId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PostApPaymentAsync_WhenSettingsComplete_DescriptionContainsApDescription()
    {
        var tenantId = Guid.NewGuid();
        _settings.GetSettingsAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(new TenantGlSettingsDto(tenantId, Guid.NewGuid(), "1.1.1", Guid.NewGuid(), "2.1.1"));

        _journal.CreateJournalEntryAsync(default, default, default!, default, default, default)
            .ReturnsForAnyArgs(new JournalEntryDto(
                Guid.NewGuid(), tenantId, DateTime.UtcNow, string.Empty,
                null, null, Guid.NewGuid(), DateTime.UtcNow,
                Array.Empty<JournalLineDto>()));

        await _sut.PostApPaymentAsync(tenantId, Guid.NewGuid(), "Rent Q2", 1200m, DateTime.UtcNow, Guid.NewGuid());

        await _journal.Received(1).CreateJournalEntryAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(),
            Arg.Is<CreateJournalEntryRequestDto>(r => r.Description == "AP payment: Rent Q2"),
            Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }
}
