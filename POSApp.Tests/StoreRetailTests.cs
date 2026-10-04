using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.EntityFrameworkCore;
using Moq;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.Core.Services;
using POSApp.Infrastructure.Repositories;
using POSApp.Infrastructure.Services;
using POSApp.UI.Helpers;
using POSApp.UI.ViewModels;
using POSApp.UI.Views;

namespace POSApp.Tests
{
    public sealed class ShopPhraseTests
    {
        [Fact]
        public void Override_Wins_AndABlankLineFallsBack()
        {
            var custom = new Dictionary<string, string> { ["sale.save"] = "  Ring it up  ", ["sale.print"] = "   " };
            Assert.Equal("Ring it up", ShopPhrases.Resolve(ShopPhrases.English, "sale.save", custom));
            Assert.Equal("Print", ShopPhrases.Resolve(ShopPhrases.English, "sale.print", custom));
            Assert.Equal("Guardar venta", ShopPhrases.Resolve(ShopPhrases.Spanish, "sale.save", new Dictionary<string, string>()));
            Assert.Equal("missing.key", ShopPhrases.Resolve(ShopPhrases.English, "missing.key", null));
        }

        [Fact]
        public void EditedLines_AreOnlyTheOnesThatDifferFromTheLanguage()
        {
            var rows = PhraseRow.Load(ShopPhrases.Spanish, new Dictionary<string, string> { ["sale.save"] = "Cobrar" });
            var save = rows.Single(r => r.Key == "sale.save");
            Assert.Equal("Guardar venta", save.DefaultText);
            Assert.Equal("Cobrar", save.CustomText);
            save.CustomText = "Guardar venta";
            Assert.Empty(PhraseRow.CollectOverrides(rows));
        }
    }

    public sealed class GiftCardRuleTests
    {
        [Fact]
        public void NewCode_AvoidsAmbiguousCharacters()
        {
            var code = GiftCardRules.NewCode();
            Assert.Matches("^GC[ABCDEFGHJKLMNPQRSTUVWXYZ23456789]{8}$", code);
            Assert.Equal(code, GiftCardRules.CodeIn("Tarjeta de regalo " + code));
            Assert.Null(GiftCardRules.CodeIn("no code here"));
        }

        [Theory]
        [InlineData(20, false, 15, 15, 15, null)]
        [InlineData(20, false, 25, 25, 20, null)]
        [InlineData(0, false, 5, 5, 0, "This gift card has no balance left.")]
        [InlineData(20, true, 5, 5, 0, "This gift card has been voided.")]
        public void Redeemable_CapsAtTheBalanceAndWhatIsDue(decimal balance, bool isVoid, decimal requested, decimal remaining, decimal pay, string? error)
        {
            var result = GiftCardRules.Redeemable(balance, isVoid, requested, remaining);
            Assert.Equal(pay, result.Pay);
            Assert.Equal(error, result.Error);
        }

        [Fact]
        public void ReverseIssue_RefusesACardThatWasAlreadySpent_AndCreditPutsARefundBack()
        {
            var card = new GiftCard { Code = "GCABCDEFGH", Balance = 30m, IssuedAmount = 50m };
            Assert.NotNull(GiftCardRules.ReverseIssue(card, 50m));
            Assert.Equal(30m, card.Balance);

            Assert.Null(GiftCardRules.ReverseIssue(card, 10m));
            Assert.Equal(20m, card.Balance);

            GiftCardRules.Credit(card, 5m);
            Assert.Equal(25m, card.Balance);
        }
    }

    public sealed class LoyaltyRuleTests
    {
        [Fact]
        public void Points_AreEarnedOnMerchandise_NotOnGiftCards_OrThePartPaidWithPoints()
        {
            Assert.Equal(25, LoyaltyRules.EarnedOnLines(25m, 0m, 0m, 1m));
            Assert.Equal(8, LoyaltyRules.EarnedOnLines(15m, 2m, 5m, 1m)); // 15 - 2 - 5
            Assert.Equal(0, LoyaltyRules.EarnedOnLines(10m, 0m, 10m, 1m));
            Assert.Equal(0, LoyaltyRules.Earned(20m, 20m, 0m, 1m));
            Assert.Equal(0, LoyaltyRules.EarnedOnLines(10m, 0m, 0m, 0m));
        }

