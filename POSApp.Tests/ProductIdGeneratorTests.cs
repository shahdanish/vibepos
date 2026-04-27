using Moq;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.Infrastructure.Helpers;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace POSApp.Tests
{
    public class ProductIdGeneratorTests
    {
        private readonly Mock<IProductRepository> _mockRepo;
        private readonly ProductIdGenerator _generator;

        public ProductIdGeneratorTests()
        {
            _mockRepo = new Mock<IProductRepository>();
            _generator = new ProductIdGenerator(_mockRepo.Object);
        }

        [Fact]
        public async Task GenerateProductIdAsync_WithCategory_ReturnsCorrectFormat()
        {
            // Arrange
            _mockRepo.Setup(repo => repo.GetAllAsync())
                .ReturnsAsync(new List<Product>());

            // Act
            var result = await _generator.GenerateProductIdAsync(1);

            // Assert
            Assert.Equal("10001", result);
        }

        [Fact]
        public async Task GenerateProductIdAsync_WithCategoryAndExistingProducts_IncrementsSequence()
        {
            // Arrange
            var existingProducts = new List<Product>
            {
                new Product { CategoryId = 1, ProductId = "10001" },
                new Product { CategoryId = 1, ProductId = "10002" }
            };

            _mockRepo.Setup(repo => repo.GetAllAsync())
                .ReturnsAsync(existingProducts);

            // Act
            var result = await _generator.GenerateProductIdAsync(1);

            // Assert
            Assert.Equal("10003", result);
        }

        [Fact]
        public async Task GenerateProductIdAsync_WithoutCategory_ReturnsGenericNumber()
        {
            // Arrange
            _mockRepo.Setup(repo => repo.GetAllAsync())
                .ReturnsAsync(new List<Product>());

            // Act
            var result = await _generator.GenerateProductIdAsync();

            // Assert
            Assert.Equal("90001", result);
        }

        [Fact]
        public async Task GenerateProductIdAsync_WithoutCategoryAndExistingGenericProducts_IncrementsNumber()
        {
            // Arrange
            var existingProducts = new List<Product>
            {
                new Product { ProductId = "90001" },
                new Product { ProductId = "90005" }
            };

            _mockRepo.Setup(repo => repo.GetAllAsync())
                .ReturnsAsync(existingProducts);

            // Act
            var result = await _generator.GenerateProductIdAsync();

            // Assert
            Assert.Equal("90006", result);
        }
    }
}
