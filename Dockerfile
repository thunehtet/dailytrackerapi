FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["daily tracker api.csproj", "./"]
RUN dotnet restore "daily tracker api.csproj"

COPY . .
RUN dotnet publish "daily tracker api.csproj" \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_FORWARDEDHEADERS_ENABLED=true \
    DOTNET_EnableDiagnostics=0

COPY --from=build /app/publish .

EXPOSE 8080
USER $APP_UID

ENTRYPOINT ["sh", "-c", "exec dotnet 'daily tracker api.dll' --urls http://0.0.0.0:${PORT:-8080}"]

