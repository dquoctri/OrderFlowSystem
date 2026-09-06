namespace OrderFlow.Orders.Infrastructure.Persistence.Entities;

public enum OrderStatus
{
    Pending,
    Reserving,
    Charging,
    Confirmed,
    Cancelled
}
