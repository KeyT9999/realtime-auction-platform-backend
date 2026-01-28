using MongoDB.Driver;
using RealtimeAuction.Api.Dtos;
using RealtimeAuction.Api.Models;

namespace RealtimeAuction.Api.Repositories
{
    public class ProductRepository : IProductRepository
    {
        private readonly IMongoCollection<Product> _products;

        public ProductRepository(IMongoDatabase database)
        {
            _products = database.GetCollection<Product>("products");
            
            // Create indexes
            var indexKeysDefinition = Builders<Product>.IndexKeys.Text(p => p.Name).Text(p => p.Description);
            var indexModel = new CreateIndexModel<Product>(indexKeysDefinition);
            _products.Indexes.CreateOne(indexModel);
        }

        public async Task<Product?> GetByIdAsync(string id)
        {
            return await _products.Find(p => p.Id == id).FirstOrDefaultAsync();
        }

        public async Task<Product> CreateAsync(Product product)
        {
            await _products.InsertOneAsync(product);
            return product;
        }

        public async Task<Product> UpdateAsync(Product product)
        {
            product.UpdatedAt = DateTime.UtcNow;
            await _products.ReplaceOneAsync(p => p.Id == product.Id, product);
            return product;
        }

        public async Task<List<Product>> SearchAsync(ProductFilterDto filter)
        {
            var builder = Builders<Product>.Filter;
            // Default: show Active products. If user is admin (implied logic elsewhere), might want to see Pending, but for public search: Active.
            // I'll assume this repository method is for public search mostly or filtered by status.
            // Let's NOT force "Active" here so Admin can use it too if needed, or I should strict it?
            // The requirement says "Search & Filter" for users.
            
            // If Status is provided in filter, use it. Otherwise default to "Active".
            var statusFilter = !string.IsNullOrEmpty(filter.Status) ? filter.Status : "Active";
            var mongoFilter = builder.Eq(p => p.Status, statusFilter); 

            if (!string.IsNullOrEmpty(filter.Keyword))
            {
                mongoFilter &= builder.Text(filter.Keyword);
            }

            if (!string.IsNullOrEmpty(filter.CategoryId))
            {
                mongoFilter &= builder.Eq(p => p.CategoryId, filter.CategoryId);
            }

            if (filter.MinPrice.HasValue)
            {
                mongoFilter &= builder.Gte(p => p.Price, filter.MinPrice.Value);
            }

            if (filter.MaxPrice.HasValue)
            {
                mongoFilter &= builder.Lte(p => p.Price, filter.MaxPrice.Value);
            }

            return await _products.Find(mongoFilter)
                .Skip((filter.Page - 1) * filter.PageSize)
                .Limit(filter.PageSize)
                .ToListAsync();
        }
    }
}