        [Fact]
        public void Redeem_CapsAtTheBalance_AndRestoreIsProportional()
        {
            var (points, dollars, error) = LoyaltyRules.Redeem(250, 5m, 10m, 100);
            Assert.Null(error);
            Assert.Equal(250, points);
            Assert.Equal(2.50m, dollars);

            var capped = LoyaltyRules.Redeem(50, 1m, 10m, 100);
            Assert.Null(capped.Error);
            Assert.Equal(50, capped.Points);
            Assert.Equal(0.50m, capped.Dollars);
            Assert.NotNull(LoyaltyRules.Redeem(0, 1m, 10m, 100).Error);

            Assert.Equal(500, LoyaltyRules.PointsToRestore(2000, 20m, 5m));
            Assert.Equal(2000, LoyaltyRules.PointsToRestore(2000, 20m, 20m));
            Assert.Equal(0, LoyaltyRules.PointsToRestore(0, 20m, 20m));
        }
    }

    [Collection(GlobalStateCollection.Name)]
    public sealed class StoreTenderDescriptionTests
    {
        [Fact]
        public void ReceiptLine_ShowsTheGiftCardCode_AndThePoints()
        {
            Assert.Equal("Gift Card (GCABCDEFGH)", TenderCalculator.Describe(new SalePayment
            {
                Method = PaymentMethods.GiftCard, Amount = 10m, Reference = "GCABCDEFGH"
            }));
            Assert.Equal("Loyalty (500 pts)", TenderCalculator.Describe(new SalePayment
            {
                Method = PaymentMethods.Loyalty, Amount = 5m, Reference = "500"
            }));
            Assert.DoesNotContain(PaymentMethods.GiftCard, PaymentMethods.UnitedStates);
            Assert.DoesNotContain(PaymentMethods.Loyalty, PaymentMethods.UnitedStates);
        }

        [Fact]
        public void SplitReturn_GivesTheGiftCardItsShare_AndTheRestStaysCash()
        {
            var sale = new Sale { TotalBill = 30m };
            sale.Payments.Add(new SalePayment { Method = PaymentMethods.Cash, Amount = 20m });
            sale.Payments.Add(new SalePayment { Method = PaymentMethods.GiftCard, Amount = 10m, Reference = "GCABCDEFGH" });

            var full = SaleReturnViewModel.StoreCreditRefunds(sale, 30m);
            var gift = Assert.Single(full);
            Assert.Equal(PaymentMethods.GiftCard, gift.Method);
            Assert.Equal(-10m, gift.Amount);
            Assert.Equal("GCABCDEFGH", gift.Reference);

            var half = Assert.Single(SaleReturnViewModel.StoreCreditRefunds(sale, 15m));
            Assert.Equal(-5m, half.Amount);
        }

        [Fact]
        public void TakeRefund_OfASplit_DoesNotPayTheGiftCardPortionInCashAsWell()
        {
            Sta.Run(() =>
            {
                var vm = new SaleReturnViewModel(new Mock<ISaleRepository>().Object, new Mock<IProductRepository>().Object)
                {
                    TotalReturnAmount = 30m
                };
                var sale = new Sale { InvoiceNumber = "500", TotalBill = 30m };
                sale.Payments.Add(new SalePayment { Method = PaymentMethods.Cash, Amount = 20m });
                sale.Payments.Add(new SalePayment { Method = PaymentMethods.GiftCard, Amount = 10m, Reference = "GCABCDEFGH" });

                var refund = vm.TakeRefundAsync(sale).GetAwaiter().GetResult()!;

                Assert.Equal(PaymentMethods.Cash, refund.Method);
                Assert.Equal(-20m, refund.Amount);
                var extra = Assert.Single(vm.ExtraRefunds);
                Assert.Equal(PaymentMethods.GiftCard, extra.Method);
                Assert.Equal(-10m, extra.Amount);
            });
        }

