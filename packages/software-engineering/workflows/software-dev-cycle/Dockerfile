FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY SoftwareDevCycle/ ./SoftwareDevCycle/
RUN dotnet publish SoftwareDevCycle/SoftwareDevCycle.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "SoftwareDevCycle.dll"]
