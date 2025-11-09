FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY gearify-tenant-svc/*.csproj ./gearify-tenant-svc/
RUN dotnet restore gearify-tenant-svc/Gearify.TenantService.csproj
COPY gearify-tenant-svc/ ./gearify-tenant-svc/
WORKDIR /src/gearify-tenant-svc
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:80
ENTRYPOINT ["dotnet", "Gearify.TenantService.dll"]
