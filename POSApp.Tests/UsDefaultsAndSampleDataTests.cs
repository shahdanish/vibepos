using System.IO;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using POSApp.Core.Interfaces;
using POSApp.Core.Services;
using POSApp.Infrastructure.Repositories;
using POSApp.Infrastructure.SampleData;
using POSApp.Infrastructure.Services;

namespace POSApp.Tests
{
    /// <summary>New installs are US shops; the tills already in use keep the Pakistani behaviour they had.</summary>
    [Collection(GlobalStateCollection.Name)]
    public sealed class UsDefaultsTests : IDisposable
    {
        private readonly TempDataDirectory _dir = new();

        public void Dispose() => _dir.Dispose();

        [Fact]
        public void NewSettings_AreTheUnitedStates()
        {
            var s = new RegionSettingsData();
            Assert.Equal(RegionCodes.UnitedStates, s.RegionCode);
            Assert.Equal("en-US", s.Culture);
            Assert.Equal("$", s.CurrencySymbol);
            Assert.Equal("USD", s.CurrencyCode);
            Assert.False(s.SymbolSpacing);
            Assert.Equal(2, s.CompactDecimalPlaces);
            Assert.Equal(DateStyle.MonthFirst, s.Dates);
            Assert.Equal(TimeStyle.TwelveHour, s.Times);
            Assert.Equal(NumberWordStyle.International, s.NumberWords);
            Assert.Equal("ID Number", s.NationalIdLabel);
            Assert.Equal("Social Security", s.StatutoryDeductionLabel);
            Assert.Equal("$1,250.00", new FormatService(s).Money(1250m));
        }

        [Fact]
        public void PakistanPreset_KeepsEveryOriginalValue()
        {
            var pk = RegionSettingsData.Pakistan();
            Assert.Equal(RegionCodes.Pakistan, pk.RegionCode);
            Assert.Equal("en-PK", pk.Culture);
            Assert.Equal("Rs.", pk.CurrencySymbol);
            Assert.Equal("Rupees", pk.CurrencyName);
            Assert.Equal("PKR", pk.CurrencyCode);
            Assert.Equal("CNIC", pk.NationalIdLabel);
            Assert.Equal("EOBI", pk.StatutoryDeductionLabel);
            Assert.Equal(NumberWordStyle.SouthAsian, pk.NumberWords);
            Assert.Equal(SymbolPosition.Before, pk.SymbolSide);
            Assert.True(pk.SymbolSpacing);
            Assert.Equal(2, pk.DecimalPlaces);
            Assert.Equal(0, pk.CompactDecimalPlaces);
            Assert.Equal(NumberFormatStyle.DotDecimal, pk.NumberFormat);
            Assert.Equal(DateStyle.DayFirst, pk.Dates);
            Assert.Equal(TimeStyle.TwelveHourPadded, pk.Times);
        }

        [Fact]
        public void OlderBuildsFile_WithADollarSign_StillReadsAsPakistan()
        {
            // The shape a pre-US-defaults build wrote, as found on a developer PC: no RegionCode,
            // Culture, CurrencyCode, SymbolSpacing, CompactDecimalPlaces or Times.
            const string legacyJson = """
                {
                  "CurrencySymbol": "$",
                  "CurrencyName": "Dollar",
                  "NationalIdLabel": "CNIC",
                  "StatutoryDeductionLabel": "EOBI",
                  "NumberWords": 0,
                  "SymbolSide": 0,
                  "DecimalPlaces": 2,
                  "NumberFormat": 0,
                  "Dates": 0
                }
                """;

            var s = RegionSettingsStore.Deserialize(legacyJson);

            Assert.Equal("$", s.CurrencySymbol);                // what the file says wins
            Assert.Equal(NumberWordStyle.International, s.NumberWords);
            Assert.Equal(RegionCodes.Pakistan, s.RegionCode);   // what it lacks comes from Pakistan
            Assert.Equal("PKR", s.CurrencyCode);
            Assert.True(s.SymbolSpacing);
            Assert.Equal(0, s.CompactDecimalPlaces);
            Assert.Equal(TimeStyle.TwelveHourPadded, s.Times);
        }

        [Fact]
        public void CurrentFile_MissingAProperty_TakesItFromItsOwnCountry()
        {
            var file = System.Text.Json.Nodes.JsonNode.Parse(RegionSettingsStore.Serialize(RegionSettingsData.UnitedStates()))!.AsObject();
            Assert.True(file.Remove("Times"));
            var json = file.ToJsonString();

            Assert.Equal(TimeStyle.TwelveHour, RegionSettingsStore.Deserialize(json).Times);
        }

