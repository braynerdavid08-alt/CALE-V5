# syntax=docker/dockerfile:1
# Repo root Dockerfile for hosts that build from the repository root
# (project sources live under CALE-V5-main/).

# --- Frontend ---
FROM node:20-alpine AS frontend
WORKDIR /src/frontend
COPY CALE-V5-main/frontend/package.json CALE-V5-main/frontend/package-lock.json* ./
RUN npm ci
COPY CALE-V5-main/frontend/ ./
RUN npx ng build --configuration=production

# --- Backend ---
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY CALE-V5-main/Cale.sln ./
COPY CALE-V5-main/src/ ./src/
COPY CALE-V5-main/tests/ ./tests/
RUN dotnet restore src/Cale.Api/Cale.Api.csproj
RUN dotnet publish src/Cale.Api/Cale.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# Copy Angular browser output into API wwwroot (same-origin SPA)
COPY --from=frontend /src/frontend/dist/frontend/browser/ /app/publish/wwwroot/

# --- Runtime ---
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
ENV ASPNETCORE_URLS=
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false
ENV ASPNETCORE_hostBuilder__reloadConfigOnChange=false
EXPOSE 8080
RUN mkdir -p /data /app/wwwroot/uploads
COPY --from=build /app/publish .
VOLUME ["/data", "/app/wwwroot/uploads"]
ENTRYPOINT ["dotnet", "Cale.Api.dll"]
