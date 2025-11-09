FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project file
COPY gearify-api-gateway/Gearify.ApiGateway.csproj gearify-api-gateway/

# Restore dependencies
RUN dotnet restore gearify-api-gateway/Gearify.ApiGateway.csproj

# Copy all source files
COPY gearify-api-gateway/ gearify-api-gateway/

# Build the project
WORKDIR /src/gearify-api-gateway
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV ASPNETCORE_URLS=http://+:80
ENTRYPOINT ["dotnet", "Gearify.ApiGateway.dll"]
