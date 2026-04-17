# Multi-stage build: SDK for compile, aspnet runtime for the final image
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY AzureMessagingApi/AzureMessagingApi.csproj AzureMessagingApi/
RUN dotnet restore AzureMessagingApi/AzureMessagingApi.csproj

COPY AzureMessagingApi/ AzureMessagingApi/
RUN dotnet publish AzureMessagingApi/AzureMessagingApi.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "AzureMessagingApi.dll"]
