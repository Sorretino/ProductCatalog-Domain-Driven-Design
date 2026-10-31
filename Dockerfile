FROM mcr.microsoft.com/dotnet/sdk:10.0.403 AS build
WORKDIR /source
COPY . .
RUN dotnet restore src/ProductCatalog.Api/ProductCatalog.Api.csproj --locked-mode \
    && dotnet publish src/ProductCatalog.Api/ProductCatalog.Api.csproj \
       --no-restore -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.14 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "ProductCatalog.Api.dll"]
