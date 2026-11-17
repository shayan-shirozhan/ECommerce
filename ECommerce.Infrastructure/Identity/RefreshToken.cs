namespace ECommerce.Infrastructure.Identity;

public sealed class RefreshToken
{
    private RefreshToken()
    {
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid FamilyId { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public Guid? ReplacedByTokenId { get; private set; }

    public static RefreshToken Create(
        Guid id,
        Guid userId,
        Guid familyId,
        string tokenHash,
        DateTime createdAtUtc,
        DateTime expiresAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        if (expiresAtUtc <= createdAtUtc)
            throw new ArgumentOutOfRangeException(nameof(expiresAtUtc));

        return new RefreshToken
        {
            Id = id,
            UserId = userId,
            FamilyId = familyId,
            TokenHash = tokenHash,
            CreatedAtUtc = DateTime.SpecifyKind(createdAtUtc, DateTimeKind.Utc),
            ExpiresAtUtc = DateTime.SpecifyKind(expiresAtUtc, DateTimeKind.Utc)
        };
    }

    public void Revoke(DateTime revokedAtUtc, Guid? replacedByTokenId = null)
    {
        if (RevokedAtUtc.HasValue)
            return;

        RevokedAtUtc = DateTime.SpecifyKind(revokedAtUtc, DateTimeKind.Utc);
        ReplacedByTokenId = replacedByTokenId;
    }
}
