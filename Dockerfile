FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src

COPY . .
RUN dotnet restore ./EldenBingoServerStandalone/EldenBingoServerStandalone.csproj -p:CheckEolTargetFramework=false
RUN dotnet publish ./EldenBingoServerStandalone/EldenBingoServerStandalone.csproj -c Release -o /app/publish --no-restore -p:CheckEolTargetFramework=false

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENV ELDENBINGO_PORT=4501
ENV ELDENBINGO_SERVERDATA_PATH=/data/server/serverData.json
ENV ELDENBINGO_MATCHLOG_DIR=/data/matchlogs
ENV ELDENBINGO_BIND_ADDRESS=0.0.0.0

VOLUME ["/data/server", "/data/matchlogs"]
EXPOSE 4501/tcp

ENTRYPOINT ["sh", "-c", "dotnet EldenBingoServerStandalone.dll --port=${ELDENBINGO_PORT} --serverdata=${ELDENBINGO_SERVERDATA_PATH} --matchlog --matchlogdir=${ELDENBINGO_MATCHLOG_DIR} --bindaddress=${ELDENBINGO_BIND_ADDRESS}"]
