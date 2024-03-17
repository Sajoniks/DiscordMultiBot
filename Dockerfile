FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build-env
WORKDIR /app

FROM build-env AS tools
WORKDIR /tools/src

ARG AUDIOPARSER_NAME=DiscordMultiBot.AudioParser

COPY src/$AUDIOPARSER_NAME/*.csproj ./$AUDIOPARSER_NAME/
RUN dotnet restore ./$AUDIOPARSER_NAME

COPY src/$AUDIOPARSER_NAME ./$AUDIOPARSER_NAME

RUN dotnet publish ./$AUDIOPARSER_NAME -c Release -o ../audioparser --no-restore

FROM build-env AS apps
WORKDIR /app/src

ARG POLLAPI_NAME=DiscordMultiBot.PollAPI
ARG APP_NAME=DiscordMultiBot.App

COPY src/$APP_NAME/*.csproj ./$APP_NAME/
COPY src/$POLLAPI_NAME/*.csproj ./$POLLAPI_NAME/

RUN dotnet restore ./$APP_NAME     && \
    dotnet restore ./$POLLAPI_NAME

COPY src/$APP_NAME      ./$APP_NAME
COPY src/$POLLAPI_NAME  ./$POLLAPI_NAME

FROM apps AS publish

ARG APP_NAME=DiscordMultiBot.App

COPY --from=tools /tools /tools

WORKDIR /app/src

RUN dotnet publish ./$APP_NAME -c Release --no-restore -o ../publish && \
    dotnet /tools/audioparser/audioparser.dll -i /app/src/$APP_NAME/Audios -o /app/publish/Configuration/audiosettings.json --base-path /app/audios --namespace Bot:Audio

FROM mcr.microsoft.com/dotnet/runtime:6.0 as runtime
WORKDIR /app

COPY --from=publish /app/publish .
COPY --from=mwader/static-ffmpeg:6.1.1 /ffmpeg /usr/local/bin/

ENV BOT_ENV=DEV
ENV AUDIOS_PATH=./audios
ENV SQLITE_PATH=./db

RUN apt-get update && \
    apt-get install -y --no-install-recommends \
      libopus-dev \
      libsodium-dev \

CMD dotnet DiscordMultiBot.App.dll   