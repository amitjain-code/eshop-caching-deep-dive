# Build context: repository root.  docker build -f src/Services/Basket/Basket.Api/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/Services/Basket/Basket.Api/Basket.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Basket.Api.dll"]
