# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj first for better layer caching
COPY ./src/OrderNotification.Worker/OrderNotification.Worker.csproj ./src/OrderNotification.Worker/

RUN dotnet restore ./src/OrderNotification.Worker/OrderNotification.Worker.csproj

# Copy everything else
COPY ./src ./src

RUN dotnet publish ./src/OrderNotification.Worker/OrderNotification.Worker.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./
ENTRYPOINT ["dotnet", "OrderNotification.Worker.dll"]
