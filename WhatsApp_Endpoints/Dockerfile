# Etapa de compilaci�n
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiar solo el .csproj y restaurar paquetes (para aprovechar el cache de Docker)
COPY WhatsApp_Endpoints/WhatsApp_Endpoints.csproj WhatsApp_Endpoints/
RUN dotnet restore "WhatsApp_Endpoints/WhatsApp_Endpoints.csproj"

# Copiar todo el c�digo fuente
COPY . .
WORKDIR /src/WhatsApp_Endpoints

# Compilar el proyecto
RUN dotnet build "WhatsApp_Endpoints.csproj" -c Release -o /app/build

# Publicar la aplicaci�n
RUN dotnet publish "WhatsApp_Endpoints.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Etapa final (imagen m�s liviana)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Copiar los archivos publicados desde la etapa de build
COPY --from=build /app/publish .

# Configurar variables de entorno y puertos
ENV ASPNETCORE_URLS=http://+:80
ENV META_TOKEN=placeholder_value
EXPOSE 80

# Iniciar la aplicaci�n
ENTRYPOINT ["dotnet", "WhatsApp_Endpoints.dll"]
