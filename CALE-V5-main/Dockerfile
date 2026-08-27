# syntax=docker/dockerfile:1

# --- Frontend ---
FROM node:20-alpine AS frontend
WORKDIR /src/frontend
COPY frontend/package.json frontend/package-lock.json* ./
RUN npm ci
COPY frontend/ ./
RUN npx ng build --configuration=production

# --- Backend ---
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Cale.sln ./
COPY src/ ./src/
COPY tests/ ./tests/
RUN dotnet restore src/Cale.Api/Cale.Api.csproj
RUN dotnet publish src/Cale.Api/Cale.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# Copy Angular browser output into API wwwroot (same-origin SPA)
COPY --from=frontend /src/frontend/dist/frontend/browser/ /app/publish/wwwroot/

# --- Runtime ---
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false
ENV ASPNETCORE_hostBuilder__reloadConfigOnChange=false
EXPOSE 8080
RUN mkdir -p /data /app/wwwroot/uploads
COPY --from=build /app/publish .
VOLUME ["/data", "/app/wwwroot/uploads"]
ENTRYPOINT ["dotnet", "Cale.Api.dll"]
