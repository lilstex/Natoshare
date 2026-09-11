# This file builds the Natoshare API into a small image we can run anywhere.
# It has two stages: the first stage has the full SDK and builds the app, the second
# stage only has the smaller runtime, so the final image is not bloated with build tools.

# Stage 1: build the app.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# We copy everything because the API project references other projects (Domain,
# Application, Infrastructure) that live in different folders.
COPY . .
RUN dotnet restore src/Natoshare.Api/Natoshare.Api.csproj
RUN dotnet publish src/Natoshare.Api/Natoshare.Api.csproj -c Release -o /app/publish --no-restore

# Stage 2: run the app.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Npgsql (the Postgres driver) looks for this Kerberos library when it starts up, even
# though we only use plain username/password to connect. Without it, every database
# call logs a scary looking error, so we install it here to keep the logs clean.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "Natoshare.Api.dll"]