        [Fact]
        public void TakeRefund_OfALoyaltySale_PutsThePointsBackInProportion()
        {
            Sta.Run(() =>
            {
                var vm = new SaleReturnViewModel(new Mock<ISaleRepository>().Object, new Mock<IProductRepository>().Object)
                {
                    TotalReturnAmount = 10m
                };
                var sale = new Sale { InvoiceNumber = "501", TotalBill = 20m };
                sale.Payments.Add(new SalePayment { Method = PaymentMethods.Loyalty, Amount = 20m, Reference = "2000" });

                var refund = vm.TakeRefundAsync(sale).GetAwaiter().GetResult()!;

                Assert.Equal(PaymentMethods.Loyalty, refund.Method);
                Assert.Equal(-10m, refund.Amount);
                Assert.Equal("1000", refund.Reference);
                Assert.Empty(vm.ExtraRefunds);
            });
        }

        [Fact]
        public void ReturningMerchandise_ClawsBackItsPoints_AndAGiftCardLineDoesNot()
        {
            var sales = new Mock<ISaleRepository>();
            sales.Setup(r => r.GetNextInvoiceNumberAsync(It.IsAny<CancellationToken>())).ReturnsAsync("11016");
            var vm = new SaleReturnViewModel(sales.Object, new Mock<IProductRepository>().Object);
            var sale = new Sale { InvoiceNumber = "S1", TotalBill = 30.83m, CustomerId = 3 };
            sale.SaleItems.Add(new SaleItem { ProductId = "P1", Total = 10m });
            sale.SaleItems.Add(new SaleItem { ProductId = GiftCardRules.LineProductId, Total = 20m });
            sale.Payments.Add(new SalePayment { Method = PaymentMethods.Cash, Amount = 30.83m });
            vm.OriginalSale = sale;

            vm.ReturnItems.Add(Line("P1", 10m, 1));
            Assert.Equal(10, vm.EarnedPointsToClawBack(1m));

            vm.ReturnItems.Clear();
            vm.ReturnItems.Add(Line(GiftCardRules.LineProductId, 20m, 1));
            Assert.Equal(0, vm.EarnedPointsToClawBack(1m));

            vm.ReturnItems.Clear();
            vm.ReturnItems.Add(new ReturnItemViewModel
            {
                ProductId = "P1", UnitPrice = 5m, OriginalQuantity = 2, LineQuantity = 2, ReturnQuantity = 1
            });
            Assert.Equal(5, vm.EarnedPointsToClawBack(1m));

            sale.CustomerId = null;
            Assert.Equal(0, vm.EarnedPointsToClawBack(1m));
        }

        private static ReturnItemViewModel Line(string productId, decimal unitPrice, int quantity) =>
            new()
            {
                ProductId = productId,
                UnitPrice = unitPrice,
                OriginalQuantity = quantity,
                LineQuantity = quantity,
                ReturnQuantity = quantity
            };
    }

    [Collection(GlobalStateCollection.Name)]
    public sealed class StoreRetailCheckoutTests : IDisposable
    {
        private readonly RegionSettingsData _original = Region.Current;

        public StoreRetailCheckoutTests() => Region.Save(RegionSettingsData.UnitedStates());

        public void Dispose() => Region.Save(_original);

        private static Product Bandages() => new()
        {
            Id = 1, ProductId = "P1", ProductName = "Adhesive Bandages", UnitPrice = 10m, Stock = 50
        };

        private static ITaxRepository Tax()
        {
            var tax = new Mock<ITaxRepository>();
            tax.Setup(r => r.GetSettingsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new TaxSettings(true, new[]
            {
                new TaxCategory { Id = 1, Name = "General merchandise", RatePercent = 8.25m, IsDefault = true }
            }));
            return tax.Object;
        }

        private sealed class Cards : IGiftCardRepository
        {
            public List<GiftCard> All { get; } = new();
            public Task<GiftCard?> GetByCodeAsync(string code, CancellationToken ct = default) =>
                Task.FromResult(All.FirstOrDefault(c => c.Code == GiftCardRules.Normalize(code)));
            public Task AddAsync(GiftCard card, CancellationToken ct = default)
            {
                card.Code = GiftCardRules.Normalize(card.Code);
                All.Add(card);
                return Task.CompletedTask;
            }
            public Task UpdateAsync(GiftCard card, CancellationToken ct = default) => Task.CompletedTask;
        }

