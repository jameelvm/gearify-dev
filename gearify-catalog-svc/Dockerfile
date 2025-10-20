FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5001

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy shared kernel project
COPY gearify-shared-kernel/Gearify.SharedKernel.csproj gearify-shared-kernel/
# Copy catalog service project
COPY gearify-catalog-svc/Gearify.CatalogService.csproj gearify-catalog-svc/

# Restore dependencies
RUN dotnet restore gearify-catalog-svc/Gearify.CatalogService.csproj

# Copy all source files
COPY gearify-shared-kernel/ gearify-shared-kernel/
COPY gearify-catalog-svc/ gearify-catalog-svc/

# Build the project
WORKDIR /src/gearify-catalog-svc
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV ASPNETCORE_URLS=http://+:5001
ENTRYPOINT ["dotnet", "Gearify.CatalogService.dll"]
