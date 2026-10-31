# syntax=docker/dockerfile:1.7
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and csproj files first (docker layer cache)
COPY *.sln ./
COPY src/ src/
COPY tests/ tests/

RUN dotnet restore

# Build and publish the WebApi project
ARG PROJECT=src/<Produto>.WebApi/<Produto>.WebApi.csproj
RUN dotnet publish ${PROJECT} -c Release -o /out --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /out ./

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "<Produto>.WebApi.dll"]
