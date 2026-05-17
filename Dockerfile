# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS builder
WORKDIR /src

COPY ["rag-a-muffin/rag-a-muffin.csproj", "rag-a-muffin/"]
RUN dotnet restore "rag-a-muffin/rag-a-muffin.csproj"

COPY . .
RUN dotnet build "rag-a-muffin/rag-a-muffin.csproj" -c Release -o /app/build

# Publish stage
FROM builder AS publish
RUN dotnet publish "rag-a-muffin/rag-a-muffin.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=publish /app/publish .

# Docker CLI — needed for the restart/rebuild endpoints
RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates curl gnupg \
    && install -m 0755 -d /etc/apt/keyrings \
    && curl -fsSL https://download.docker.com/linux/debian/gpg \
         | gpg --dearmor -o /etc/apt/keyrings/docker.gpg \
    && chmod a+r /etc/apt/keyrings/docker.gpg \
    && echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] \
         https://download.docker.com/linux/debian \
         $(. /etc/os-release && echo "$VERSION_CODENAME") stable" \
         > /etc/apt/sources.list.d/docker.list \
    && apt-get update \
    && apt-get install -y --no-install-recommends docker-ce-cli docker-compose-plugin \
    && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_URLS=http://+:8000
EXPOSE 8000

RUN mkdir -p /app/data/tokens /app/data/uploads /app/data/watch

ENTRYPOINT ["dotnet", "rag-a-muffin.dll"]
