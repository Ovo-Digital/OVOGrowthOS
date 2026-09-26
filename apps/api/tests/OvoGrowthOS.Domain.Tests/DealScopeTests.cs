using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Domain.Tests;

public sealed class DealScopeTests
{
    [Fact]
    public void Scope_is_editable_until_the_deal_is_rejected_or_terminated()
    {
        Assert.True(DealScope.Editable(DealStatus.Draft));
        Assert.True(DealScope.Editable(DealStatus.Negotiation));
        Assert.True(DealScope.Editable(DealStatus.Active));
        Assert.True(DealScope.Editable(DealStatus.Expired));
        Assert.False(DealScope.Editable(DealStatus.Rejected));
        Assert.False(DealScope.Editable(DealStatus.Terminated));
        Assert.Contains("Sonlandırılmış", DealScope.EditError(DealStatus.Terminated));
    }

    [Fact]
    public void Only_pending_requests_can_be_decided_and_approval_creates_one_item()
    {
        Assert.Null(DealScope.DecisionError(ScopeRequestStatus.Pending));
        Assert.NotNull(DealScope.DecisionError(ScopeRequestStatus.Approved));
        Assert.NotNull(DealScope.DecisionError(ScopeRequestStatus.Rejected));

        var deal = Guid.NewGuid();
        var request = new DealScopeRequest { DealId = deal, Title = "Ek video", Description = "Paket dışı" };
        var item = DealScope.Approve(request, deal, "partner@ovo.test", DateTimeOffset.UtcNow);
        Assert.NotNull(item);
        Assert.Equal(deal, item!.DealId);
        Assert.Equal("Ek video", item.Title);
        Assert.Equal("partner@ovo.test", item.CreatedBy);
        Assert.Null(item.RemovedAt);

        request.Status = ScopeRequestStatus.Approved; request.ScopeItemId = item.Id;
        Assert.Null(DealScope.Approve(request, deal, "partner@ovo.test", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Request_status_labels_are_turkish()
    {
        Assert.Equal("Bekliyor", DealScope.StatusLabel(ScopeRequestStatus.Pending));
        Assert.Equal("Onaylandı", DealScope.StatusLabel(ScopeRequestStatus.Approved));
        Assert.Equal("Reddedildi", DealScope.StatusLabel(ScopeRequestStatus.Rejected));
    }
}
