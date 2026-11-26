# Docker HTTPS for the MechanicShop API

The API listens on HTTP `8080` and HTTPS `8443` inside its container. Compose publishes only HTTPS as `7169:8443`. The local development override sets `ASPNETCORE_ENVIRONMENT=Development`, so the existing Swagger UI remains available. Production startup still hides Swagger through the existing `IsDevelopment()` check.

## Create and trust a local certificate

Run these commands in PowerShell from the repository root. The PFX is stored under the current user's ASP.NET certificate folder, outside the Git repository. The generated password is written only to the ignored `docker-compose/.env` file.

```powershell
$composeDir = Join-Path (Get-Location) 'docker-compose'
$envFile = Join-Path $composeDir '.env'
$certDir = Join-Path $env:USERPROFILE '.aspnet\https'
$certPath = Join-Path $certDir 'mechanicshop-docker.pfx'

if (-not (Test-Path $envFile)) {
    Copy-Item (Join-Path $composeDir '.env.example') $envFile
    Write-Host 'Edit docker-compose/.env and fill the required SQL, JWT, Seq, and Grafana values, then rerun this setup.'
    return
}

New-Item -ItemType Directory -Force -Path $certDir | Out-Null
dotnet dev-certs https --trust
if ($LASTEXITCODE -ne 0) { throw 'Could not trust the ASP.NET Core development certificate.' }

$certPassword = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
dotnet dev-certs https --export-path $certPath --password $certPassword
if ($LASTEXITCODE -ne 0) { throw 'Could not export the HTTPS development certificate.' }

$lines = [System.Collections.Generic.List[string]]::new()
Get-Content $envFile | ForEach-Object { $lines.Add($_) }
$updates = @{
    HTTPS_CERT_PATH = ($certPath -replace '\\', '/')
    HTTPS_CERT_PASSWORD = $certPassword
}
foreach ($key in $updates.Keys) {
    $match = -1
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match "^\s*$([regex]::Escape($key))\s*=") { $match = $i; break }
    }
    $entry = "$key=$($updates[$key])"
    if ($match -ge 0) { $lines[$match] = $entry } else { $lines.Add($entry) }
}
[System.IO.File]::WriteAllLines($envFile, $lines, [System.Text.UTF8Encoding]::new($false))
Remove-Variable certPassword
Write-Host 'Certificate exported outside the repository; local Compose environment updated.'
```

`dotnet dev-certs https --trust` may ask for Windows confirmation. Approve it so browsers trust the localhost certificate. Never commit the PFX, private key, password, or `docker-compose/.env`. The repository ignores those paths, and `.dockerignore` excludes them from the image build context.

If another local Redis already owns host port `6379`, set `REDIS_HOST_PORT=6380` in `.env`; the API still connects to Compose Redis at `redis:6379`. The supplied example uses `6380` for this reason.

## Build and start the existing stack

From the repository root, after filling the required non-HTTPS secrets in `docker-compose/.env`:

```powershell
docker compose --env-file docker-compose/.env -f docker-compose/docker-compose.yml -f docker-compose/docker-compose.override.yml config --quiet
docker compose --env-file docker-compose/.env -f docker-compose/docker-compose.yml -f docker-compose/docker-compose.override.yml up --build -d
docker compose --env-file docker-compose/.env -f docker-compose/docker-compose.yml -f docker-compose/docker-compose.override.yml ps
```

All existing services remain in the same Compose network. Containers reach the API's HTTP and HTTPS listeners internally on `8080` and `8443`. Only HTTPS is published to the host; the API's Docker health check probes its HTTPS listener internally.

## Verify HTTPS

```powershell
curl.exe -sS https://localhost:7169/health
curl.exe -sS -o NUL -w "%{http_code}`n" https://localhost:7169/swagger/index.html
curl.exe -sS https://localhost:7169/api/settings/operating-hours
docker compose --env-file docker-compose/.env -f docker-compose/docker-compose.yml -f docker-compose/docker-compose.override.yml logs --tail 100 api
```

Expected responses are `200` for health, Swagger, and operating hours. Swagger is available only when the Compose override selects Development. Check `docker compose ps` for the API's `(healthy)` status and the logs for Kestrel listening on `http://[::]:8080` and `https://[::]:8443`.

URLs:

- API: <https://localhost:7169>
- Swagger UI: <https://localhost:7169/swagger>
- OpenAPI JSON: <https://localhost:7169/openapi/v1.json>

Stop the containers without deleting persistent volumes with:

```powershell
docker compose --env-file docker-compose/.env -f docker-compose/docker-compose.yml -f docker-compose/docker-compose.override.yml down
```
