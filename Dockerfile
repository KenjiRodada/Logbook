# The local project remains net9.0 for Visual Studio 2022 compatibility.
# Hosting builds the same source for .NET 10 LTS, which has a longer support window.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["Logbook.csproj", "./"]
RUN dotnet restore "Logbook.csproj" -p:TargetFramework=net10.0

COPY . .
RUN dotnet publish "Logbook.csproj" -c Release -o /app/publish \
    --no-restore -p:TargetFramework=net10.0 /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://0.0.0.0:10000
EXPOSE 10000
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Logbook.dll"]
