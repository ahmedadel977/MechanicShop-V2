using System.Text;

namespace MechanicShop.Infrastructure.Settings;

public sealed class JwtSettings
{
    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int TokenExpirationInMinutes { get; set; }

    public bool IsValid =>
        !string.IsNullOrWhiteSpace(Secret)
        && Encoding.UTF8.GetByteCount(Secret) >= 32
        && !string.IsNullOrWhiteSpace(Issuer)
        && !string.IsNullOrWhiteSpace(Audience)
        && TokenExpirationInMinutes > 0;
}