        [Fact]
        public void GiftCard_IsNotTaxed_AndIsIssuedWhenTheSaleIsSaved()
        {
            Sta.Run(() =>
            {
                var cards = new Cards();
                var saved = new List<Sale>();
                var bandages = Bandages();
                var vm = SaleFixtures.NewSaleScreen(saved: saved, tax: Tax(), catalogue: new[] { bandages }, giftCards: cards,
                    shopText: Shop());
                vm.AddProductToCart(bandages);
                vm.AddGiftCard(20m, "Ada");

                Assert.Equal(30m, vm.Subtotal);
                Assert.Equal(0.83m, vm.TaxTotal); // 8.25% of the bandages only
                Assert.Equal(30.83m, vm.TotalBill);
                Assert.Contains("GC", vm.SaleItems.Single(i => i.IsGiftCardIssue).ProductName);

                vm.ReceiveCash = 40m;
                vm.SaveCommand.Execute(null);

                var card = Assert.Single(cards.All);
                Assert.Equal(20m, card.Balance);
                Assert.Equal("Ada", card.RecipientName);
                Assert.Equal(49, bandages.Stock);
                Assert.Equal(2, Assert.Single(saved).SaleItems.Count);
            });
        }

        [Fact]
        public void TwoGiftCards_OnOneSale_GetDifferentCodes()
        {
            Sta.Run(() =>
            {
                var cards = new Cards();
                var saved = new List<Sale>();
                var vm = SaleFixtures.NewSaleScreen(saved: saved, tax: Tax(), giftCards: cards, shopText: Shop());
                vm.AddGiftCard(10m, "Ada");
                vm.AddGiftCard(15m, "Grace");
                var second = vm.SaleItems.Single(i => i.GiftCardRecipient == "Grace");
                second.GiftCardCode = vm.SaleItems.Single(i => i.GiftCardRecipient == "Ada").GiftCardCode;

                vm.ReceiveCash = 30m;
                vm.SaveCommand.Execute(null);

                Assert.Equal(2, cards.All.Count);
                Assert.NotEqual(cards.All[0].Code, cards.All[1].Code);
                Assert.Equal(10m, cards.All.Single(c => c.RecipientName == "Ada").Balance);
                Assert.Equal(15m, cards.All.Single(c => c.RecipientName == "Grace").Balance);
                Assert.Single(saved);
            });
        }

        [Fact]
        public void Loyalty_EarnsOnMerchandise_AndRedeemSpendsPoints()
        {
            Sta.Run(() =>
            {
                var updated = new List<Customer>();
                var saved = new List<Sale>();
                var customer = new Customer { Id = 3, Name = "Karen Wilson", LoyaltyEnrolled = true, LoyaltyPoints = 1000 };
                var vm = SaleFixtures.NewSaleScreen(saved: saved, tax: Tax(), catalogue: new[] { Bandages() },
                    updatedCustomers: updated, shopText: Shop());
                vm.AddProductToCart(vm.Products.Single());
                vm.SelectedCustomer = customer;
                vm.ApplySplitPayments(new[]
                {
                    TenderCalculator.Take(PaymentMethods.Loyalty, 5m, 10.83m, reference: "500").Payment!,
                    TenderCalculator.Take(PaymentMethods.Cash, 5.83m, 5.83m).Payment!
                });

                vm.SaveCommand.Execute(null);

                Assert.Single(saved);
                Assert.Equal(1000 - 500 + 5, customer.LoyaltyPoints); // earned floor((10 - 5) * 1) = 5
                Assert.Contains(customer, updated);
            });
        }

        [Fact]
        public void Windows_ForGiftCards_Wording_AndSetup_Load()
        {
            Sta.Run(() =>
            {
                var gift = new GiftCardDialog();
                gift.Close();

                var split = new SplitPaymentDialog(10m, "Ada Lovelace");
                Assert.NotNull(split.FindName("GiftChoice"));
                Assert.NotNull(split.FindName("LoyaltyChoice"));
                split.Close();

                var settings = new BusinessSettingsWindow();
                Assert.NotNull(settings.FindName("cboShopLanguage"));
                Assert.NotNull(settings.FindName("gridPhrases"));
                Assert.NotNull(settings.FindName("txtPointsPerDollar"));
                Assert.NotNull(settings.FindName("txtPointsPerReward"));
                settings.Close();
            });
        }

