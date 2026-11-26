# Runtime image: ASP.NET Core 10, running as the built-in non-root app user.
FROM mcr.microsoft.com/dotnet/aspnet:10.0.12 AS base
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080 8443
USER 0
RUN apt-get update \
    && apt-get install --no-install-recommends -y curl \
    && rm -rf /var/lib/apt/lists/*
HEALTHCHECK --interval=30s --timeout=5s --start-period=90s --retries=5 \
    CMD curl --fail --insecure --silent --show-error --max-time 3 https://127.0.0.1:8443/health || exit 1
USER $APP_UID

# Build from the solution root so shared props and every referenced project are available.
FROM mcr.microsoft.com/dotnet/sdk:10.0.401 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["global.json", "NuGet.Config", "Directory.Build.props", "Directory.Packages.props", "./"]
COPY ["src/Contracts/MechanicShop.Contracts.csproj", "src/Contracts/"]
COPY ["src/MechanicShop.Api/MechanicShop.Api.csproj", "src/MechanicShop.Api/"]
COPY ["src/MechanicShop.Application/MechanicShop.Application.csproj", "src/MechanicShop.Application/"]
COPY ["src/MechanicShop.Client/MechanicShop.Client.csproj", "src/MechanicShop.Client/"]
COPY ["src/MechanicShop.Domain/MechanicShop.Domain.csproj", "src/MechanicShop.Domain/"]
COPY ["src/MechanicShop.Infrastructure/MechanicShop.Infrastructure.csproj", "src/MechanicShop.Infrastructure/"]
RUN dotnet restore "src/MechanicShop.Api/MechanicShop.Api.csproj"

COPY . .
RUN dotnet publish "src/MechanicShop.Api/MechanicShop.Api.csproj" \
    --configuration "$BUILD_CONFIGURATION" \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build --chown=$APP_UID:$APP_UID /app/publish .
ENTRYPOINT ["dotnet", "MechanicShop.Api.dll"]
