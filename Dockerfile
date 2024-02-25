FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build-env
WORKDIR /app

FROM build-env AS tools
WORKDIR /tools/src

ARG AudioParser=DiscordMultiBot.AudioParser

COPY src/$AudioParser/*.csproj ./$AudioParser/
RUN dotnet restore ./$AudioParser

COPY src/$AudioParser ./$AudioParser
RUN dotnet publish ./$AudioParser --no-restore -c Release -o /tools/audioparser

FROM build-env AS apps
WORKDIR /app/src

ARG PollAPI=DiscordMultiBot.PollAPI
ARG App=DiscordMultiBot.App

COPY --from=tools /tools /tools

COPY src/$App/*.csproj     ./$App/
COPY src/$PollAPI/*.csproj ./$PollAPI/

RUN dotnet restore ./$App && \
    dotnet restore ./$PollAPI

COPY src/$App     ./$App
COPY src/$PollAPI ./$PollAPI
COPY src/$App/Audios    /app/audio
COPY src/$App/Databases /app/db

RUN dotnet /tools/audioparser/audioparser.dll -i /app/audio -o /app/audio/audiosettings.json --namespace Bot:Audio &&  \
    dotnet build ./$PollAPI -c Release -o /app/build &&  \
    dotnet build ./$App     -c Release -o /app/build 

FROM apps AS publish
WORKDIR /app

ARG App=DiscordMultiBot.App

RUN dotnet publish ./src/$App -c Release --no-restore -o ./publish
COPY --from=apps /app/audio/audiosettings.json ./publish/Configuration/

FROM mcr.microsoft.com/dotnet/runtime:6.0 as runtime
WORKDIR /app

COPY --from=publish /app/publish .

ENV BOT_ENV=DEV
ENV AUDIOS_PATH=./audio
ENV SQLITE_PATH=./db

RUN --mount=target=/var/lib/apt/lists,type=cache,sharing=locked \
    --mount=target=/var/cache/apt,type=cache,sharing=locked \
    rm -f /etc/apt/apt.conf.d/docker-clean && \
    apt update &&  \
    apt install -y --no-install-recommends \
      ffmpeg \
      libopus-dev \
      libsodium-dev

CMD dotnet DiscordMultiBot.App.dll   