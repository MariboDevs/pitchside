# pitchside-agents. Build context is the repository root: the projects embed tests/fixtures/scripted and read
# global.json, Directory.Build.props and Directory.Packages.props.
#   docker build -f src/Pitchside.Agents.Host/Dockerfile -t pitchside/agents .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY data/calibration/ data/calibration/
COPY tests/fixtures/ tests/fixtures/
COPY src/ src/
RUN dotnet publish src/Pitchside.Agents.Host/Pitchside.Agents.Host.csproj --configuration Release --output /app

# The aspnet runtime, not the plain one: ServiceDefaults references the ASP.NET Core shared framework.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENTRYPOINT ["dotnet", "Pitchside.Agents.Host.dll"]
