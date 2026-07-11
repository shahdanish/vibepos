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

        // The printed wholesale bill must be identical to the standard item bill:
        // same title ("Bill / Invoice"), same columns, same wording. All receipt-format
        // overrides are intentionally removed so the base SaleViewModel template is used
        // verbatim. Only the on-screen selling price differs (wholesale pricing above).

        public override string ModeSwitchLabel => "⇄ RETAIL SALE";

        // Wholesale mirrors the NORMAL Sale screen: manual type-to-search must NOT
        // auto-commit the first TextSearch match. The item is only added on an EXPLICIT
        // selection (Enter or click), handled in the view. So AutoAddItem stays false
        // (inherited from the base) — this override is intentionally removed.
    }
}