        private static IShopTextStore Shop()
        {
            var shop = new Mock<IShopTextStore>();
            shop.Setup(s => s.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(ShopTextSettings.Default);
            return shop.Object;
        }
    }

    [Collection(GlobalStateCollection.Name)]
    public sealed class StoreRetailDatabaseTests : IDisposable
    {
        private readonly TempDataDirectory _dir = new();

        public void Dispose() => _dir.Dispose();

        [Fact]
        public async Task Wording_GiftCard_AndLoyalty_RoundTrip()
        {
            var settings = new ShopTextSettings(ShopPhrases.Spanish,
                new Dictionary<string, string> { ["sale.save"] = "Cobrar" }, 2m, 50);

            using (var db = _dir.NewMigratedContext())
            {
                await new ShopTextStore(db).SaveAsync(settings);
                var card = new GiftCard { Code = "gcabcdefgh", Balance = 25m, IssuedAmount = 25m, RecipientName = "Ada" };
                await new GiftCardRepository(db).AddAsync(card);
                db.Customers.Add(new Customer { CustomerId = "C9001", Name = "Karen Wilson", LoyaltyEnrolled = true, LoyaltyPoints = 250 });
                await db.SaveChangesAsync();
            }

            using (var db = _dir.NewMigratedContext())
            {
                var loaded = await new ShopTextStore(db).GetAsync();
                Assert.Equal(ShopPhrases.Spanish, loaded.Language);
                Assert.Equal("Cobrar", loaded.Overrides["sale.save"]);
                Assert.Equal(2m, loaded.PointsPerDollar);
                Assert.Equal(50, loaded.PointsPerRewardDollar);

                var gifts = new GiftCardRepository(db);
                var card = await gifts.GetByCodeAsync("GCABCDEFGH");
                Assert.NotNull(card);
                Assert.Equal(25m, card!.Balance);
                card.Balance -= 10m;
                await gifts.UpdateAsync(card);

                var customer = await new CustomerRepository(db).GetByCustomerIdAsync("C9001");
                Assert.True(customer!.LoyaltyEnrolled);
                Assert.Equal(250, customer.LoyaltyPoints);
            }

            using (var db = _dir.NewMigratedContext())
            {
                var card = await new GiftCardRepository(db).GetByCodeAsync("GCABCDEFGH");
                Assert.Equal(15m, card!.Balance);

                GiftCardRules.Credit(card, 10m);
                await new GiftCardRepository(db).UpdateAsync(card);
                Assert.Null(GiftCardRules.ReverseIssue(card, 25m));
                await new GiftCardRepository(db).UpdateAsync(card);
            }

            using (var db = _dir.NewMigratedContext())
            {
                var card = await new GiftCardRepository(db).GetByCodeAsync("GCABCDEFGH");
                Assert.Equal(0m, card!.Balance);
                db.Customers.Add(new Customer { CustomerId = "C9002", Name = "Walk In" });
                await db.SaveChangesAsync();
                db.ChangeTracker.Clear();
                var untouched = await new CustomerRepository(db).GetByCustomerIdAsync("C9002");
                Assert.False(untouched!.LoyaltyEnrolled);
                Assert.Equal(0, untouched.LoyaltyPoints);
            }
        }

