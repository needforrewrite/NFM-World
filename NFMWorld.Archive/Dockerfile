FROM mcr.microsoft.com/dotnet/sdk:11.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish NFMWorld.Archive/NFMWorld.Archive.csproj -c Release -o /app
# Uploads and time trials are validated out of process; publish it beside the app.
RUN dotnet publish NFMWorld.Archive.Validator/NFMWorld.Archive.Validator.csproj -c Release -o /validator
# Resolve shipped text stages with the validator's piece set before seeding the archive.
RUN /validator/NFMWorld.Archive.Validator stock-export /stock

FROM mcr.microsoft.com/dotnet/aspnet:11.0
WORKDIR /app
COPY --from=build /app .
COPY --from=build /validator /validator
COPY --from=build /stock /stock
# Database and uploads live here; mount a volume at /data.
ENV Archive__Database="Data Source=/data/archive.db"
ENV Archive__StorageRoot="/data/storage"
ENV Archive__ValidatorPath="/validator/NFMWorld.Archive.Validator"
ENV Archive__ValidatorWorkingDirectory="/validator"
ENV Archive__StockContentPath="/stock"
VOLUME /data
EXPOSE 8080
ENTRYPOINT ["dotnet", "NFMWorld.Archive.dll"]
