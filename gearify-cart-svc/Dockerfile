FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5003

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy shared kernel project
COPY gearify-shared-kernel/Gearify.SharedKernel.csproj gearify-shared-kernel/
# Copy cart service project
COPY gearify-cart-svc/Gearify.CartService.csproj gearify-cart-svc/

# Restore dependencies
RUN dotnet restore gearify-cart-svc/Gearify.CartService.csproj

# Copy all source files
COPY gearify-shared-kernel/ gearify-shared-kernel/
COPY gearify-cart-svc/ gearify-cart-svc/

# Build the project
WORKDIR /src/gearify-cart-svc
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV ASPNETCORE_URLS=http://+:80
ENTRYPOINT ["dotnet", "Gearify.CartService.dll"]
