# Use the official .NET SDK image for building the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["WhatsApp_Endpoints/WhatsApp_Endpoints.csproj", "WhatsApp_Endpoints/"]
RUN dotnet restore "WhatsApp_Endpoints/WhatsApp_Endpoints.csproj"

# Copy all files and build the application
COPY . .
WORKDIR "/src/WhatsApp_Endpoints"
RUN dotnet build "WhatsApp_Endpoints.csproj" -c Release -o /app/build

# Publish the application
FROM build AS publish
RUN dotnet publish "WhatsApp_Endpoints.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final stage/image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Configure container properties
ENV ASPNETCORE_URLS=http://+:80
ENV META_TOKEN=placeholder_value
EXPOSE 80

# Start the application
ENTRYPOINT ["dotnet", "WhatsApp_Endpoints.dll"]