        [Fact]
        public void Sale_OnARealDatabase_IssuesAGiftCard_EarnsPoints_ThenASecondSaleSpendsBoth()
        {
            var original = Region.Current;
            Region.Save(RegionSettingsData.UnitedStates());
            try
            {
                using (var db = _dir.NewMigratedContext())
                {
                    db.Products.Add(new Product
                    {
                        ProductId = "P1", Barcode = "P1", ProductName = "Adhesive Bandages", UnitPrice = 10m, Stock = 50
                    });
                    db.Customers.Add(new Customer
                    {
                        CustomerId = "C1", Name = "Karen Wilson", LoyaltyEnrolled = true, LoyaltyPoints = 0
                    });
                    db.SaveChanges();
                }

                PumpSale(vm =>
                {
                    vm.SelectedCustomer = vm.Customers.Single(c => c.CustomerId == "C1");
                    vm.AddProductToCart(vm.Products.Single(p => p.ProductId == "P1"));
                    vm.AddGiftCard(20m, "Ada");
                    vm.ReceiveCash = 40m;
                });

                string code;
                using (var db = _dir.NewMigratedContext())
                {
                    var card = Assert.Single(db.GiftCards);
                    code = card.Code;
                    Assert.Equal(20m, card.Balance);
                    Assert.Equal("Ada", card.RecipientName);
                    Assert.Equal(10, db.Customers.Single(c => c.CustomerId == "C1").LoyaltyPoints);
                    Assert.Equal(49, db.Products.Single(p => p.ProductId == "P1").Stock);

                    var sale = db.Sales.Include(s => s.SaleItems).Include(s => s.Payments).Single();
                    Assert.Equal(30.83m, sale.TotalBill);
                    Assert.Equal(0.83m, sale.TaxTotal);
                    var giftLine = sale.SaleItems.Single(i => i.ProductId == GiftCardRules.LineProductId);
                    Assert.Equal(0m, giftLine.TaxAmount);
                    Assert.Contains(code, giftLine.ProductName);
                    Assert.Equal(30.83m, Assert.Single(sale.Payments).Amount);
                }

                PumpSale(vm =>
                {
                    vm.SelectedCustomer = vm.Customers.Single(c => c.CustomerId == "C1");
                    vm.AddProductToCart(vm.Products.Single(p => p.ProductId == "P1"));
                    vm.ApplySplitPayments(new[]
                    {
                        TenderCalculator.Take(PaymentMethods.Loyalty, 0.10m, 10.83m, reference: "10").Payment!,
                        TenderCalculator.Take(PaymentMethods.GiftCard, 5m, 10.73m, reference: code).Payment!,
                        TenderCalculator.Take(PaymentMethods.Cash, 5.73m, 5.73m).Payment!
                    });
                });

                using (var db = _dir.NewMigratedContext())
                {
                    Assert.Equal(15m, db.GiftCards.Single().Balance);
                    // 10 points spent, floor((10.00 - 0.10) * 1) = 9 earned → 9 left.
                    Assert.Equal(9, db.Customers.Single(c => c.CustomerId == "C1").LoyaltyPoints);
                    Assert.Equal(48, db.Products.Single(p => p.ProductId == "P1").Stock);
                    Assert.Equal(2, db.Sales.Count());
                }
            }
            finally
            {
                Region.Save(original);
            }
        }

        /// <summary>Builds a sale screen on this test database and waits until Save finishes.</summary>
        private void PumpSale(Action<SaleViewModel> fill)
        {
            Sta.Run(() =>
            {
                SynchronizationContext.SetSynchronizationContext(
                    new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
                using var db = _dir.NewMigratedContext();
                var tax = new Mock<ITaxRepository>();
                tax.Setup(r => r.GetSettingsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new TaxSettings(true, new[]
                {
                    new TaxCategory { Id = 1, Name = "General merchandise", RatePercent = 8.25m, IsDefault = true }
                }));
                var vm = new SaleViewModel(
                    new SaleRepository(db),
                    new ProductRepository(db),
                    new CustomerRepository(db),
                    taxRepository: tax.Object,
                    giftCards: new GiftCardRepository(db),
                    shopText: new ShopTextStore(db));

                var frame = new DispatcherFrame();
                var started = false;
                string? failed = null;
                var ticks = 0;
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
                timer.Tick += (_, _) =>
                {
                    ticks++;
                    if (ticks > 200)
                    {
                        failed = "The sale did not finish.";
                        frame.Continue = false;
                        return;
                    }
                    if (!started)
                    {
                        if (!vm.TaxApplies || vm.Products.Count == 0 || vm.Customers.Count == 0) return;
                        fill(vm);
                        started = true;
                        vm.SaveCommand.Execute(null);
                        return;
                    }
                    if (vm.SaleItems.Count == 0)
                        frame.Continue = false;
                };
                timer.Start();
                Dispatcher.PushFrame(frame);
                timer.Stop();
                Assert.Null(failed);
            });
        }
    }
}
