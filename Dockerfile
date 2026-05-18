FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

WORKDIR /src

COPY ["PagueVeloz.sln", "./"]
COPY ["PagueVeloz.API/PagueVeloz.API.csproj", "PagueVeloz.API/"]
COPY ["PagueVeloz.Domain/PagueVeloz.Domain.csproj", "PagueVeloz.Domain/"]
COPY ["PagueVeloz.Application/PagueVeloz.Application.csproj", "PagueVeloz.Application/"]
COPY ["PagueVeloz.Infrastructure/PagueVeloz.Infrastructure.csproj", "PagueVeloz.Infrastructure/"]
COPY ["PagueVeloz.Tests/PagueVeloz.Tests.csproj", "PagueVeloz.Tests/"]

RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore "PagueVeloz.sln"

COPY . .

RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish "PagueVeloz.API/PagueVeloz.API.csproj" \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime

WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "PagueVeloz.API.dll"]
