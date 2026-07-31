# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first so Docker can cache the NuGet layer.
COPY global.json ./
COPY DaftechCrm.sln ./
COPY src/DaftechCrm.Domain/DaftechCrm.Domain.csproj src/DaftechCrm.Domain/
COPY src/DaftechCrm.Application/DaftechCrm.Application.csproj src/DaftechCrm.Application/
COPY src/DaftechCrm.Infrastructure/DaftechCrm.Infrastructure.csproj src/DaftechCrm.Infrastructure/
COPY src/DaftechCrm.Api/DaftechCrm.Api.csproj src/DaftechCrm.Api/
RUN dotnet restore src/DaftechCrm.Api/DaftechCrm.Api.csproj

COPY . .
RUN dotnet publish src/DaftechCrm.Api/DaftechCrm.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

# Uploads and logs live on a mounted disk when one is attached (see render.yaml).
RUN mkdir -p /var/data/uploads /var/data/logs
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://0.0.0.0:8080 \
    Storage__RootPath=/var/data/uploads \
    LOG_DIR=/var/data/logs \
    DOTNET_gcServer=0

EXPOSE 8080
ENTRYPOINT ["dotnet", "DaftechCrm.Api.dll"]
