using JobPlatform.AiMatching.Domain;

namespace JobPlatform.AiMatching.Domain.UnitTests;

public class KnownProfileTests
{
    private static readonly DateTime At = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_SetsActiveStanding()
    {
        var profile = KnownProfile.Create(Guid.NewGuid(), Guid.NewGuid(), 1, At);

        profile.Standing.Should().Be(KnownStanding.Active);
        profile.LastEventVersion.Should().Be(1);
    }

    [Fact]
    public void Touch_NewerVersion_UpdatesAndReturnsTrue()
    {
        var profile = KnownProfile.Create(Guid.NewGuid(), Guid.NewGuid(), 1, At);

        var changed = profile.Touch(2, At.AddMinutes(1));

        changed.Should().BeTrue();
        profile.LastEventVersion.Should().Be(2);
    }

    [Fact]
    public void Touch_StaleVersion_IsIgnored()
    {
        var profile = KnownProfile.Create(Guid.NewGuid(), Guid.NewGuid(), 5, At);

        var changed = profile.Touch(3, At.AddMinutes(1));

        changed.Should().BeFalse();
        profile.LastEventVersion.Should().Be(5);
    }

    [Fact]
    public void Deactivate_SetsStandingToDeactivated()
    {
        var profile = KnownProfile.Create(Guid.NewGuid(), Guid.NewGuid(), 1, At);

        profile.Deactivate(At.AddDays(1));

        profile.Standing.Should().Be(KnownStanding.Deactivated);
    }
}

public class KnownPostingTests
{
    private static readonly DateTime At = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void IsActive_ActiveStatusAndNotSuspended_ReturnsTrue()
    {
        var posting = KnownPosting.Create(Guid.NewGuid(), Guid.NewGuid(), "active", "Developer", 1, At);

        posting.IsActive.Should().BeTrue();
    }

    [Fact]
    public void IsActive_NonActiveStatus_ReturnsFalse()
    {
        var posting = KnownPosting.Create(Guid.NewGuid(), Guid.NewGuid(), "paused", "Developer", 1, At);

        posting.IsActive.Should().BeFalse();
    }

    [Fact]
    public void IsActive_SuspendedEvenIfStatusActive_ReturnsFalse()
    {
        var posting = KnownPosting.Create(Guid.NewGuid(), Guid.NewGuid(), "active", "Developer", 1, At);

        posting.Suspend(At.AddMinutes(1));

        posting.IsActive.Should().BeFalse();
    }

    [Fact]
    public void ApplyStatus_NewerVersion_Applies()
    {
        var posting = KnownPosting.Create(Guid.NewGuid(), Guid.NewGuid(), "draft", "Developer", 1, At);

        var changed = posting.ApplyStatus("active", 2, At.AddMinutes(1));

        changed.Should().BeTrue();
        posting.Status.Should().Be("active");
    }

    [Fact]
    public void ApplyStatus_StaleVersion_IsIgnored()
    {
        var posting = KnownPosting.Create(Guid.NewGuid(), Guid.NewGuid(), "active", "Developer", 5, At);

        var changed = posting.ApplyStatus("archived", 2, At.AddMinutes(1));

        changed.Should().BeFalse();
        posting.Status.Should().Be("active");
    }

    [Fact]
    public void Create_TruncatesLongTitle()
    {
        var posting = KnownPosting.Create(Guid.NewGuid(), Guid.NewGuid(), "active", new string('a', 400), 1, At);

        posting.Title.Should().HaveLength(300);
    }
}

public class MatchingWorkItemTests
{
    private static readonly DateTime At = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Enqueue_CreatesPendingItem()
    {
        var item = MatchingWorkItem.Enqueue(WorkItemKind.ComputeShortlist, Guid.NewGuid(), At);

        item.Status.Should().Be(WorkItemStatus.Pending);
        item.NextAttemptUtc.Should().Be(At);
    }

    [Fact]
    public void MarkDone_SetsDoneAndClearsError()
    {
        var item = MatchingWorkItem.Enqueue(WorkItemKind.ComputeShortlist, Guid.NewGuid(), At);

        item.MarkDone(At.AddMinutes(1));

        item.Status.Should().Be(WorkItemStatus.Done);
        item.LastError.Should().BeNull();
    }

    [Fact]
    public void MarkFailed_BelowMaxAttempts_ReschedulesWithBackoff()
    {
        var item = MatchingWorkItem.Enqueue(WorkItemKind.ComputeShortlist, Guid.NewGuid(), At);

        item.MarkFailed("boom", At, maxAttempts: 3, backoff: TimeSpan.FromMinutes(5));

        item.Status.Should().Be(WorkItemStatus.Pending);
        item.Attempts.Should().Be(1);
        item.NextAttemptUtc.Should().Be(At.AddMinutes(5));
    }

    [Fact]
    public void MarkFailed_ReachingMaxAttempts_MarksFailed()
    {
        var item = MatchingWorkItem.Enqueue(WorkItemKind.ComputeShortlist, Guid.NewGuid(), At);

        item.MarkFailed("boom", At, maxAttempts: 1, backoff: TimeSpan.FromMinutes(5));

        item.Status.Should().Be(WorkItemStatus.Failed);
        item.ProcessedAtUtc.Should().Be(At);
    }

    [Fact]
    public void MarkFailed_TruncatesLongError()
    {
        var item = MatchingWorkItem.Enqueue(WorkItemKind.ComputeShortlist, Guid.NewGuid(), At);

        item.MarkFailed(new string('e', 1500), At, maxAttempts: 5, backoff: TimeSpan.FromMinutes(1));

        item.LastError.Should().HaveLength(1000);
    }
}
