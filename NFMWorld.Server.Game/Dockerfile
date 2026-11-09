FROM mcr.microsoft.com/dotnet/sdk:11.0 AS build
WORKDIR /src
# The build needs every submodule (CasCore, MemoryPack, FixedMathSharp, ...). `docker build`
# copies files, not git metadata, so clone/submodule first: git clone --recurse-submodules.
COPY . .
RUN dotnet publish NFMWorld.Server.Game/NFMWorld.Server.Game.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/runtime:11.0
WORKDIR /app
# The gamemode and AI plugins publish into data/gamemodes and data/ais beside the exe; the
# game master loads them by path, so the whole tree has to come across.
COPY --from=build /app .
ENV GM_GAME_PORT=7002
ENV GM_HTTP_ENDPOINT="http://+:7003/"
EXPOSE 7002/udp
EXPOSE 7003
ENTRYPOINT ["dotnet", "NFMWorld.Server.Game.dll"]
