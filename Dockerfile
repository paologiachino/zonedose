# Production image of the Orchard host. Built in CI; deployed from phase 6.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# Project files first, so the restore layer is reused while only code changes
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/Zonedose.Web/Zonedose.Web.csproj src/Zonedose.Web/
COPY src/Modules/Zonedose.Platform/Zonedose.Platform.csproj src/Modules/Zonedose.Platform/
COPY src/Modules/Zonedose.Accounts/Zonedose.Accounts.csproj src/Modules/Zonedose.Accounts/
RUN dotnet restore src/Zonedose.Web/Zonedose.Web.csproj
COPY src/ src/
RUN dotnet publish src/Zonedose.Web/Zonedose.Web.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
# Tenant settings and per-tenant data protection keys live in App_Data:
# a volume in every environment, owned by the image's non-root user.
# wwwroot must exist and be writable: the media module keeps its resized
# image cache there and refuses to start without a web root.
RUN mkdir /app-data /app/wwwroot && chown app:app /app-data /app/wwwroot
ENV ORCHARD_APP_DATA=/app-data \
    ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1
VOLUME /app-data
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "Zonedose.Web.dll"]
