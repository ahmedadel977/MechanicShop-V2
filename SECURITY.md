# Security configuration and reporting

## Secret configuration

`JwtSettings:Secret` must be supplied outside tracked configuration. It must contain at least 32 UTF-8 bytes. In Development, ASP.NET Core User Secrets are loaded through the `UserSecretsId` in `MechanicShop.Api.csproj`. In hosted environments, supply it as the `JwtSettings__Secret` environment variable or through the deployment secret store.

Generate and save a local Development key without writing it to shell history:

```powershell
$jwtSecret = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
dotnet user-secrets set "JwtSettings:Secret" $jwtSecret --project MechanicShop.Api
Remove-Variable jwtSecret
```

Set SMTP account data through User Secrets or environment variables such as `EmailSettings__SenderEmail`, `EmailSettings__Username`, and `EmailSettings__Password`. Do not place production values in `appsettings.json`, `appsettings.Example.json`, test fixtures, or logs.

## Rotation

If a JWT signing key was ever committed, revoke it and configure a new random key. Changing the signing key invalidates existing access tokens; users must authenticate again. Rotate SMTP credentials at the mail provider as well. Removing a value from the current file does not remove copies from Git history. History cleanup requires a separately coordinated repository operation; until then, treat committed credentials as compromised.

## Refresh-token storage

Refresh tokens are returned once to the client and stored in the database only as SHA-256 hashes. They rotate on use. Reusing a rotated token revokes all active refresh tokens in its family. Logout and successful password change revoke the user's remaining refresh tokens. Existing raw refresh tokens are converted to hashes by the EF Core migration.

## Reverse proxies and rate limits

Rate limits are configured under `RateLimiting`. Forwarded client addresses are honored only from IP addresses listed under `ForwardedHeaders:KnownProxies`. Never add arbitrary or client-controlled addresses to this list.

## Reporting a vulnerability

Report suspected vulnerabilities privately to the repository maintainers. Include the affected endpoint or package, impact, reproduction details, and a safe contact method. Do not include live credentials or customer data in the report.
