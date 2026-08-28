# syntax=docker/dockerfile:1
# Contexto de build é a raiz da solution:
#   docker build -f Prumo.Notifications/Dockerfile -t prumo-notifications .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore Prumo.Notifications/Prumo.Notifications.csproj
RUN dotnet publish Prumo.Notifications/Prumo.Notifications.csproj \
    -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
# Runtime e não aspnet: o serviço consome fila, não atende HTTP. Sem porta exposta, e no
# Container App ele escala a zero pela profundidade da fila (KEDA), não por requisição.
ENTRYPOINT ["dotnet", "Prumo.Notifications.dll"]
