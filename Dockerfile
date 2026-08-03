# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

# Copy solution and project files
COPY SPTushonka.LFS.slnx ./
COPY SPTushonka.LFS/SPTushonka.LFS.csproj SPTushonka.LFS/

# Restore dependencies
RUN dotnet restore

# Copy source code
COPY . .

# Build and publish
RUN dotnet publish SPTushonka.LFS/SPTushonka.LFS.csproj -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Copy published app
COPY --from=build /app/publish .

# Create directory for LFS storage
RUN mkdir -p /app/data

# Expose port 5420
EXPOSE 5420

# Start the application
ENTRYPOINT ["dotnet", "SPTushonka.LFS.dll"]
