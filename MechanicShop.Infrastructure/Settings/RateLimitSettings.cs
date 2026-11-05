namespace MechanicShop.Infrastructure.Settings;

public sealed class RateLimitSettings
{
    public int GlobalPermitLimit { get; set; } = 100;
    public int GlobalWindowMinutes { get; set; } = 1;
    public int GlobalConcurrencyLimit { get; set; } = 50;
    public int LoginPermitLimit { get; set; } = 5;
    public int LoginWindowMinutes { get; set; } = 1;
    public int RefreshPermitLimit { get; set; } = 10;
    public int RefreshWindowMinutes { get; set; } = 1;
    public int AuthenticatedUserPermitLimit { get; set; } = 120;
    public int AuthenticatedUserWindowMinutes { get; set; } = 1;
    public int AccountCreationPermitLimit { get; set; } = 5;
    public int AccountCreationWindowMinutes { get; set; } = 1;

    public bool IsValid =>
        GlobalPermitLimit > 0
        && GlobalWindowMinutes > 0
        && GlobalConcurrencyLimit > 0
        && LoginPermitLimit > 0
        && LoginWindowMinutes > 0
        && RefreshPermitLimit > 0
        && RefreshWindowMinutes > 0
        && AuthenticatedUserPermitLimit > 0
        && AuthenticatedUserWindowMinutes > 0
        && AccountCreationPermitLimit > 0
        && AccountCreationWindowMinutes > 0;
}
