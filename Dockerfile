FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build-env
WORKDIR /app

COPY *.sln .
COPY src/DiscordMultiBot.App/*.csproj ./src/DiscordMultiBot.App/
COPY src/DiscordMultiBot.PollAPI/*.csproj ./src/DiscordMultiBot.PollAPI/
COPY src/DiscordMultiBot.AudioParser/*.csproj ./src/DiscordMultiBot.AudioParser/
RUN dotnet restore

COPY src ./src
RUN dotnet publish ./src/DiscordMultiBot.App -c Release --no-restore -o ./publish

FROM mcr.microsoft.com/dotnet/runtime:6.0
WORKDIR /app

ENV BOT_ENV=DEV
ENV AUDIOS_PATH=./audio
ENV SQLITE_PATH=./db

COPY --from=build-env /app/publish .

CMD dotnet DiscordMultiBot.App.dll   