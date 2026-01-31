# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj first for better layer caching
COPY ./src/OrderProcess.Worker/OrderProcess.Worker.csproj ./src/OrderProcess.Worker/
COPY ./src/OrderProcess.Application/OrderProcess.Application.csproj ./src/OrderProcess.Application/
COPY ./src/OrderProcess.Infrastructure/OrderProcess.Infrastructure.csproj ./src/OrderProcess.Infrastructure/
COPY ./src/OrderProcess.Shared/OrderProcess.Shared.csproj ./src/OrderProcess.Shared/

RUN dotnet restore ./src/OrderProcess.Worker/OrderProcess.Worker.csproj

# Copy everything else
COPY ./src ./src

RUN dotnet publish ./src/OrderProcess.Worker/OrderProcess.Worker.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
COPY --from=build /app/publish ./
ENTRYPOINT ["dotnet", "OrderProcess.Worker.dll"]
