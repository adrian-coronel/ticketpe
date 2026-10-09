using Microsoft.EntityFrameworkCore;
using Orders.Infrastructure.Persistence;
using StackExchange.Redis;
using TicketPe.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Redis como singleton: la conexión es cara y se reutiliza
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")!));

// CORS para que el front en Vite pueda llamar a la API
builder.Services.AddCors(o => o.AddPolicy("dev", p => p
    .WithOrigins("http://localhost:5173")
    .AllowAnyHeader()
    .AllowAnyMethod()));

builder.Services.AddDbContext<OrdersDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.UseCors("dev");

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.MapControllers();

// Endpoint temporal de verificación — bórralo después
app.MapGet("/ping-redis", async (IConnectionMultiplexer redis) =>
{
    var db = redis.GetDatabase();
    await db.StringSetAsync("prueba", "funciona", TimeSpan.FromSeconds(30));
    return Results.Ok(new { valor = (string?)await db.StringGetAsync("prueba") });
});

app.Run();
