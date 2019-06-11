FROM mcr.microsoft.com/dotnet/core/aspnet:3.0-buster-slim AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/core/sdk:3.0-buster AS build
WORKDIR /src
COPY ["src/GameMaster/GameMaster.csproj", "src/GameMaster/"]
RUN dotnet restore "src/GameMaster/GameMaster.csproj"
COPY . .
WORKDIR "/src/src/GameMaster"
RUN dotnet build "GameMaster.csproj" -c Release -o /app

FROM build AS publish
RUN dotnet publish "GameMaster.csproj" -c Release -o /app

FROM base AS final
WORKDIR /app
COPY --from=publish /app .
ENTRYPOINT ["dotnet", "GameMaster.dll"]