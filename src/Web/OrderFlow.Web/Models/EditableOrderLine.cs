namespace OrderFlow.Web.Models;

public sealed class EditableOrderLine
{
    public EditableOrderLine(string sku, int quantity, decimal unitPrice)
    {
        Sku = sku;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }

    public string Sku { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}
