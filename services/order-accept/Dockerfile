# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj first for better layer caching
COPY ./src/OrderAccept.Api/OrderAccept.Api.csproj ./src/OrderAccept.Api/
COPY ./src/OrderAccept.Application/OrderAccept.Application.csproj ./src/OrderAccept.Application/
COPY ./src/OrderAccept.Infrastructure/OrderAccept.Infrastructure.csproj ./src/OrderAccept.Infrastructure/
COPY ./src/OrderAccept.Shared/OrderAccept.Shared.csproj ./src/OrderAccept.Shared/

RUN dotnet restore ./src/OrderAccept.Api/OrderAccept.Api.csproj

# Copy everything else
COPY ./src ./src

RUN dotnet publish ./src/OrderAccept.Api/OrderAccept.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
COPY --from=build /app/publish ./
ENTRYPOINT ["dotnet", "OrderAccept.Api.dll"]
