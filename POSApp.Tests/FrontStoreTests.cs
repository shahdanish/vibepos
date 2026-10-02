using Microsoft.EntityFrameworkCore;
using Moq;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.Core.Services;
using POSApp.Infrastructure.Repositories;
using POSApp.UI.Helpers;
using POSApp.UI.ViewModels;

namespace POSApp.Tests
{
    /// <summary>The pure front-store rules.</summary>
    public sealed class FrontStoreRuleTests
    {
        [Theory]
        [InlineData("2008-10-02", "2026-10-02", 18)]   // 18th birthday today
        [InlineData("2008-10-03", "2026-10-02", 17)]   // tomorrow
        [InlineData("2005-10-02", "2026-10-02", 21)]
        [InlineData("2008-02-29", "2026-02-28", 17)]   // leap-day birthday, not yet
        [InlineData("2008-02-29", "2026-03-01", 18)]
        public void Age_CountsCompletedYears(string dob, string on, int expected)
        {
            Assert.Equal(expected, AgeCheck.AgeOn(DateTime.Parse(dob), DateTime.Parse(on)));
        }

        [Fact]
        public void Pse_WithinLimits_IsAllowed()
        {
            var now = new DateTime(2026, 10, 2, 12, 0, 0);
            var r = PseLimits.Check(new[] { (now.AddDays(-3), 2000m) }, 1180m, now);
            Assert.True(r.Allowed);
            Assert.Equal(1180m, r.TodayMg);
            Assert.Equal(3180m, r.ThirtyDayMg);
        }

        [Fact]
        public void Pse_OverTheDailyLimit_IsRefused()
        {
            var now = new DateTime(2026, 10, 2, 12, 0, 0);
            var r = PseLimits.Check(new[] { (now.AddHours(-2), 3000m) }, 983m, now);
            Assert.False(r.Allowed);
            Assert.Contains("3.6 g a day", r.Reason);
        }

        [Fact]
        public void Pse_OverThe30DayLimit_IsRefused_AndOlderPurchasesDropOff()
        {
            var now = new DateTime(2026, 10, 2, 12, 0, 0);
            var month = Enumerable.Range(1, 3).Select(i => (now.AddDays(-i * 7), 3000m)).ToList();   // 9 g in 3 weeks
            Assert.False(PseLimits.Check(month, 590m, now).Allowed);

            var old = Enumerable.Range(31, 3).Select(i => (now.AddDays(-i), 3000m)).ToList();        // over 30 days ago
            Assert.True(PseLimits.Check(old, 590m, now).Allowed);
        }

        [Fact]
        public void Pse_StricterStateLimits_AreApplied()
        {
            var now = new DateTime(2026, 10, 2);
            Assert.False(PseLimits.Check(Array.Empty<(DateTime, decimal)>(), 983m, now, dailyLimitMg: 900m).Allowed);
        }

        [Fact]
        public void Fsa_CountsEligibleItemsAndTheirTax()
        {
            var lines = new[] { new FsaLine(true, 10m, 0.83m), new FsaLine(false, 5m, 0.41m), new FsaLine(true, 2m, 0m) };
            Assert.Equal(12.83m, FsaEligibility.EligibleAmount(lines));
        }

        [Fact]
        public void IdNumbers_MatchWhateverTheFormatting()
        {
            Assert.Equal("D1234567", PseLogEntry.NormalizeId(" d123-4567 "));
        }

        [Fact]
        public void FrontStorePharmacy_IsPro()
        {
            Assert.False(EditionPolicy.IsEnabled(AppEdition.StoreLite, AppFeature.FrontStorePharmacy));
            Assert.True(EditionPolicy.IsEnabled(AppEdition.StorePro, AppFeature.FrontStorePharmacy));
            Assert.True(EditionPolicy.IsEnabled(AppEdition.Direct, AppFeature.FrontStorePharmacy));
        }
    }

    /// <summary>The US sale screen's ID checks, PSE logbook and FSA/HSA amount.</summary>
    [Collection(GlobalStateCollection.Name)]
    public sealed class FrontStoreSaleTests : IDisposable
    {
        private readonly RegionSettingsData _original = Region.Current;

        public FrontStoreSaleTests() => Region.Save(RegionSettingsData.UnitedStates());

        public void Dispose() => Region.Save(_original);

