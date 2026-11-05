FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY Backend/BlueHarvest.PricingExceptions/src/PricingExceptions.API/PricingExceptions.API.csproj ./
RUN dotnet restore PricingExceptions.API.csproj
COPY Backend/BlueHarvest.PricingExceptions/src/PricingExceptions.API/ ./
RUN dotnet publish PricingExceptions.API.csproj -c Release --no-restore -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish ./
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "PricingExceptions.API.dll"]
