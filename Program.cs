using Microsoft.EntityFrameworkCore;
using ordersAPI.Database;
using ordersAPI.Entities;
using ordersAPI.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection");

// Register NpgsqlDataSources as singletons once to optimize connection pooling under high concurrency
var ordersDataSource = DatabaseConfiguration.CreateDataSourceBuilder(connectionString!).Build();
builder.Services.AddSingleton(ordersDataSource);

builder.Services.AddDbContext<OrdersDBContext>(options =>
    options.UseNpgsql(ordersDataSource, o => o.MapEnum<OrderStatus>("orders_status_enum")));

var productsConnectionString =
    builder.Configuration.GetConnectionString("ProductsConnection");

var productsDataSource = new Npgsql.NpgsqlDataSourceBuilder(productsConnectionString!).Build();
builder.Services.AddSingleton(productsDataSource);

builder.Services.AddDbContext<ProductDbContext>(options =>
    options.UseNpgsql(productsDataSource));

// In-Memory cache for ultra-fast L1 responses and concurrency resilience
builder.Services.AddMemoryCache();

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "OrdersAPI_"; // Optional prefix for Redis keys
});


builder.Services.AddScoped<IOrderService, OrderService>();

// Add services to the container.
builder.Services.AddOpenApi();
builder.Services.AddControllers();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();