        private static readonly Product Gum = new() { Id = 1, ProductId = "N1", ProductName = "Nicotine Gum", UnitPrice = 11.99m, Stock = 10, MinimumAge = 18 };
        private static readonly Product CoughSyrup = new() { Id = 2, ProductId = "D1", ProductName = "DXM Cough Syrup", UnitPrice = 7.99m, Stock = 10, MinimumAge = 18 };
        private static readonly Product Pse = new() { Id = 3, ProductId = "P1", ProductName = "Pseudoephedrine 30 mg, 24 ct", UnitPrice = 6.49m, Stock = 10, IsPse = true, PseBaseMgPerPack = 590m };
        private static readonly Product Bandages = new() { Id = 4, ProductId = "B1", ProductName = "Bandages", UnitPrice = 10m, Stock = 10, IsFsaEligible = true };
        private static readonly Product Candy = new() { Id = 5, ProductId = "C1", ProductName = "Candy", UnitPrice = 5m, Stock = 10 };

        private static SaleViewModel Screen(List<Sale>? saved = null) =>
            SaleFixtures.NewSaleScreen(saved: saved, catalogue: new[] { Gum, CoughSyrup, Pse, Bandages, Candy });

        [Fact]
        public void AgeRestrictedItem_AsksForIdOnce_PerSale()
        {
            Sta.Run(() =>
            {
                var vm = Screen();
                var asked = 0;
                vm.RequestIdCheck = (age, _) => { asked++; Assert.Equal(18, age); return new DateTime(1990, 5, 1); };

                Assert.True(vm.AddProductToCart(Gum));
                Assert.True(vm.AddProductToCart(CoughSyrup));
                Assert.Equal(1, asked);
                Assert.Equal(18, vm.IdCheckedAge);
                Assert.Equal(2, vm.SaleItems.Count);
            });
        }

        [Fact]
        public void CancelledIdCheck_LeavesTheItemOut()
        {
            Sta.Run(() =>
            {
                var vm = Screen();
                vm.RequestIdCheck = (_, _) => null;
                Assert.False(vm.AddProductToCart(Gum));
                Assert.Empty(vm.SaleItems);
            });
        }

        [Fact]
        public void PseSale_IsSavedWithItsLogbookLines()
        {
            Sta.Run(() =>
            {
                var saved = new List<Sale>();
                var vm = Screen(saved);
                vm.RequestPseLog = lines =>
                {
                    var line = Assert.Single(lines);
                    Assert.Equal(1180m, line.BaseMg);             // two packages × 590 mg
                    return new[] { new PseLogEntry { PurchaserName = "Pat Doe", IdType = "Driver license", IdNumber = "D1234567", ProductId = line.ProductId, ProductName = line.ProductName, Packages = line.Packages, BaseMg = line.BaseMg } };
                };
                vm.AddProductToCart(Pse);
                vm.AddProductToCart(Pse);
                vm.SaveCommand.Execute(null);

                var entry = Assert.Single(Assert.Single(saved).PseLogEntries);
                Assert.Equal("Pat Doe", entry.PurchaserName);
                Assert.Equal(2m, entry.Packages);
                Assert.NotEqual(default, entry.PurchaseDate);
            });
        }

        [Fact]
        public void PseSale_WithoutTheLogbook_IsNotSaved()
        {
            Sta.Run(() =>
            {
                var saved = new List<Sale>();
                var vm = Screen(saved);
                vm.RequestPseLog = _ => null;    // cancelled, or over the limit
                vm.AddProductToCart(Pse);
                vm.SaveCommand.Execute(null);
                Assert.Empty(saved);
                Assert.Single(vm.SaleItems);     // the cart is kept
            });
        }

        [Fact]
        public void FsaEligibleTotal_CoversEligibleItemsOnly_AndIsOnTheReceipt()
        {
            var text = Sta.Run(() =>
            {
                var vm = Screen();
                vm.AddProductToCart(Bandages);
                vm.AddProductToCart(Candy);
                Assert.Equal(10m, vm.FsaEligibleTotal);
                Assert.True(vm.ResolvePayments());
                return Sta.TextOf(vm.CreateProfessionalInvoice());
            });
            Assert.Contains("\tFSA/HSA eligible\t10.00\n", text);
        }
    }

    /// <summary>Front-store data against a real migrated database.</summary>
    [Collection(GlobalStateCollection.Name)]
    public sealed class FrontStoreDatabaseTests : IDisposable
    {
        private readonly TempDataDirectory _dir = new();

        public void Dispose() => _dir.Dispose();

