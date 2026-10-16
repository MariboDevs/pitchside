# pitchside-api. Build context is the repository root: the projects embed data/calibration/targets.json and
# tests/fixtures/scripted, and read global.json, Directory.Build.props and Directory.Packages.props.
#   docker build -f src/Pitchside.Api/Dockerfile -t pitchside/api .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY data/calibration/ data/calibration/
COPY tests/fixtures/ tests/fixtures/
COPY src/ src/
RUN dotnet publish src/Pitchside.Api/Pitchside.Api.csproj --configuration Release --output /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .
# The aspnet image listens on 8080 (ASPNETCORE_HTTP_PORTS) and defines the non-root APP_UID.
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Pitchside.Api.dll"]
