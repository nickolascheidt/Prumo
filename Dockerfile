# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

# Copy project files
COPY ["BiomePampa.Api/BiomePampa.Api.csproj", "BiomePampa.Api/"]
COPY ["BiomePampa.Application/BiomePampa.Application.csproj", "BiomePampa.Application/"]
COPY ["BiomePampa.Domain/BiomePampa.Domain.csproj", "BiomePampa.Domain/"]
COPY ["BiomePampa.Infrastructure/BiomePampa.Infrastructure.csproj", "BiomePampa.Infrastructure/"]

# Restore dependencies
RUN dotnet restore "BiomePampa.Api/BiomePampa.Api.csproj"

# Copy everything else
COPY . .

# Build the application
WORKDIR "/src/BiomePampa.Api"
RUN dotnet build "BiomePampa.Api.csproj" -c Release -o /app/build

# Stage 2: Publish
FROM build AS publish
RUN dotnet publish "BiomePampa.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 3: Final Runtime Image
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS final
WORKDIR /app

# Create non-root user for security
RUN groupadd -r appuser && useradd -r -g appuser appuser

# Copy published files
COPY --from=publish /app/publish .

# Change ownership
RUN chown -R appuser:appuser /app

# Switch to non-root user
USER appuser

# Expose port
EXPOSE 8080

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD curl --fail http://localhost:8080/health/ready || exit 1

# Entry point
ENTRYPOINT ["dotnet", "BiomePampa.Api.dll"]
