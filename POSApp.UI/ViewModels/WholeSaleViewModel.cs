using POSApp.Core.Entities;
using POSApp.Core.Interfaces;

namespace POSApp.UI.ViewModels
{
    public class WholeSaleViewModel : SaleViewModel
    {
        public WholeSaleViewModel(ISaleRepository saleRepository, IProductRepository productRepository, ICustomerRepository customerRepository)
            : base(saleRepository, productRepository, customerRepository)
        {
            // Override to use wholesale prices
        }

        // Override the SelectedProduct setter to use wholesale price
        public new Product? SelectedProduct
        {
            get => base.SelectedProduct;
            set
            {
                base.SelectedProduct = value;
                if (value != null)
                {
                    // Use wholesale price instead of retail price
                    UnitPrice = value.WholesalePrice > 0 ? value.WholesalePrice : value.UnitPrice;
                }
            }
        }
    }
}