        [Fact]
        public async Task Roles_AreAddedByName_Once_EvenWhenAShopsOwnRoleHoldsTheNextId()
        {
            using var db = _dir.NewMigratedContext();
            // A shop-made role takes the next free id first.
            db.Roles.Add(new Role { Name = "Night Shift", Description = "custom" });
            await db.SaveChangesAsync();

            var repo = new FrontStoreRepository(db);
            await repo.EnsureRolesAsync();
            await repo.EnsureRolesAsync();

            Assert.Equal(1, await db.Roles.CountAsync(r => r.Name == FrontStoreRepository.PharmacistRole));
            Assert.Equal(1, await db.Roles.CountAsync(r => r.Name == FrontStoreRepository.TechnicianRole));
            Assert.Equal(1, await db.Permissions.CountAsync(p => p.Name == Permissions.PseLogView));

            var pharmacist = await db.Roles.Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
                .SingleAsync(r => r.Name == FrontStoreRepository.PharmacistRole);
            Assert.Contains(pharmacist.RolePermissions, rp => rp.Permission.Name == Permissions.PseLogView);
            Assert.Contains(pharmacist.RolePermissions, rp => rp.Permission.Name == Permissions.ProductsManage);
            Assert.DoesNotContain(pharmacist.RolePermissions, rp => rp.Permission.Name == Permissions.UsersManage);

            var admin = await db.Roles.Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission).SingleAsync(r => r.Name == "Admin");
            Assert.Contains(admin.RolePermissions, rp => rp.Permission.Name == Permissions.PseLogView);
        }

        [Fact]
        public async Task AShopsOwnPharmacistRole_IsLeftAlone()
        {
            using var db = _dir.NewMigratedContext();
            db.Roles.Add(new Role { Name = FrontStoreRepository.PharmacistRole, Description = "ours" });
            await db.SaveChangesAsync();

            await new FrontStoreRepository(db).EnsureRolesAsync();

            var role = await db.Roles.Include(r => r.RolePermissions).SingleAsync(r => r.Name == FrontStoreRepository.PharmacistRole);
            Assert.Equal("ours", role.Description);
            Assert.Empty(role.RolePermissions);
        }

        [Fact]
        public async Task Settings_DefaultToFederalLimits_AndSave()
        {
            using var db = _dir.NewMigratedContext();
            var repo = new FrontStoreRepository(db);
            Assert.Equal(FrontStoreSettings.Default, await repo.GetSettingsAsync());

            await repo.SaveSettingsAsync(new FrontStoreSettings(1800m, 7500m, BlockExpired: false));
            Assert.Equal(new FrontStoreSettings(1800m, 7500m, false), await repo.GetSettingsAsync());
        }

        [Fact]
        public async Task Logbook_IsSavedWithTheSale_AndFoundById()
        {
            using (var db = _dir.NewMigratedContext())
            {
                var sale = new Sale { InvoiceNumber = "11020", SaleDate = new DateTime(2026, 10, 2, 9, 0, 0), TotalBill = 6.49m };
                sale.PseLogEntries.Add(new PseLogEntry { PurchaseDate = sale.SaleDate, PurchaserName = "Pat Doe", IdType = "State ID", IdNumber = "D1234567", ProductName = "PSE 30 mg", Packages = 1, BaseMg = 590m, RecordedBy = "owner" });
                await new SaleRepository(db).AddAsync(sale);
            }

            using (var db = _dir.NewMigratedContext())
            {
                var repo = new FrontStoreRepository(db);
                var found = await repo.GetPsePurchasesAsync("d123-4567", new DateTime(2026, 9, 1));
                Assert.Equal(590m, Assert.Single(found).BaseMg);
                Assert.NotNull(found[0].SaleId);
                Assert.Single(await repo.GetPseLogAsync(new DateTime(2026, 10, 1), new DateTime(2026, 10, 2)));
            }
        }

        [Fact]
        public async Task Expiring_ListsExpiredAndSoon_SoonestFirst()
        {
            using var db = _dir.NewMigratedContext();
            db.Products.AddRange(
                new Product { ProductId = "E1", ProductName = "Later", ExpiryDate = new DateTime(2026, 12, 1) },
                new Product { ProductId = "E2", ProductName = "Expired", ExpiryDate = new DateTime(2026, 9, 1) },
                new Product { ProductId = "E3", ProductName = "Next year", ExpiryDate = new DateTime(2027, 6, 1) });
            await db.SaveChangesAsync();

            var list = await new FrontStoreRepository(db).GetExpiringAsync(new DateTime(2026, 12, 31));
            Assert.Equal(new[] { "Expired", "Later" }, list.Select(p => p.ProductName));
        }
    }
}
