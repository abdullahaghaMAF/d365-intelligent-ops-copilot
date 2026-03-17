using System.ComponentModel;
using System.Text.Json;
using Microsoft.SemanticKernel;

namespace D365OpsCopilot.Plugins;

public class D365DataPlugin
{
    [KernelFunction("get_inventory_by_sku")]
    [Description("Gets current inventory levels for a given SKU or item number across all warehouses")]
    public async Task<string> GetInventoryBySku(
        [Description("The SKU or item number to look up")] string skuNumber)
    {
        // Mock data - will be replaced with Dataverse API calls in Phase 3
        var mockData = new
        {
            sku = skuNumber,
            warehouses = new[]
            {
                new { name = "Dubai Main", quantity = 1250, unit = "EA" },
                new { name = "Abu Dhabi DC", quantity = 430, unit = "EA" },
                new { name = "Jebel Ali", quantity = 2100, unit = "EA" }
            },
            totalQuantity = 3780,
            lastUpdated = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
        };

        return JsonSerializer.Serialize(mockData);
    }

    [KernelFunction("get_purchase_orders")]
    [Description("Gets recent purchase orders, optionally filtered by status (Open, Confirmed, Received)")]
    public async Task<string> GetPurchaseOrders(
        [Description("PO status filter: Open, Confirmed, or Received")] string status)
    {
        var mockData = new
        {
            status,
            orders = new[]
            {
                new { poNumber = "PO-2026-0451", vendor = "Acme Supplies LLC", status, totalAmount = 45000.00, currency = "AED", date = "2026-03-10" },
                new { poNumber = "PO-2026-0452", vendor = "Gulf Materials Trading", status, totalAmount = 12500.00, currency = "AED", date = "2026-03-12" },
                new { poNumber = "PO-2026-0453", vendor = "Emirates Industrial Co", status, totalAmount = 78200.00, currency = "AED", date = "2026-03-14" }
            },
            totalOrders = 3
        };

        return JsonSerializer.Serialize(mockData);
    }

    [KernelFunction("get_warehouse_summary")]
    [Description("Gets a summary of all warehouses including total items, capacity utilization, and pending transfers")]
    public async Task<string> GetWarehouseSummary()
    {
        var mockData = new
        {
            warehouses = new[]
            {
                new { name = "Dubai Main", totalItems = 15420, capacityPercent = 78, pendingInbound = 3, pendingOutbound = 7 },
                new { name = "Abu Dhabi DC", totalItems = 8930, capacityPercent = 62, pendingInbound = 1, pendingOutbound = 4 },
                new { name = "Jebel Ali", totalItems = 22100, capacityPercent = 91, pendingInbound = 5, pendingOutbound = 2 }
            },
            lastUpdated = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
        };

        return JsonSerializer.Serialize(mockData);
    }
}