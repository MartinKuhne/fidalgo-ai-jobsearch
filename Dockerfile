# Stage 1: Build React SPA
FROM node:22-alpine AS frontend-build
WORKDIR /frontend
COPY src/Fidalgo.Frontend/package.json src/Fidalgo.Frontend/package-lock.json ./
RUN npm ci
COPY src/Fidalgo.Frontend/ ./
RUN npm run build

# Stage 2: Restore & publish .NET services
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dotnet-build
WORKDIR /src
COPY src/Directory.Build.props ./src/
COPY src/Fidalgo.slnx ./src/
COPY src/Fidalgo.Shared/Fidalgo.Shared.csproj ./src/Fidalgo.Shared/
COPY src/Fidalgo.Host/Fidalgo.Host.csproj ./src/Fidalgo.Host/
COPY src/Fidalgo.Agent/Fidalgo.Agent.csproj ./src/Fidalgo.Agent/
COPY src/Fidalgo.Ingest/Fidalgo.Ingest.csproj ./src/Fidalgo.Ingest/
COPY src/Fidalgo.Shared.Tests/Fidalgo.Shared.Tests.csproj ./src/Fidalgo.Shared.Tests/
COPY src/Fidalgo.Host.Tests/Fidalgo.Host.Tests.csproj ./src/Fidalgo.Host.Tests/
COPY src/Fidalgo.Agent.Tests/Fidalgo.Agent.Tests.csproj ./src/Fidalgo.Agent.Tests/
COPY src/Fidalgo.Ingest.Tests/Fidalgo.Ingest.Tests.csproj ./src/Fidalgo.Ingest.Tests/
RUN dotnet restore src/Fidalgo.slnx

COPY src/Fidalgo.Shared/ ./src/Fidalgo.Shared/
COPY src/Fidalgo.Host/ ./src/Fidalgo.Host/
COPY src/Fidalgo.Agent/ ./src/Fidalgo.Agent/
COPY src/Fidalgo.Ingest/ ./src/Fidalgo.Ingest/

RUN dotnet publish src/Fidalgo.Host/Fidalgo.Host.csproj -c Release -o /app/host \
      --no-restore -p:PublishSingleFile=false -p:SelfContained=false -p:RuntimeIdentifier= -p:UseAppHost=false && \
    dotnet publish src/Fidalgo.Agent/Fidalgo.Agent.csproj -c Release -o /app/agent --no-restore && \
    dotnet publish src/Fidalgo.Ingest/Fidalgo.Ingest.csproj -c Release -o /app/ingest --no-restore

# Target: Web Host + Frontend SPA
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS host
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    FIDALGO_PROFILE_CONFIG_PATH=/data/config/config.json \
    DatabasePath=/data/db/jobs.db \
    FIDALGO_LOG_DIR=/data/logs \
    ApplicationPacket__ProfileDirectory=/data/profile \
    ApplicationPacket__OutputDirectory=/data/applications
COPY --from=dotnet-build /app/host ./
COPY --from=frontend-build /Fidalgo.Host/wwwroot ./wwwroot
RUN mkdir -p /data/config /data/profile /data/applications /data/db /data/logs
EXPOSE 8080
ENTRYPOINT ["dotnet", "Fidalgo.Host.dll"]

# Target: Evaluation & Packet Generation CLI/Batch Worker
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS agent
WORKDIR /app
ENV DOTNET_RUNNING_IN_CONTAINER=true \
    FIDALGO_PROFILE_CONFIG_PATH=/data/config/config.json \
    DatabasePath=/data/db/jobs.db \
    FIDALGO_LOG_DIR=/data/logs \
    ApplicationPacket__ProfileDirectory=/data/profile \
    ApplicationPacket__OutputDirectory=/data/applications
COPY --from=dotnet-build /app/agent ./
RUN mkdir -p /data/config /data/profile /data/applications /data/db /data/logs
ENTRYPOINT ["dotnet", "Fidalgo.Agent.dll"]

# Target: Job Ingestion CLI/Batch Worker
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS ingest
WORKDIR /app
ENV DOTNET_RUNNING_IN_CONTAINER=true \
    FIDALGO_PROFILE_CONFIG_PATH=/data/config/config.json \
    DatabasePath=/data/db/jobs.db \
    FIDALGO_LOG_DIR=/data/logs
COPY --from=dotnet-build /app/ingest ./
RUN mkdir -p /data/config /data/db /data/logs
ENTRYPOINT ["dotnet", "Fidalgo.Ingest.dll"]
