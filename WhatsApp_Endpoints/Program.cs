using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net.Http;

var builder = WebApplication.CreateBuilder(args);

// Habilitar CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "http://localhost:3001") // Agrega aquí el puerto del frontend local
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

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

// Usa CORS antes del enrutamiento
app.UseCors("AllowFrontend");

app.UseHttpsRedirection();
app.UseAuthorization();

app.MapControllers();
app.Run();
