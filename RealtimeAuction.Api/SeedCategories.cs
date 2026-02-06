using MongoDB.Driver;
using RealtimeAuction.Api.Models;

// No namespace - same scope as Program.cs top-level statements
public static class SeedCategories
{
    public static async Task SeedAsync(IMongoDatabase database)
    {
        var categoriesCollection = database.GetCollection<Category>("Categories");
        
        // Kiểm tra xem đã có categories chưa
        var count = await categoriesCollection.CountDocumentsAsync(FilterDefinition<Category>.Empty);
        if (count > 0)
        {
            Console.WriteLine("[Seed] Categories already exist. Skipping seed.");
            return;
        }

        Console.WriteLine("[Seed] Seeding categories...");

        var categories = new List<Category>
        {
            // Điện tử
            new Category
            {
                Name = "Điện thoại & Phụ kiện",
                Description = "Điện thoại di động, smartphone, phụ kiện điện thoại",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Category
            {
                Name = "Laptop & Máy tính",
                Description = "Laptop, PC, linh kiện máy tính",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Category
            {
                Name = "Máy ảnh & Camera",
                Description = "Máy ảnh, camera, phụ kiện nhiếp ảnh",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Category
            {
                Name = "TV & Thiết bị nghe nhìn",
                Description = "Tivi, loa, tai nghe, âm thanh",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },

            // Thời trang
            new Category
            {
                Name = "Thời trang Nam",
                Description = "Quần áo, giày dép, phụ kiện nam",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Category
            {
                Name = "Thời trang Nữ",
                Description = "Quần áo, giày dép, phụ kiện nữ",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Category
            {
                Name = "Đồng hồ & Trang sức",
                Description = "Đồng hồ, nhẫn, dây chuyền, vòng tay",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },

            // Gia dụng
            new Category
            {
                Name = "Đồ gia dụng",
                Description = "Đồ dùng nhà bếp, nội thất, trang trí",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Category
            {
                Name = "Đồ điện tử gia dụng",
                Description = "Tủ lạnh, máy giặt, điều hòa, lò vi sóng",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },

            // Xe cộ
            new Category
            {
                Name = "Xe máy",
                Description = "Xe máy các loại, phụ tùng xe máy",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Category
            {
                Name = "Xe đạp",
                Description = "Xe đạp thể thao, xe đạp điện",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },

            // Sách & Văn phòng phẩm
            new Category
            {
                Name = "Sách",
                Description = "Sách văn học, sách giáo khoa, sách tham khảo",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Category
            {
                Name = "Văn phòng phẩm",
                Description = "Bút, vở, đồ dùng học tập văn phòng",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },

            // Khác
            new Category
            {
                Name = "Đồ chơi & Sở thích",
                Description = "Đồ chơi, đồ sưu tầm, mô hình",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Category
            {
                Name = "Thể thao & Du lịch",
                Description = "Dụng cụ thể thao, đồ du lịch, cắm trại",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Category
            {
                Name = "Nghệ thuật & Sưu tầm",
                Description = "Tranh, tác phẩm nghệ thuật, đồ cổ",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        await categoriesCollection.InsertManyAsync(categories);
        Console.WriteLine($"[Seed] Successfully seeded {categories.Count} categories.");
    }
}
