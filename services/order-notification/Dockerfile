# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj first for better layer caching
COPY ./src/OrderNotification.Api/OrderNotification.Api.csproj ./src/OrderNotification.Api/
COPY ./src/OrderNotification.Application/OrderNotification.Application.csproj ./src/OrderNotification.Application/
COPY ./src/OrderNotification.Infrastructure/OrderNotification.Infrastructure.csproj ./src/OrderNotification.Infrastructure/
COPY ./src/OrderNotification.Shared/OrderNotification.Shared.csproj ./src/OrderNotification.Shared/

RUN dotnet restore ./src/OrderNotification.Api/OrderNotification.Api.csproj

# Copy everything else
COPY ./src ./src

RUN dotnet publish ./src/OrderNotification.Api/OrderNotification.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
COPY --from=build /app/publish ./
ENTRYPOINT ["dotnet", "OrderNotification.Api.dll"]
