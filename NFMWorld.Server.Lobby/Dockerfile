FROM mcr.microsoft.com/dotnet/sdk:11.0 AS build
WORKDIR /src
# The build needs every submodule (CasCore, MemoryPack, FixedMathSharp, ...). `docker build`
# copies files, not git metadata, so clone/submodule first: git clone --recurse-submodules.
COPY . .
RUN dotnet publish NFMWorld.Server.Lobby/NFMWorld.Server.Lobby.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/runtime:11.0
WORKDIR /app
COPY --from=build /app .
ENV LOBBY_HTTP_ENDPOINT="http://+:7001/"
EXPOSE 7001
ENTRYPOINT ["dotnet", "NFMWorld.Server.Lobby.dll"]
