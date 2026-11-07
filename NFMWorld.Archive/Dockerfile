FROM mcr.microsoft.com/dotnet/sdk:11.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish NFMWorld.Archive/NFMWorld.Archive.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:11.0
WORKDIR /app
COPY --from=build /app .
# Database and uploads live here; mount a volume at /data.
ENV Archive__Database="Data Source=/data/archive.db"
ENV Archive__StorageRoot="/data/storage"
VOLUME /data
EXPOSE 8080
ENTRYPOINT ["dotnet", "NFMWorld.Archive.dll"]
