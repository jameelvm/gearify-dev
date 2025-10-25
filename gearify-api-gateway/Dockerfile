FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY gearify-api-gateway/*.csproj ./gearify-api-gateway/
RUN dotnet restore gearify-api-gateway/Gearify.ApiGateway.csproj
COPY gearify-api-gateway/. ./gearify-api-gateway/
WORKDIR /src/gearify-api-gateway
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Gearify.ApiGateway.dll"]
