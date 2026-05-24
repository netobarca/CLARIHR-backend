# syntax=docker/dockerfile:1
#
# CLARIHR API container image.
#
# §4.1 (technical-debt doc 01): this Dockerfile versions the packaging/deploy
# mechanism (previously absent) and provisions the native dependency QuestPDF
# needs to render PDFs on Linux. The PDF typeface (Lato) ships embedded inside
# the QuestPDF NuGet package and is copied into the publish output, so NO system
# font package is required — only the fontconfig native library below.
#
# Debian-based images are used because SkiaSharp (QuestPDF's rendering backend)
# resolves its glibc native assets there by default. For an Alpine variant, swap
# the base tags to `-alpine` and replace the apt-get block with:
#   RUN apk add --no-cache fontconfig

# ---------- Build stage ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Solution-wide build config first (Central Package Management + shared props),
# so the restore layer is reused while only sources change.
COPY Directory.Build.props Directory.Packages.props ./
COPY src/ src/

RUN dotnet restore src/CLARIHR.Api/CLARIHR.Api.csproj

# Framework-dependent publish; the runtime image already carries the .NET runtime.
# UseAppHost=false skips the native launcher — we start via `dotnet CLARIHR.Api.dll`.
RUN dotnet publish src/CLARIHR.Api/CLARIHR.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    -p:UseAppHost=false

# ---------- Runtime stage ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# QuestPDF renders via SkiaSharp, which requires the native fontconfig library on
# Linux even when fonts are embedded. Without it the worker throws on the first
# PDF render. No OS font package is installed: Lato travels embedded in the
# published QuestPDF assets (see §4.1 comment above).
RUN apt-get update \
    && apt-get install -y --no-install-recommends libfontconfig1 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish ./

# Serilog writes to a relative `logs/` directory (LoggingConfigurationExtensions).
# Create it and hand ownership to the non-root runtime user so logging works
# without running the container as root.
RUN mkdir -p /app/logs && chown -R $APP_UID /app/logs
USER $APP_UID

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "CLARIHR.Api.dll"]
