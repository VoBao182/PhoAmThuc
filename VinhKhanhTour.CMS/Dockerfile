FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY VinhKhanhTour.API/VinhKhanhTour.API.csproj VinhKhanhTour.API/
COPY VinhKhanhTour.CMS/VinhKhanhTour.CMS.csproj VinhKhanhTour.CMS/
RUN dotnet restore VinhKhanhTour.CMS/VinhKhanhTour.CMS.csproj

COPY . .
WORKDIR /src/VinhKhanhTour.CMS
RUN dotnet publish VinhKhanhTour.CMS.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://0.0.0.0:10000

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "VinhKhanhTour.CMS.dll"]
