FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/MikanScan.ConsoleApp/MikanScan.ConsoleApp.csproj", "src/MikanScan.ConsoleApp/"]
RUN dotnet restore "src/MikanScan.ConsoleApp/MikanScan.ConsoleApp.csproj"
COPY . .
WORKDIR "/src/src/MikanScan.ConsoleApp"
RUN dotnet build "MikanScan.ConsoleApp.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "MikanScan.ConsoleApp.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MikanScan.ConsoleApp.dll"]
