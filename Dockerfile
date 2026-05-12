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
COPY ["credentials.json", "/app/credentials.json"]

ENV ASPNETCORE_URLS=http://+:8000
EXPOSE 8000

ENTRYPOINT ["dotnet", "rag-a-muffin.dll"]
