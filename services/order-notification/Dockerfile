# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj first for better layer caching
COPY ./src/OrderNotification.Worker/OrderNotification.Worker.csproj ./src/OrderNotification.Worker/
COPY ./src/OrderNotification.Application/OrderNotification.Application.csproj ./src/OrderNotification.Application/
COPY ./src/OrderNotification.Infrastructure/OrderNotification.Infrastructure.csproj ./src/OrderNotification.Infrastructure/
COPY ./src/OrderNotification.Shared/OrderNotification.Shared.csproj ./src/OrderNotification.Shared/

RUN dotnet restore ./src/OrderNotification.Worker/OrderNotification.Worker.csproj

# Copy everything else
COPY ./src ./src

RUN dotnet publish ./src/OrderNotification.Worker/OrderNotification.Worker.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./
ENTRYPOINT ["dotnet", "OrderNotification.Worker.dll"]
