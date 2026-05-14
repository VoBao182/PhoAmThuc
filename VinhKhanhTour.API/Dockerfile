FROM mcr.microsoft.com/dotnet/sdk:10.0.203 AS build
WORKDIR /src

ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1 \
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

COPY global.json Directory.Build.props ./
COPY VinhKhanhTour.API/VinhKhanhTour.API.csproj VinhKhanhTour.API/
RUN dotnet restore VinhKhanhTour.API/VinhKhanhTour.API.csproj

COPY . .
WORKDIR /src/VinhKhanhTour.API
RUN dotnet publish VinhKhanhTour.API.csproj -c Release -o /app/publish --no-restore -m:1 /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.7 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://0.0.0.0:10000

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "VinhKhanhTour.API.dll"]
