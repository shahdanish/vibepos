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
    /// <summary>
    /// Quick keys are stored in the existing UserFavorites table but are shop-wide: one tile
    /// per product, whoever starred it. The migrated database has seeded products 1 and 2
    /// and users 1 (admin) and 2 (cashier).
    /// </summary>
    [Collection(GlobalStateCollection.Name)]
    public sealed class QuickKeyRepositoryTests : IDisposable
    {
        private readonly TempDataDirectory _dir = new();

        public void Dispose() => _dir.Dispose();

        private static async Task<int[]> TileOrder(FavoriteRepository repo) =>
            (await repo.GetQuickKeyProductsAsync()).Select(p => p.Id).ToArray();

        [Fact]
        public async Task Star_ThenUnstar()
        {
            using var db = _dir.NewMigratedContext();
            var repo = new FavoriteRepository(db);

            await repo.SetQuickKeyAsync(1, userId: 1, isQuickKey: true);
            Assert.Equal(new[] { 1 }, await TileOrder(repo));
            Assert.Contains(1, await repo.GetQuickKeyProductIdsAsync());

            await repo.SetQuickKeyAsync(1, userId: 1, isQuickKey: false);
            Assert.Empty(await TileOrder(repo));
            Assert.Empty(await repo.GetQuickKeyProductIdsAsync());
        }

        [Fact]
        public async Task StarredByTwoCashiers_IsOneTile_AndEitherCanRemoveIt()
        {
            using var db = _dir.NewMigratedContext();
            var repo = new FavoriteRepository(db);

            await repo.SetQuickKeyAsync(1, userId: 1, isQuickKey: true);
            await repo.SetQuickKeyAsync(1, userId: 2, isQuickKey: true); // already a quick key: no-op

            Assert.Equal(new[] { 1 }, await TileOrder(repo));
            Assert.Equal(1, await db.UserFavorites.CountAsync());

            await repo.SetQuickKeyAsync(1, userId: 2, isQuickKey: false);
            Assert.Empty(await TileOrder(repo));
        }

        [Fact]
        public async Task NewStarsGoLast_AndAReorderSticks()
        {
            using var db = _dir.NewMigratedContext();
            var repo = new FavoriteRepository(db);

            await repo.SetQuickKeyAsync(2, userId: 1, isQuickKey: true);
            await repo.SetQuickKeyAsync(1, userId: 1, isQuickKey: true);
            Assert.Equal(new[] { 2, 1 }, await TileOrder(repo));

            await repo.ReorderQuickKeysAsync(new[] { 1, 2 });
            Assert.Equal(new[] { 1, 2 }, await TileOrder(repo));
        }

        [Fact]
        public async Task DeletedProduct_HasNoTile()
        {
            using var db = _dir.NewMigratedContext();
            var repo = new FavoriteRepository(db);
            await repo.SetQuickKeyAsync(1, userId: 1, isQuickKey: true);
            await repo.SetQuickKeyAsync(2, userId: 1, isQuickKey: true);

            var product = await db.Products.SingleAsync(p => p.Id == 1);
            product.IsDeleted = true;
            await db.SaveChangesAsync();

            Assert.Equal(new[] { 2 }, await TileOrder(repo));
        }

        [Fact]
        public async Task FavouritesFromBeforeTheUpgrade_BecomeTiles_InTheOrderTheyWereAdded()
        {
            using var db = _dir.NewMigratedContext();
            // Rows written by the old per-user favourites: SortOrder is the migration default 0.
            db.UserFavorites.AddRange(
                new UserFavorite { UserId = 1, ProductId = 2, ProductName = "Glue Stick", AddedDate = new DateTime(2026, 1, 1) },
                new UserFavorite { UserId = 2, ProductId = 2, ProductName = "Glue Stick", AddedDate = new DateTime(2026, 3, 1) },
                new UserFavorite { UserId = 2, ProductId = 1, ProductName = "Glycerin 25gm", AddedDate = new DateTime(2026, 2, 1) });
            await db.SaveChangesAsync();

            var repo = new FavoriteRepository(db);
            Assert.Equal(new[] { 2, 1 }, await TileOrder(repo));

            // A product starred now goes after them.
            db.Products.Add(new Product { Id = 3, ProductId = "3003", ProductName = "Item 3", Stock = 5 });
            await db.SaveChangesAsync();
            await repo.SetQuickKeyAsync(3, userId: 1, isQuickKey: true);
            Assert.Equal(new[] { 2, 1, 3 }, await TileOrder(repo));
        }
    }

    /// <summary>The quick-key tiles and F1–F12 on the sale screens.</summary>
    [Collection(GlobalStateCollection.Name)]
    public sealed class SaleScreenQuickKeyTests : IDisposable
    {
        private readonly RegionSettingsData _original = Region.Current;

        public SaleScreenQuickKeyTests() => Region.Save(RegionSettingsData.PresetFor(RegionCodes.UnitedStates)!);

        public void Dispose() => Region.Save(_original);

        private static List<Product> Products(int count) =>
            Enumerable.Range(1, count).Select(i => new Product
            {
                Id = i, ProductId = $"P{i}", ProductName = i == 2 ? "Bandage Roll" : $"Item {i}",
                UnitPrice = 2.5m * i, WholesalePrice = 2m * i, CostPrice = 1, Stock = 50
            }).ToList();

        private static IFavoriteRepository Favorites(List<Product> products)
        {
            var favorites = new Mock<IFavoriteRepository>();
            favorites.Setup(r => r.GetQuickKeyProductsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(products);
            return favorites.Object;
        }

        [Fact]
        public void TheFirstTwelveTiles_GetF1ToF12()
        {
            Sta.Run(() =>
            {
                var vm = SaleFixtures.NewSaleScreen(favorites: Favorites(Products(13)));

                Assert.Equal(13, vm.QuickKeys.Count);
                Assert.Equal("F1", vm.QuickKeys[0].HotKey);
                Assert.Equal("F12", vm.QuickKeys[11].HotKey);
                Assert.Null(vm.QuickKeys[12].HotKey);
                Assert.Equal("$2.50", vm.QuickKeys[0].PriceText);
            });
        }

        [Fact]
        public void HotKey_AddsOneUnit_ThenOneMore()
        {
            Sta.Run(() =>
            {
                var vm = SaleFixtures.NewSaleScreen(favorites: Favorites(Products(3)));

                Assert.True(vm.TryAddQuickKey(1));
                Assert.True(vm.TryAddQuickKey(1));

                var line = Assert.Single(vm.SaleItems);
                Assert.Equal("P2", line.ProductId);
                Assert.Equal(2, line.Quantity);
                Assert.Equal(5m, line.UnitPrice);
            });
        }

        [Fact]
        public void HotKeyWithoutATile_DoesNothing()
        {
            Sta.Run(() =>
            {
                var vm = SaleFixtures.NewSaleScreen(favorites: Favorites(Products(13)));

                Assert.False(vm.TryAddQuickKey(12)); // the 13th tile has no F-key
                Assert.False(vm.TryAddQuickKey(-1));
                Assert.Empty(vm.SaleItems);
            });
        }

        [Fact]
        public void Filter_NarrowsTheTiles_ButFKeysKeepTheirProduct()
        {
            Sta.Run(() =>
            {
                var vm = SaleFixtures.NewSaleScreen(favorites: Favorites(Products(3)));

                vm.QuickKeySearch = "bandage";
                var tile = Assert.Single(vm.QuickKeys);
                Assert.Equal("F2", tile.HotKey);

                Assert.True(vm.TryAddQuickKey(0)); // F1 is still Item 1
                Assert.Equal("P1", Assert.Single(vm.SaleItems).ProductId);
            });
        }

        [Fact]
        public void Wholesale_TilesAndCartUseTheWholesalePrice()
        {
            Sta.Run(() =>
            {
                var vm = SaleFixtures.NewSaleScreen(favorites: Favorites(Products(2)), wholesale: true);

                Assert.Equal("$4.00", vm.QuickKeys[1].PriceText);
                Assert.True(vm.TryAddQuickKey(1));
                Assert.Equal(4m, Assert.Single(vm.SaleItems).UnitPrice);
            });
        }

        [Fact]
        public void NoQuickKeys_PanelStaysHidden_SoTheScreenIsUnchanged()
        {
            Sta.Run(() =>
            {
                var none = SaleFixtures.NewSaleScreen(favorites: Favorites(new List<Product>()));
                Assert.False(none.HasQuickKeys);
                Assert.False(none.IsQuickKeysPanelVisible);

                var some = SaleFixtures.NewSaleScreen(favorites: Favorites(Products(2)));
                Assert.True(some.IsQuickKeysPanelVisible);
                some.ShowQuickKeys = false;
                Assert.False(some.IsQuickKeysPanelVisible);
                some.ShowQuickKeys = true;
            });
        }

        [Fact]
        public void AddingToTheCart_ReportsTheLine_SoTheScreenCanScrollToIt()
        {
            Sta.Run(() =>
            {
                var vm = SaleFixtures.NewSaleScreen(favorites: Favorites(Products(2)));
                var reported = new List<SaleItemViewModel>();
                vm.CartLineChanged += reported.Add;

                vm.TryAddQuickKey(0);
                vm.TryAddQuickKey(1);
                vm.TryAddQuickKey(0); // one more of the first line

                Assert.Equal(new[] { "P1", "P2", "P1" }, reported.Select(l => l.ProductId));
                Assert.Same(vm.SaleItems[0], reported[2]);
            });
        }

        [Fact]
        public void LowAndOutOfStock_AreBadged()
        {
            var low = new QuickKeyTile(new Product { ProductName = "A", Stock = 3, MinStockThreshold = 10 }, "$1.00", "F1");
            var none = new QuickKeyTile(new Product { ProductName = "B", Stock = 0 }, "$1.00", "F2");
            var fine = new QuickKeyTile(new Product { ProductName = "C", Stock = 40, MinStockThreshold = 10 }, "$1.00", null);

            Assert.Equal("Low · 3", low.StockBadge);
            Assert.Equal("Out", none.StockBadge);
            Assert.False(fine.HasStockBadge);
        }
    }

    /// <summary>The ☆ column in Inventory → Products.</summary>
    public sealed class ProductQuickKeyToggleTests
    {
        [Fact]
        public async Task Star_AddsTheProduct_AndRefreshesTheStars()
        {
            var products = new Mock<IProductRepository>();
            products.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Product>());
            var categories = new Mock<ICategoryRepository>();
            categories.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Category>());

            var starred = new HashSet<int>();
            var favorites = new Mock<IFavoriteRepository>();
            favorites.Setup(r => r.GetQuickKeyProductIdsAsync(It.IsAny<CancellationToken>()))
                     .ReturnsAsync(() => new HashSet<int>(starred));
            favorites.Setup(r => r.SetQuickKeyAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                     .Callback((int id, int _, bool on, CancellationToken _) => { if (on) starred.Add(id); else starred.Remove(id); })
                     .Returns(Task.CompletedTask);

            var vm = new ProductManagementViewModel(products.Object, categories.Object, favorites.Object);
            var product = new Product { Id = 7, ProductName = "Item 7" };

            await vm.ToggleQuickKeyAsync(product);
            Assert.Contains(7, vm.QuickKeyIds);

            await vm.ToggleQuickKeyAsync(product);
            Assert.DoesNotContain(7, vm.QuickKeyIds);
            favorites.Verify(r => r.SetQuickKeyAsync(7, It.IsAny<int>(), true, It.IsAny<CancellationToken>()), Times.Once);
            favorites.Verify(r => r.SetQuickKeyAsync(7, It.IsAny<int>(), false, It.IsAny<CancellationToken>()), Times.Once);
        }
    }

    /// <summary>"Khata" is kept for Pakistan; other regions see the US term.</summary>
    [Collection(GlobalStateCollection.Name)]
    public sealed class CustomerAccountsLabelTests : IDisposable
    {
        private readonly RegionSettingsData _original = Region.Current;

        public void Dispose() => Region.Save(_original);

        [Fact]
        public void Pakistan_KeepsKhata()
        {
            Region.Save(RegionSettingsData.PresetFor(RegionCodes.Pakistan)!);
            Assert.Equal("Khata", Region.CustomerAccountsLabel);
            Assert.Equal("📒 Khata", Region.CustomerAccountsMenuHeader);
            Assert.Equal("Customer Ledger (Khata)", Region.CustomerAccountsTitle);
        }

        [Fact]
        public void UnitedStates_SaysChargeAccounts()
        {
            Region.Save(RegionSettingsData.PresetFor(RegionCodes.UnitedStates)!);
            Assert.Equal("Charge Accounts", Region.CustomerAccountsLabel);
            Assert.Equal("Customer Charge Accounts", Region.CustomerAccountsTitle);
        }
    }
}
