FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/VulnTracker/VulnTracker.csproj src/VulnTracker/
RUN dotnet restore src/VulnTracker/VulnTracker.csproj
COPY src/VulnTracker/ src/VulnTracker/
RUN dotnet publish src/VulnTracker/VulnTracker.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
# Built-in non-root user in the .NET images.
USER $APP_UID
ENTRYPOINT ["dotnet", "VulnTracker.dll"]
