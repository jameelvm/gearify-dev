FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy shared kernel project
COPY gearify-shared-kernel/Gearify.SharedKernel.csproj gearify-shared-kernel/
# Copy shipping service project
COPY gearify-shipping-svc/Gearify.ShippingService.csproj gearify-shipping-svc/

# Restore dependencies
RUN dotnet restore gearify-shipping-svc/Gearify.ShippingService.csproj

# Copy all source files
COPY gearify-shared-kernel/ gearify-shared-kernel/
COPY gearify-shipping-svc/ gearify-shipping-svc/

# Build the project
WORKDIR /src/gearify-shipping-svc
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV ASPNETCORE_URLS=http://+:80
ENTRYPOINT ["dotnet", "Gearify.ShippingService.dll"]
