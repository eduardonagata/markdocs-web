# syntax=docker/dockerfile:1.7

# ---- 1. O frontend (frontend/dist) ----
FROM node:22-alpine AS frontend
WORKDIR /src/frontend
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

# ---- 2. O backend ----
# O motor Typst (MarkDocs.Core/Artefatos/runtimes/linux-x64) já vem compilado
# no repositório: a imagem não precisa de Rust. O target BuildFrontend do Host
# pula o npm porque frontend/dist já existe.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend
WORKDIR /src
COPY MarkDocs.Core/ MarkDocs.Core/
COPY src/ src/
RUN dotnet restore src/MarkDocs/MarkDocs.csproj
COPY --from=frontend /src/frontend/dist /src/frontend/dist
RUN dotnet publish src/MarkDocs/MarkDocs.csproj -c Release -o /out --no-restore

# ---- 3. O runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=backend /out ./

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "MarkDocs.dll"]