        [Fact]
        public void ExistingInstallWithoutAFile_IsPinnedToPakistan_Once()
        {
            Assert.False(File.Exists(RegionSettingsStore.FilePath));

            Assert.True(RegionSettingsStore.KeepLegacyDefaultsIfUnset());
            RegionSettingsStore.Reload();
            Assert.Equal(RegionCodes.Pakistan, RegionSettingsStore.Current.RegionCode);
            Assert.Equal("Rs. 1,250.00", new FormatService(RegionSettingsStore.Current).Money(1250m));

            Assert.False(RegionSettingsStore.KeepLegacyDefaultsIfUnset());
        }

        [Fact]
        public void ExistingInstallWithAFile_IsLeftAlone()
        {
            RegionSettingsStore.Save(RegionSettingsData.UnitedStates());

            Assert.False(RegionSettingsStore.KeepLegacyDefaultsIfUnset());
            RegionSettingsStore.Reload();
            Assert.Equal(RegionCodes.UnitedStates, RegionSettingsStore.Current.RegionCode);
        }

        [Fact]
        public async Task InstallOrigin_IsRecordedOnce()
        {
            using (var db = _dir.NewMigratedContext())
            {
                var first = await new FirstRunSetupService(db).RecordInstallOriginAsync(databaseExistedAtStart: false);
                Assert.Equal(new InstallOriginResult(InstallOrigin.New, FirstSeen: true), first);
            }

            using (var db = _dir.NewMigratedContext())
            {
                // Next start: the database now exists, but it is still the new install it was.
                var again = await new FirstRunSetupService(db).RecordInstallOriginAsync(databaseExistedAtStart: true);
                Assert.Equal(new InstallOriginResult(InstallOrigin.New, FirstSeen: false), again);
            }
        }

        [Fact]
        public async Task DatabaseFromBeforeTheChange_IsRecordedAsUpgraded()
        {
            using var db = _dir.NewMigratedContext();
            var origin = await new FirstRunSetupService(db).RecordInstallOriginAsync(databaseExistedAtStart: true);
            Assert.Equal(new InstallOriginResult(InstallOrigin.Upgraded, FirstSeen: true), origin);
        }
    }

    /// <summary>The optional sample data offered in first-run setup.</summary>
    [Collection(GlobalStateCollection.Name)]
    public sealed class UsPharmacySampleDataTests : IDisposable
    {
        private readonly TempDataDirectory _dir = new();

        public void Dispose() => _dir.Dispose();

        private static readonly string[] BrandNames =
        {
            "Tylenol", "Advil", "Motrin", "Aleve", "Bayer", "Excedrin", "Mucinex", "Robitussin", "Delsym", "NyQuil",
            "DayQuil", "Vicks", "Halls", "Chloraseptic", "Afrin", "Claritin", "Zyrtec", "Allegra", "Xyzal", "Benadryl",
            "Flonase", "Nasacort", "Prilosec", "Nexium", "Pepcid", "Tums", "Pepto", "Imodium", "Gas-X", "Colace",
            "Senokot", "Dulcolax", "MiraLAX", "Metamucil", "Lactaid", "Bonine", "Dramamine", "Pedialyte", "ZzzQuil",
            "Unisom", "Band-Aid", "Neosporin", "Cortizone", "Lotrimin", "Lamisil", "Micatin", "Differin", "Abreva",
            "Vaseline", "Desitin", "Coppertone", "Rogaine", "Nix", "Head & Shoulders", "Systane", "Zaditor", "Visine",
            "Naphcon", "Debrox", "Centrum", "Nature Made", "One A Day", "Flintstones", "Culturelle", "Dex4",
            "Nicorette", "NicoDerm", "Omron", "Clearblue", "First Response", "Voltaren", "Salonpas", "Icy Hot", "Bengay"
        };

        [Fact]
        public void Catalogue_HasNoBrandNames_AndValidInStoreBarcodes()
        {
            var items = UsPharmacySampleData.Items;
            Assert.True(items.Count >= 100);

            foreach (var item in items)
            {
                foreach (var brand in BrandNames)
                    Assert.False(Regex.IsMatch(item.Name, $@"\b{Regex.Escape(brand)}\b", RegexOptions.IgnoreCase),
                        $"'{item.Name}' contains the brand name '{brand}'");
                Assert.True(item.Price > item.Cost, item.Name);
                Assert.InRange(item.Category, 1, UsPharmacySampleData.Categories.Count);
            }

            var quickKeys = items.Where(i => i.QuickKey > 0).Select(i => i.QuickKey).OrderBy(k => k).ToList();
            Assert.Equal(Enumerable.Range(1, quickKeys.Count), quickKeys);

            var upc = UsPharmacySampleData.InStoreUpc(101);
            Assert.Equal(12, upc.Length);
            Assert.StartsWith("4", upc); // number system 4: a store's own labels, never a real product
            var sum = Enumerable.Range(0, 12).Sum(i => (upc[i] - '0') * (i % 2 == 0 ? 3 : 1));
            Assert.Equal(0, sum % 10);
        }

