FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Packages.props .
COPY SmartPalm.FieldService.API/SmartPalm.FieldService.API.csproj SmartPalm.FieldService.API/
RUN dotnet restore SmartPalm.FieldService.API/SmartPalm.FieldService.API.csproj
COPY SmartPalm.FieldService.API/ SmartPalm.FieldService.API/
RUN dotnet publish SmartPalm.FieldService.API/SmartPalm.FieldService.API.csproj -c Release -o /app --no-restore
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD curl --fail --silent --show-error http://localhost:8080/health/ready || exit 1
ENTRYPOINT ["dotnet", "SmartPalm.FieldService.API.dll"]
