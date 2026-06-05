using POSApp.Core.Entities;
using POSApp.Core.Interfaces;

namespace POSApp.UI.ViewModels
{
    public class WholeSaleViewModel : SaleViewModel
    {
        public WholeSaleViewModel(ISaleRepository saleRepository, IProductRepository productRepository, ICustomerRepository customerRepository)
            : base(saleRepository, productRepository, customerRepository)
        {
        }

        protected override decimal GetUnitPriceForProduct(Product product)
            => product.WholesalePrice > 0 ? product.WholesalePrice : product.UnitPrice;

        protected override string InvoiceTitle => "Whole Sale Bill / Invoice";
    }
}
