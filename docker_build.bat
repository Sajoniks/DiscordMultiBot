@echo off

echo [Building docker image]
docker build -t discord-multibot %~dp0

echo [Saving to the archive]
echo Saving to %~dp0docker
mkdir %~dp0docker
cd %~dp0docker
docker save -o discord-multibot discord-multibot
docker scout cache prune -f
tar -czf discord-multibot.tar.gz discord-multibot
del discord-multibot