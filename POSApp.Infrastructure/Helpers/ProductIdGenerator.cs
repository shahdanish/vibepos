using POSApp.Core.Interfaces;

namespace POSApp.Infrastructure.Helpers
{
    public sealed class ProductIdGenerator
    {
        private readonly IProductRepository _productRepository;

        public ProductIdGenerator(IProductRepository productRepository)
        {
            _productRepository = productRepository;
        }

        /// <summary>
        /// Generates a unique 5-digit product ID based on category
        /// Format: [Category Number (1-2 digits)][Sequence (3-4 digits)]
        /// Example: For category 1, products: 10001, 10002, 10003...
        ///          For category 12, products: 12001, 12002, 12003...
        /// </summary>
        public async Task<string> GenerateProductIdAsync(int? categoryId = null, CancellationToken ct = default)
        {
            var allProducts = await _productRepository.GetAllAsync(ct);

            if (categoryId.HasValue)
            {
                // Get all products in this category
                var categoryProducts = allProducts.Where(p => p.CategoryId == categoryId.Value).ToList();

                // Find the highest sequence number for this category
                var categoryPrefix = categoryId.Value.ToString();
                var maxSequence = 0;

                foreach (var product in categoryProducts)
                {
                    if (product.ProductId.StartsWith(categoryPrefix) && product.ProductId.Length == 5)
                    {
                        var sequencePart = product.ProductId.Substring(categoryPrefix.Length);
                        if (int.TryParse(sequencePart, out int sequence))
                        {
                            maxSequence = Math.Max(maxSequence, sequence);
                        }
                    }
                }

                // Generate new ID
                var newSequence = maxSequence + 1;
                var requiredDigits = 5 - categoryPrefix.Length;
                return $"{categoryPrefix}{newSequence.ToString().PadLeft(requiredDigits, '0')}";
            }
            else
            {
                // No category - use generic numbering starting from 90000
                var genericProducts = allProducts
                    .Where(p => p.ProductId.StartsWith("9") && p.ProductId.Length == 5)
                    .ToList();

                var maxId = 90000;
                foreach (var product in genericProducts)
                {
                    if (int.TryParse(product.ProductId, out int id) && id >= 90000 && id < 100000)
                    {
                        maxId = Math.Max(maxId, id);
                    }
                }

                return (maxId + 1).ToString();
            }
        }
    }
}
