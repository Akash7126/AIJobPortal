using JobPlatform.Notification.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;
using Microsoft.Extensions.Options;

namespace JobPlatform.Notification.Application.Delivery;

/// <summary>Daily digest (A-02-013): groups every pending digest-mode e-mail per recipient into one e-mail. Scheduled; a system command with no caller.</summary>
public sealed record BuildDailyDigestCommand(DateTime CutOffUtc) : ICommand<int>;

internal sealed class DigestHandler : ICommandHandler<BuildDailyDigestCommand, int>
{
    private readonly IOutboundMessageRepository _messages;
    private readonly NotificationOptions _options;
    private readonly IUnsubscribeTokens _tokens;
    private readonly TimeProvider _clock;

    public DigestHandler(IOutboundMessageRepository messages, IOptions<NotificationOptions> options, IUnsubscribeTokens tokens, TimeProvider clock)
    {
        _messages = messages;
        _options = options.Value;
        _tokens = tokens;
        _clock = clock;
    }

    public async Task<Result<int>> Handle(BuildDailyDigestCommand request, CancellationToken ct)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var candidates = await _messages.ListDigestCandidatesAsync(request.CutOffUtc, ct);
        var digests = 0;
        foreach (var group in candidates.GroupBy(m => m.RecipientAccountId))
        {
            var key = $"digest:{group.Key:N}:{DateOnly.FromDateTime(now):yyyyMMdd}";
            if (await _messages.ExistsByDedupeKeyAsync(key, ct))
            {
                continue;
            }

            var items = group.OrderBy(m => m.CreatedAtUtc).ToList();
            var body = string.Join("\n\n", items.Select(m => $"- {m.Subject}\n{m.Body}"))
                       + $"\n\nUnsubscribe: {_options.PublicBaseUrl.TrimEnd('/')}/unsubscribe/{_tokens.Create(group.Key, Categories.Digest)}";
            var digest = OutboundMessage.Compose(Channel.Email, group.Key, Categories.Digest, key, $"Your daily digest ({items.Count})", body, items[0].Locale, null, null, false,
                false, now);
            _messages.Add(digest);
            foreach (var item in items)
            {
                item.MarkDigested(digest.Id);
            }

            digests++;
        }

        return digests;
    }
}
