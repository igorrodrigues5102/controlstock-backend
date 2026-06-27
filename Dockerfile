FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["ControlStockBackend.csproj", "./"]
RUN dotnet restore "./ControlStockBackend.csproj"
COPY . .
RUN dotnet publish "ControlStockBackend.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# --- CORREÇÃO AQUI ---
# Volta temporariamente para o root para criar a pasta e dar permissão
USER root
RUN mkdir -p /app/data && chown -R app:app /app/data

# Voltar para o usuário padrão do .NET antes de rodar o app
USER app
# ---------------------

ENTRYPOINT ["dotnet", "ControlStockBackend.dll"]
