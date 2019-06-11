FROM mcr.microsoft.com/dotnet/core/runtime:3.0-buster-slim AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/core/sdk:3.0-buster AS build
WORKDIR /src
COPY ["src/RobbyBot/RobbyBot.csproj", "src/RobbyBot/"]
RUN dotnet restore "src/RobbyBot/RobbyBot.csproj"
COPY . .
WORKDIR "/src/src/RobbyBot"
RUN dotnet build "RobbyBot.csproj" -c Release -o /app

FROM build AS publish
RUN dotnet publish "RobbyBot.csproj" -c Release -o /app

FROM base AS final
WORKDIR /app
COPY --from=publish /app .
ENTRYPOINT ["dotnet", "RobbyBot.dll"]