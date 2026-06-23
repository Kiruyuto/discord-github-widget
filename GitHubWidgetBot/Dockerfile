FROM mcr.microsoft.com/dotnet/runtime:10.0.9 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0.301 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Directory.Packages.props", "./"]
COPY ["Directory.Build.props", "./"]
COPY ["GitHubWidgetBot/GitHubWidgetBot.csproj", "GitHubWidgetBot/"]
RUN dotnet restore "GitHubWidgetBot/GitHubWidgetBot.csproj"
COPY . .
WORKDIR "/src/GitHubWidgetBot"
RUN dotnet build "./GitHubWidgetBot.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./GitHubWidgetBot.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "GitHubWidgetBot.dll"]
