# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app

# Copy solution and project files
COPY SPTarkov.LFS.sln ./
COPY SPTarkov.LFS/SPTarkov.LFS.csproj SPTarkov.LFS/

# Restore dependencies
RUN dotnet restore

# Copy source code
COPY . .

# Build and publish
RUN dotnet publish SPTarkov.LFS/SPTarkov.LFS.csproj -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Copy published app
COPY --from=build /app/publish .

# Create directory for LFS storage
RUN mkdir -p /app/data

# Start the application
ENTRYPOINT ["dotnet", "SPTarkov.LFS.dll"]
