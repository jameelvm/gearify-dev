FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5010

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy shared kernel project
COPY gearify-shared-kernel/Gearify.SharedKernel.csproj gearify-shared-kernel/
# Copy notification service project
COPY gearify-notification-svc/Gearify.NotificationService.csproj gearify-notification-svc/

# Restore dependencies
RUN dotnet restore gearify-notification-svc/Gearify.NotificationService.csproj

# Copy all source files
COPY gearify-shared-kernel/ gearify-shared-kernel/
COPY gearify-notification-svc/ gearify-notification-svc/

# Build the project
WORKDIR /src/gearify-notification-svc
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV ASPNETCORE_URLS=http://+:80
ENTRYPOINT ["dotnet", "Gearify.NotificationService.dll"]
