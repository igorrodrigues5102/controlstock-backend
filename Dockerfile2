FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["ControlStockBackend.csproj", "./"]
RUN dotnet restore "./ControlStockBackend.csproj"
COPY . .
RUN dotnet publish "ControlStockBackend.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ControlStockBackend.dll"]
