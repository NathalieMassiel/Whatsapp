using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net.Http;

var builder = WebApplication.CreateBuilder(args);

// Agregar configuración y dependencias
builder.Services.AddControllers();
builder.Services.AddHttpClient(); // Necesario para inyectar HttpClient
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(); // Opcional, para probar en Swagger UI

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Configuración básica de la app
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();
