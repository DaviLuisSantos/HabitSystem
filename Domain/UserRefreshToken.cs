namespace HabitSystem.Domain;

/// <summary>
/// A refresh token issued to one device/session (table "RefreshTokens").
/// Only the SHA-256 hash of the token is stored.
/// </summary>
public class UserRefreshToken
{
    /// <summary>
    /// Unique identifier
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Owner of this token
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// SHA-256 hash (hex) of the raw refresh token
    /// </summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>
    /// When this token expires
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// When this token was issued
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// When this token was revoked (rotation, logout, password change). Null if still valid.
    /// </summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>
    /// Hash of the token that replaced this one on rotation (null if revoked for other reasons)
    /// </summary>
    public string? ReplacedByTokenHash { get; set; }

    // Navigation property
    public User User { get; set; } = null!;
}
