using BreakFastShop.Models;
using Microsoft.AspNet.SignalR;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;

namespace BreakFastShop.Hubs
{
    public class OrdersHub : Hub
    {
        private static string BuildShopGroupName(Guid shopId) => $"shop:{shopId:D}";

        private static IReadOnlyList<OrderItemDto> NormalizeItems(IEnumerable<OrderItemDto> items, out bool hasInvalid)
        {
            hasInvalid = false;

            if (items == null)
            {
                return Array.Empty<OrderItemDto>();
            }

            var list = new List<OrderItemDto>();

            foreach (var item in items)
            {
                if (item == null)
                {
                    hasInvalid = true;
                    continue;
                }

                if (!item.MealId.HasValue || item.MealId.Value == Guid.Empty)
                {
                    hasInvalid = true;
                    continue;
                }

                var name = item.Name?.Trim();
                if (string.IsNullOrWhiteSpace(name) || item.Qty <= 0 || item.Price < 0)
                {
                    hasInvalid = true;
                    continue;
                }

                list.Add(new OrderItemDto
                {
                    MealId = item.MealId,
                    Name = name,
                    Qty = item.Qty,
                    Price = item.Price
                });
            }

            return list;
        }

        public async Task CreateOrder(OrderCreateDto dto)
        {
            if (dto == null || dto.ShopId == Guid.Empty)
            {
                Clients.Caller.orderChanged(new { type = "error", error = "店家識別碼無效。" });
                return;
            }

            var connStr = ConfigurationManager.ConnectionStrings["BreakfastShop"]?.ConnectionString;
            if (string.IsNullOrWhiteSpace(connStr))
            {
                Clients.Caller.orderChanged(new { type = "error", error = "尚未設定資料庫連線字串。" });
                return;
            }

            if (dto.TableId == Guid.Empty)
            {
                Clients.Caller.orderChanged(new { type = "error", error = "桌號資料無效。" });
                return;
            }

            var normalizedItems = NormalizeItems(dto.Items, out var hasInvalidItems);
            if (hasInvalidItems)
            {
                Clients.Caller.orderChanged(new { type = "error", error = "餐點資料無效，請重新選擇。" });
                return;
            }

            if (normalizedItems.Count == 0)
            {
                Clients.Caller.orderChanged(new { type = "error", error = "請先選擇至少一項餐點。" });
                return;
            }

            var orderId = Guid.NewGuid();
            var now = DateTime.UtcNow;
            var nowOffset = DateTimeOffset.UtcNow;
            var trimmedNotes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();

            const string insertOrderSql = @"INSERT INTO Orders(Id,ShopId,OrderType,TableId,TakeoutCode,Notes,Status,CreatedAt,UpdatedAt)
                                           VALUES(@Id,@ShopId,@OrderType,@TableId,NULL,@Notes,@Status,@CreatedAt,@UpdatedAt);";

            const string insertItemSql = @"INSERT INTO OrderItems(Id,OrderId,MealId,MealName,Quantity,UnitPrice,Notes,CreateDate)
                                          VALUES(@Id,@OrderId,@MealId,@MealName,@Quantity,@UnitPrice,NULL,@CreateDate);";

            try
            {
                using (var connection = new SqlConnection(connStr))
                {
                    await connection.OpenAsync();
                    using (var transaction = connection.BeginTransaction())
                    {
                        using (var orderCommand = new SqlCommand(insertOrderSql, connection, transaction))
                        {
                            orderCommand.Parameters.AddWithValue("@Id", orderId);
                            orderCommand.Parameters.AddWithValue("@ShopId", dto.ShopId);
                            orderCommand.Parameters.AddWithValue("@OrderType", "DineIn");
                            orderCommand.Parameters.AddWithValue("@TableId", dto.TableId);
                            orderCommand.Parameters.AddWithValue("@Notes", (object)trimmedNotes ?? DBNull.Value);
                            orderCommand.Parameters.AddWithValue("@Status", "Pending");
                            orderCommand.Parameters.AddWithValue("@CreatedAt", now);
                            orderCommand.Parameters.AddWithValue("@UpdatedAt", now);

                            await orderCommand.ExecuteNonQueryAsync();
                        }

                        foreach (var item in normalizedItems)
                        {
                            using (var itemCommand = new SqlCommand(insertItemSql, connection, transaction))
                            {
                                itemCommand.Parameters.AddWithValue("@Id", Guid.NewGuid());
                                itemCommand.Parameters.AddWithValue("@OrderId", orderId);
                                itemCommand.Parameters.AddWithValue("@MealId", item.MealId.Value);
                                itemCommand.Parameters.AddWithValue("@MealName", item.Name);
                                itemCommand.Parameters.AddWithValue("@Quantity", item.Qty);
                                itemCommand.Parameters.AddWithValue("@UnitPrice", item.Price);
                                itemCommand.Parameters.AddWithValue("@CreateDate", now);

                                await itemCommand.ExecuteNonQueryAsync();
                            }
                        }

                        transaction.Commit();
                    }
                }
            }
            catch
            {
                Clients.Caller.orderChanged(new { type = "error", error = "送出訂單時發生錯誤，請稍後再試。" });
                return;
            }

            var orderPayload = new
            {
                id = orderId,
                shopId = dto.ShopId,
                tableId = dto.TableId,
                tableNumber = dto.TableNumber,
                tableZone = dto.TableZone,
                notes = trimmedNotes,
                status = "Pending",
                orderType = "DineIn",
                createdAt = now,
                updatedAt = now,
                items = normalizedItems.Select(i => new
                {
                    mealId = i.MealId,
                    name = i.Name,
                    qty = i.Qty,
                    price = i.Price
                }).ToList()
            };

            var dtoPayload = new
            {
                shopId = dto.ShopId,
                shopName = string.IsNullOrWhiteSpace(dto.ShopName) ? null : dto.ShopName.Trim(),
                tableId = dto.TableId,
                tableNumber = dto.TableNumber,
                tableZone = dto.TableZone,
                notes = trimmedNotes,
                items = orderPayload.items
            };

            var payload = new { type = "created", order = orderPayload, dto = dtoPayload, ts = nowOffset };
            Clients.Group(BuildShopGroupName(dto.ShopId)).orderChanged(payload);
            Clients.Caller.orderChanged(payload);
        }

        public async Task<bool> JoinShop(Guid shopId)
        {
            if (shopId == Guid.Empty)
            {
                return false;
            }

            await Groups.Add(Context.ConnectionId, BuildShopGroupName(shopId));
            return true;
        }

        public Task LeaveShop(Guid shopId)
        {
            if (shopId == Guid.Empty)
            {
                return Task.FromResult<object>(null);
            }

            return Groups.Remove(Context.ConnectionId, BuildShopGroupName(shopId));
        }
    }
}
