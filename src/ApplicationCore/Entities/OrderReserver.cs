using System.Collections.Generic;

namespace Microsoft.eShopWeb.ApplicationCore.Entities;

internal class OrderReserver
{
    public int OrderId { get; set; }
    public List<OrderedItem> OrderedItems { get; set; }
}

internal class OrderedItem
{
    public int ProductId { get; set; }
    public string ProductName { get; set; }
    public int ProductQuantity { get; set; }
}
