# 4. Compose and Runtime Environments

Compose defines each service as an independent container. `depends_on` waits for SQL and Redis health checks before starting the API, while Seq and Collector rely only on container startup order. `restart: unless-stopped` automatically restarts a service after a crash or Docker Engine restart.

## Main Compose File and Override File

- `docker-compose/docker-compose.yml`: Defines services, networks, volumes, internal connections, and API port binding through configurable variables.
- `docker-compose/docker-compose.override.yml`: Exposes development ports on the host machine and sets `ASPNETCORE_ENVIRONMENT=Development`.
- `docker-compose/docker-compose.dcproj`: The Visual Studio project that runs Compose and loads `docker-compose/.env`.

The development environment uses the following default ports:

- **API:** 5106
- **SQL Server:** 1433
- **Redis:** 6379
- **Seq:** 5341
- **Prometheus:** 9090
- **Grafana:** 3000

These ports can be changed through `docker-compose/.env` when needed. SQL Server and Redis ports are not exposed to the host in the main Compose file.

## Compose Configuration

- `build.context: ..` makes the solution root available as the Docker build context.
- `dockerfile` points to `src/MechanicShop.Api/Dockerfile`, relative to the build context.
- `environment` passes .NET configuration settings to the application, using `__` as the equivalent of `:` in configuration keys.
- `expose` documents ports available to other services within the Docker network.
- `ports` publishes container ports to the development machine.
- Bind mount paths such as `./prometheus/prometheus.yml` are resolved relative to the main Compose file.
- `healthcheck` verifies the health of SQL Server, Redis, Prometheus, and the API.

The `.env` file is used for variable substitution inside the Compose YAML files. However, defining a variable in `.env` does not automatically make it an environment variable inside the container.

The `environment` section is responsible for passing configuration values to the container's application process.

There is no `env_file:` configuration in this setup, preventing all host environment variables from being automatically passed to the application.

## Running and Developing with Visual Studio 2026

```powershell
Set-Location docker-compose
docker compose config --quiet
docker compose up --build -d
docker compose ps
docker compose logs -f api
```

The Compose project has been registered in the solution and connects the API project to `docker-compose.dcproj` through `DockerComposeProjectPath`.

The `Properties/launchSettings.json` file configures Visual Studio to:

- Start the API in Debug mode.
- Start the remaining services without Debug mode.
- Open the `/health` endpoint automatically.

The `DependencyAwareStart` property enables health-check-aware startup in supported recent Visual Studio versions.

To run the application:

1. Set `docker-compose` as the Startup Project.
2. Make sure Docker Desktop is running.
3. Accept the required licenses.
4. Configure all required secrets.
5. Press **F5** to start debugging.
