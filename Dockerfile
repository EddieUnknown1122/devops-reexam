
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src


COPY app/CrudApp/CrudApp.csproj app/CrudApp/
RUN dotnet restore app/CrudApp/CrudApp.csproj


COPY app/CrudApp/ app/CrudApp/
RUN dotnet publish app/CrudApp/CrudApp.csproj -c Release -o /app/publish


FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app

ENV APP_PORT=8080
COPY --from=build /app/publish .

EXPOSE 8080


USER $APP_UID

ENTRYPOINT ["dotnet", "CrudApp.dll"]