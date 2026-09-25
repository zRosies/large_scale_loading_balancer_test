using Npgsql;
using ordersAPI.Entities;

namespace ordersAPI.Database;

public static class DatabaseConfiguration
{
    public static NpgsqlDataSourceBuilder CreateDataSourceBuilder(
        string connectionString)
    {
        var dataSourceBuilder =
            new NpgsqlDataSourceBuilder(connectionString);

        dataSourceBuilder.MapEnum<OrderStatus>("orders_status_enum");

        return dataSourceBuilder;
    }
}