# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY CLARIHR.slnx ./
COPY Directory.Build.props ./
COPY Directory.Packages.props ./
COPY src/CLARIHR.Api/CLARIHR.Api.csproj src/CLARIHR.Api/
COPY src/CLARIHR.Application/CLARIHR.Application.csproj src/CLARIHR.Application/
COPY src/CLARIHR.Domain/CLARIHR.Domain.csproj src/CLARIHR.Domain/
COPY src/CLARIHR.Infrastructure/CLARIHR.Infrastructure.csproj src/CLARIHR.Infrastructure/

RUN dotnet restore src/CLARIHR.Api/CLARIHR.Api.csproj

COPY src ./src

RUN dotnet publish src/CLARIHR.Api/CLARIHR.Api.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish ./

ENTRYPOINT ["dotnet", "CLARIHR.Api.dll"]
