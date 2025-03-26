# Usa la imagen oficial de .NET para compilación
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /app

# Copiar archivos del proyecto
COPY WhatsApp_Endpoints.csproj ./
RUN dotnet restore

# Copiar el resto del código
COPY . ./
RUN dotnet publish -c Release -o out

# Imagen final para producción
FROM mcr.microsoft.com/dotnet/aspnet:8.0

WORKDIR /app
COPY --from=build /app/out .

# Puerto que usará el contenedor
EXPOSE 80

# Comando que ejecutará la app
ENTRYPOINT ["dotnet", "WhatsApp_Endpoints.dll"]
