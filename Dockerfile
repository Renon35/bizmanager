# ── Stage 1: Build ───────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore NuGet packages
COPY BizManager/BizManager.csproj BizManager/
RUN dotnet restore BizManager/BizManager.csproj

# Copy everything and publish
COPY BizManager/ BizManager/
RUN dotnet publish BizManager/BizManager.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ── Stage 2: Runtime ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Install libicu (needed for .NET globalization) and curl (healthcheck)
RUN apt-get update && apt-get install -y --no-install-recommends \
    curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# Railway dynamically sets PORT; ASP.NET Core reads it via our Program.cs
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

ENTRYPOINT ["dotnet", "BizManager.dll"]
