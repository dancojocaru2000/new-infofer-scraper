# https://hub.docker.com/_/microsoft-dotnet
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /source

# copy csproj and restore as distinct layers
COPY *.sln .
COPY server/*.csproj ./server/
COPY scraper/*.csproj ./scraper/
COPY ConsoleTest/*.csproj ./ConsoleTest/
RUN dotnet restore

# copy everything else and build app
COPY server/. ./server/
COPY scraper/. ./scraper/
COPY ConsoleTest/. ./ConsoleTest/
WORKDIR /source/server
RUN dotnet publish -c release -o /app --no-restore

# final stage/image
FROM mcr.microsoft.com/dotnet/aspnet:6.0
WORKDIR /app
COPY --from=build /app ./
ENTRYPOINT ["dotnet", "Server.dll"]
