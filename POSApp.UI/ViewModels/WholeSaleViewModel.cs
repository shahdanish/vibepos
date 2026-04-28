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

        public new Product? SelectedProduct
        {
            get => base.SelectedProduct;
            set
            {
                base.SelectedProduct = value;
                if (value != null)
                {
                    UnitPrice = value.WholesalePrice > 0 ? value.WholesalePrice : value.UnitPrice;
                }
            }
        }

        protected override string InvoiceTitle => "Whole Sale Bill / Invoice";
    }
}
