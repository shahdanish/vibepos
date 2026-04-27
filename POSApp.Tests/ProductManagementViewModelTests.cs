using Moq;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.UI.ViewModels;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace POSApp.Tests
{
    public class ProductManagementViewModelTests
    {
        private readonly Mock<IProductRepository> _mockProductRepo;
        private readonly Mock<ICategoryRepository> _mockCategoryRepo;
        private readonly ProductManagementViewModel _viewModel;

        public ProductManagementViewModelTests()
        {
            _mockProductRepo = new Mock<IProductRepository>();
            _mockCategoryRepo = new Mock<ICategoryRepository>();

            _mockProductRepo.Setup(repo => repo.GetAllAsync())
                .ReturnsAsync(new List<Product>());
            _mockCategoryRepo.Setup(repo => repo.GetAllAsync())
                .ReturnsAsync(new List<Category>());

            _viewModel = new ProductManagementViewModel(_mockProductRepo.Object, _mockCategoryRepo.Object);
        }

        [Fact]
        public async Task LoadData_PopulatesProductsAndCategories()
        {
            // Arrange
            var products = new List<Product>
            {
                new Product { Id = 1, ProductName = "Test Product" }
            };
            var categories = new List<Category>
            {
                new Category { Id = 1, Name = "Test Category" }
            };

            _mockProductRepo.Setup(repo => repo.GetAllAsync()).ReturnsAsync(products);
            _mockCategoryRepo.Setup(repo => repo.GetAllAsync()).ReturnsAsync(categories);

            // Act
            await Task.Delay(100); // Give constructor LoadData task a tiny bit to finish, or just call Refresh
            _viewModel.RefreshCommand.Execute(null);
            await Task.Delay(100); // wait for RefreshCommand to complete

            // Assert
            Assert.Single(_viewModel.Products);
            Assert.Single(_viewModel.Categories);
            Assert.Equal("Test Product", _viewModel.Products[0].ProductName);
            Assert.Equal("Test Category", _viewModel.Categories[0].Name);
        }
        
        [Fact]
        public void ClearForm_ResetsAllProperties()
        {
            // Arrange
            _viewModel.ProductName = "Test";
            _viewModel.CostPrice = 10;
            _viewModel.SelectedProduct = new Product { ProductName = "Test" };
            
            // Act
            _viewModel.ClearCommand.Execute(null);
            
            // Assert
            Assert.Null(_viewModel.SelectedProduct);
            Assert.Equal(string.Empty, _viewModel.ProductName);
            Assert.Equal(0, _viewModel.CostPrice);
        }
    }
}