        [Fact]
        public async Task SetupWithSamples_LoadsTheCatalogue_CustomersQuickKeysAndLogins()
        {
            FirstRunSetupResult result;
            using (var db = _dir.NewMigratedContext())
                result = await new FirstRunSetupService(db).CompleteAsync(new FirstRunSetupRequest("owner", "Owner#2026", LoadSampleData: true));

            Assert.Equal(UsPharmacySampleData.Items.Count, result.SampleProducts);
            Assert.Equal(UsPharmacySampleData.Customers.Count, result.SampleCustomers);
            Assert.Equal(UsPharmacySampleData.Logins, result.SampleLogins);

            using (var db = _dir.NewMigratedContext())
            {
                var products = await db.Products.ToListAsync();
                Assert.Equal(UsPharmacySampleData.Items.Count, products.Count);
                Assert.DoesNotContain(products, p => p.ProductName == "Glycerin 25gm" || p.ProductName == "Glue Stick");
                Assert.Equal(products.Count, products.Select(p => p.Barcode).Distinct().Count());
                Assert.Equal(products.Count, products.Select(p => p.ProductId).Distinct().Count());
                Assert.All(products, p => Assert.NotNull(p.ExpiryDate));
                Assert.All(products, p => Assert.False(string.IsNullOrEmpty(p.BatchNo)));
                Assert.Equal(UsPharmacySampleData.Categories.Select(c => c.Name).OrderBy(n => n),
                             (await db.Categories.Select(c => c.Name).ToListAsync()).OrderBy(n => n));

                var tiles = await new FavoriteRepository(db).GetQuickKeyProductsAsync();
                Assert.Equal(10, tiles.Count);
                Assert.Equal("Acetaminophen 500 mg Extra Strength Caplets, 100 ct", tiles[0].ProductName);

                var customers = await db.Customers.Where(c => c.CustomerId != "CASH").OrderBy(c => c.CustomerId).ToListAsync();
                Assert.Equal(new[] { "C0001", "C0002", "C0003" }, customers.Select(c => c.CustomerId));

                var users = new UserRepository(db);
                Assert.NotNull(await users.ValidateUserAsync("owner", "Owner#2026"));
                var manager = await users.ValidateUserAsync("sarah.mitchell", "Mitchell#2026!");
                Assert.NotNull(manager);
                Assert.Equal("Manager", (await db.Roles.SingleAsync(r => r.Id == manager!.RoleId)).Name);
                Assert.NotNull(await users.ValidateUserAsync("james.parker", "Parker#2026!"));
                Assert.Null(await users.ValidateUserAsync("ali", "ali443"));
            }
        }

        [Fact]
        public async Task SetupWithoutSamples_StartsWithAnEmptyCatalogue()
        {
            using (var db = _dir.NewMigratedContext())
            {
                var result = await new FirstRunSetupService(db).CompleteAsync(new FirstRunSetupRequest("owner", "Owner#2026", LoadSampleData: false));
                Assert.Equal(FirstRunSetupResult.NoSamples, result);
            }

            using (var db = _dir.NewMigratedContext())
            {
                Assert.Empty(await db.Products.IgnoreQueryFilters().ToListAsync());
                Assert.Equal(1, await db.Users.CountAsync());
            }
        }

        [Fact]
        public async Task OwnerWhoPicksASampleUsername_KeepsTheirOwnPassword()
        {
            using (var db = _dir.NewMigratedContext())
            {
                var result = await new FirstRunSetupService(db).CompleteAsync(new FirstRunSetupRequest("james.parker", "MyOwn#Pass1", LoadSampleData: true));
                Assert.DoesNotContain(result.SampleLogins, l => l.Username == "james.parker");
            }

            using (var db = _dir.NewMigratedContext())
            {
                var users = new UserRepository(db);
                var owner = await users.ValidateUserAsync("james.parker", "MyOwn#Pass1");
                Assert.NotNull(owner);
                Assert.Equal(1, owner!.RoleId);
                Assert.Null(await users.ValidateUserAsync("james.parker", "Parker#2026!"));
            }
        }

        [Fact]
        public async Task LoadingTwice_AddsNothingTheSecondTime()
        {
            using var db = _dir.NewMigratedContext();
            await UsPharmacySampleData.LoadAsync(db, 1, new DateTime(2026, 10, 1));
            var again = await UsPharmacySampleData.LoadAsync(db, 1, new DateTime(2026, 10, 1));

            Assert.Equal(0, again.SampleProducts);
            Assert.Equal(0, again.SampleCustomers);
            Assert.Empty(again.SampleLogins);
        }
    }
}
