using Microsoft.EntityFrameworkCore;
using ordersAPI.Database;
using ordersAPI.Entities;
using ordersAPI.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<OrdersDBContext>(options =>
    options.UseNpgsql(
        DatabaseConfiguration.CreateDataSourceBuilder(connectionString!).Build(),
        o => o.MapEnum<OrderStatus>("orders_status_enum")));

var productsConnectionString =
    builder.Configuration.GetConnectionString("ProductsConnection");

builder.Services.AddDbContext<ProductDbContext>(options =>
    options.UseNpgsql(productsConnectionString));


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

