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
EXPOSE 8080
ENTRYPOINT ["dotnet", "SmartPalm.FieldService.API.dll"]
