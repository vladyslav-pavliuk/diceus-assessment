# syntax=docker/dockerfile:1
# One Dockerfile, three targets:
#   runtime  - the API image deployed to Azure Container Apps (default, last stage)
#   migrator - an EF Core migrations bundle; runs `dotnet ef database update` without the SDK
# Both are built from the same restore/build, so the migrations always match the API.

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Restore first, from project files only, so the layer is cached until a dependency changes.
COPY global.json Directory.Build.props Directory.Packages.props dotnet-tools.json ./
COPY src/ClaimsModule.Domain/ClaimsModule.Domain.csproj src/ClaimsModule.Domain/
COPY src/ClaimsModule.Application/ClaimsModule.Application.csproj src/ClaimsModule.Application/
COPY src/ClaimsModule.Infrastructure/ClaimsModule.Infrastructure.csproj src/ClaimsModule.Infrastructure/
COPY src/ClaimsModule.Persistence/ClaimsModule.Persistence.csproj src/ClaimsModule.Persistence/
COPY src/ClaimsModule.API/ClaimsModule.API.csproj src/ClaimsModule.API/
RUN dotnet restore src/ClaimsModule.API/ClaimsModule.API.csproj && dotnet tool restore

COPY src/ src/
RUN dotnet publish src/ClaimsModule.API/ClaimsModule.API.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM build AS migrations-bundle
RUN dotnet ef migrations bundle \
      --project src/ClaimsModule.Persistence \
      --startup-project src/ClaimsModule.API \
      --configuration Release \
      --output /app/efbundle

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS migrator
WORKDIR /app
COPY --from=migrations-bundle /app/efbundle .
USER $APP_UID
# The connection string comes from ConnectionStrings__ClaimsDb.
ENTRYPOINT ["./efbundle"]

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
# Non-root user shipped with the .NET images; the app listens on 8080 (ASPNETCORE_HTTP_PORTS).
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "ClaimsModule.API.dll"]
