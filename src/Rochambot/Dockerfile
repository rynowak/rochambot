FROM mcr.microsoft.com/dotnet/core/aspnet:3.0-buster-slim AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/core/sdk:3.0-buster AS build
WORKDIR /src
COPY ["src/Rochambot/Rochambot.csproj", "src/Rochambot/"]
RUN dotnet restore "src/Rochambot/Rochambot.csproj"
COPY . .
WORKDIR "/src/src/Rochambot"
RUN dotnet build "Rochambot.csproj" -c Release -o /app

FROM build AS publish
RUN dotnet publish "Rochambot.csproj" -c Release -o /app

FROM base AS final
WORKDIR /app
COPY --from=publish /app .
ENTRYPOINT ["dotnet", "Rochambot.dll"]