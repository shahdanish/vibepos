// Builds store-assets/seed/demo.sqlite — a SEPARATE, fictional "Demo Mart" database for the
// Microsoft Store screenshots. It never opens the real posapp.db: POSAPP_DATA_DIR is pointed at
// a throw-away folder before AppDbContext is created, the app's own EF migrations build the
// schema there, and the result is copied out.
//
//   dotnet run --project store-assets/seed/DemoSeed [-- <output .sqlite path>]
//
// Everything is deterministic (fixed Random seed) and relative to today's date, so reports show
// the last three weeks whenever it is re-run. Re-run right before capturing screenshots.

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using POSApp.Core.Entities;
using POSApp.Core.Services;
using POSApp.Data;

var seedDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
var output = Path.GetFullPath(args.Length > 0 ? args[0] : Path.Combine(seedDir, "demo.sqlite"));

var work = Path.Combine(Path.GetTempPath(), "swifttill-demo-seed-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(work);
Environment.SetEnvironmentVariable("POSAPP_DATA_DIR", work);
AppPaths.ResetForTests();
if (!AppPaths.DatabasePath.StartsWith(work, StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException("Refusing to run: database path is not the throw-away folder.");

const string DemoPassword = "demo1234";
var rng = new Random(20260930);
var today = DateTime.Today;
var created = today.AddDays(-60);

using (var db = new AppDbContext())
{
    db.Database.Migrate();

    // ── Users: exactly two — Admin and Cashier ───────────────────────────────
    db.Users.RemoveRange(db.Users.Where(u => u.Id == 3 || u.Id == 4));
    foreach (var u in db.Users.Where(u => u.Id == 1 || u.Id == 2))
    {
        u.PasswordHash = PasswordHasher.Hash(DemoPassword);
        u.CreatedDate = created;
        u.LastLoginDate = today.AddDays(-1).AddHours(20);
    }

    // Store first-run wizard already done (it would otherwise delete the users above).
    db.ApplicationSettings.Add(new ApplicationSetting
    {
        Key = "Setup.Completed", Value = "true", Description = "Demo database", CreatedDate = created
    });

    // ── Catalogue: replace the two sample products ───────────────────────────
    db.Products.RemoveRange(db.Products.IgnoreQueryFilters().Where(p => p.Id == 1 || p.Id == 2));
    var catMedicine = db.Categories.Single(c => c.Id == 1);
    catMedicine.Name = "Medicines"; catMedicine.Description = "OTC medicines and first aid";
    var catPersonal = db.Categories.Single(c => c.Id == 2);
    catPersonal.Name = "Personal Care"; catPersonal.Description = "Toiletries and hygiene";
    var catGrocery = new Category { Name = "Grocery", Description = "Staples, dairy and bakery", CreatedDate = created };
    var catDrinks = new Category { Name = "Beverages", Description = "Water, juice and hot drinks", CreatedDate = created };
    var catHome = new Category { Name = "Household", Description = "Cleaning and paper goods", CreatedDate = created };
    var catBaby = new Category { Name = "Baby Care", Description = "Diapers, wipes and lotions", CreatedDate = created };
    db.Categories.AddRange(catGrocery, catDrinks, catHome, catBaby);
    db.SaveChanges();

    // Name, category, cost, retail, stock (null = random healthy stock), rack, popularity weight
    var catalogue = new (string Name, Category Cat, decimal Cost, decimal Price, int? Stock, string Rack, int Weight)[]
    {
        ("Paracetamol 500mg 10s",     catMedicine, 18,   25,   null, "M1", 9),
        ("Ibuprofen 400mg 10s",       catMedicine, 32,   45,   null, "M1", 5),
        ("Cetirizine 10mg 10s",       catMedicine, 28,   40,   null, "M1", 4),
        ("Omeprazole 20mg 14s",       catMedicine, 110,  150,  null, "M2", 3),
        ("Amoxicillin 500mg 10s",     catMedicine, 145,  195,  6,    "M2", 3),
        ("ORS Sachet Orange",         catMedicine, 20,   28,   null, "M2", 4),
        ("Vitamin C 500mg 30s",       catMedicine, 220,  300,  null, "M3", 2),
        ("Cough Syrup 120ml",         catMedicine, 95,   130,  4,    "M3", 4),
        ("Antiseptic Liquid 250ml",   catMedicine, 160,  210,  null, "M3", 2),
        ("Multivitamin 30s",          catMedicine, 380,  520,  null, "M4", 1),
        ("Digital Thermometer",       catMedicine, 290,  450,  null, "M4", 1),
        ("Cotton Bandage 6cm",        catMedicine, 35,   50,   null, "M4", 2),
        ("Herbal Shampoo 200ml",      catPersonal, 260,  340,  null, "P1", 3),
        ("Toothpaste Mint 100g",      catPersonal, 135,  175,  null, "P1", 5),
        ("Bath Soap 115g",            catPersonal, 70,   95,   null, "P1", 6),
        ("Hand Sanitizer 100ml",      catPersonal, 120,  160,  8,    "P2", 2),
        ("Basmati Rice 5kg",          catGrocery,  1650, 1950, null, "G1", 4),
        ("Wheat Flour 10kg",          catGrocery,  1180, 1350, null, "G1", 5),
        ("Sugar 1kg",                 catGrocery,  145,  165,  null, "G1", 8),
        ("Cooking Oil 1L",            catGrocery,  520,  590,  null, "G2", 6),
        ("Red Lentils 1kg",           catGrocery,  290,  340,  null, "G2", 4),
        ("Chickpeas 1kg",             catGrocery,  260,  310,  null, "G2", 3),
        ("Iodised Salt 800g",         catGrocery,  45,   60,   null, "G3", 4),
        ("Red Chilli Powder 200g",    catGrocery,  180,  230,  null, "G3", 3),
        ("Black Tea 190g",            catGrocery,  390,  450,  null, "G3", 6),
        ("Fresh Milk 1L",             catGrocery,  200,  220,  null, "D1", 9),
        ("Eggs Dozen",                catGrocery,  330,  360,  null, "D1", 7),
        ("Bread Large",               catGrocery,  150,  170,  null, "D1", 8),
        ("Mineral Water 1.5L",        catDrinks,   70,   90,   null, "B1", 7),
        ("Orange Juice 1L",           catDrinks,   260,  320,  null, "B1", 3),
        ("Cola 1.5L",                 catDrinks,   150,  180,  null, "B1", 5),
        ("Instant Coffee 50g",        catDrinks,   520,  650,  null, "B2", 2),
        ("Dishwash Liquid 500ml",     catHome,     210,  260,  null, "H1", 4),
        ("Laundry Powder 1kg",        catHome,     360,  430,  null, "H1", 4),
        ("Floor Cleaner 1L",          catHome,     280,  340,  null, "H2", 2),
        ("Tissue Box 100s",           catHome,     140,  180,  null, "H2", 4),
        ("Garbage Bags 30s",          catHome,     190,  240,  3,    "H2", 2),
        ("Baby Diapers Medium 40s",   catBaby,     1850, 2150, null, "K1", 2),
        ("Baby Wipes 80s",            catBaby,     290,  360,  null, "K1", 3),
        ("Baby Lotion 200ml",         catBaby,     380,  460,  null, "K1", 1),
    };

    var products = new List<Product>();
    for (var i = 0; i < catalogue.Length; i++)
    {
        var c = catalogue[i];
        // "200…" is the GS1 in-store / restricted-circulation prefix: never a real brand's code.
        var barcode = Ean13("2001000" + (i + 1).ToString("00000"));
        var isMedicine = c.Cat == catMedicine;
        products.Add(new Product
        {
            ProductId = barcode,
            Barcode = barcode,
            ProductName = c.Name,
            CostPrice = c.Cost,
            UnitPrice = c.Price,
            WholesalePrice = Math.Round(c.Cost + (c.Price - c.Cost) * 0.5m),
            Stock = c.Stock ?? rng.Next(24, 160),
            MinStockThreshold = 10,
            ProfitMarginPercentage = Math.Round((c.Price - c.Cost) / c.Cost * 100, 2),
            Rack = c.Rack,
            BatchNo = isMedicine ? $"B{rng.Next(2400, 2699)}" : null,
            ExpiryDate = isMedicine ? today.AddMonths(rng.Next(8, 30)) : null,
            Category = c.Cat,
            CreatedDate = created
        });
    }
    db.Products.AddRange(products);

    // ── Customers (10 fictional, placeholder phone numbers) ──────────────────
    var names = new[]
    {
        "Ayesha Khan", "Bilal Ahmed", "Fatima Noor", "Hamza Iqbal", "Sana Tariq",
        "Usman Raza", "Zainab Ali", "Imran Siddiqui", "Hira Malik", "Kamran Shah"
    };
    var customers = names.Select((n, i) => new Customer
    {
        CustomerId = $"C{i + 1:000}",
        Name = n,
        Phone = $"0300-00000{i + 1:00}",
        CellNo = $"0300-00000{i + 1:00}",
        Address = $"House {12 + i * 7}, Street {i % 4 + 1}, Block {(char)('A' + i % 3)}",
        CreatedDate = created.AddDays(i)
    }).ToList();
    db.Customers.AddRange(customers);

    // ── Suppliers (3 fictional) ──────────────────────────────────────────────
    db.Suppliers.AddRange(
        new Supplier { SupplierId = "S001", Name = "Northside Pharma Distributors", ContactPerson = "Adnan Qureshi",
                       Phone = "0300-0000201", Email = "orders@northside.example", Address = "Unit 4, Trade Centre",
                       CurrentBalance = 18450, PaymentTerms = "30 days", CreatedDate = created },
        new Supplier { SupplierId = "S002", Name = "Green Valley Wholesale", ContactPerson = "Nadia Farooq",
                       Phone = "0300-0000202", Email = "sales@greenvalley.example", Address = "Grain Market, Shop 22",
                       CurrentBalance = 42600, PaymentTerms = "15 days", CreatedDate = created },
        new Supplier { SupplierId = "S003", Name = "Metro Household Supply", ContactPerson = "Waqar Hussain",
                       Phone = "0300-0000203", Email = "hello@metrohousehold.example", Address = "Industrial Area, Plot 9",
                       CurrentBalance = 0, PaymentTerms = "Cash on delivery", CreatedDate = created });
    db.SaveChanges();

    // ── Sales: last 20 days + this morning ───────────────────────────────────
    var weighted = products.SelectMany((p, i) => Enumerable.Repeat(p, catalogue[i].Weight)).ToList();
    var invoice = 11016;
    for (var d = 20; d >= 0; d--)
    {
        var day = today.AddDays(-d);
        var weekendBoost = day.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday or DayOfWeek.Sunday ? 6 : 0;
        var count = d == 0 ? 9 : 11 + weekendBoost + (20 - d) / 4 + rng.Next(0, 6);
        var times = Enumerable.Range(0, count)
            .Select(_ => day.AddHours(d == 0 ? rng.Next(9, 12) : rng.Next(9, 22)).AddMinutes(rng.Next(60)).AddSeconds(rng.Next(60)))
            .OrderBy(t => t).ToList();

        foreach (var when in times)
        {
            var sale = new Sale
            {
                InvoiceNumber = (invoice++).ToString(),
                SaleDate = when,
                CreatedDate = when,
                SaleType = "Sale",
                AutoPrinted = true
            };

            foreach (var p in weighted.OrderBy(_ => rng.Next()).DistinctBy(p => p.Id).Take(rng.Next(1, 7)))
            {
                var qty = p.UnitPrice >= 1000 ? 1 : rng.Next(10) < 7 ? 1 : rng.Next(2, 4);
                sale.SaleItems.Add(new SaleItem
                {
                    ProductId = p.ProductId, ProductName = p.ProductName, Quantity = qty,
                    CostPrice = p.CostPrice, UnitPrice = p.UnitPrice, DiscountType = "%",
                    Total = p.UnitPrice * qty
                });
            }

            var subtotal = sale.SaleItems.Sum(i => i.Total);
            sale.DiscountOnBill = subtotal > 2500 && rng.Next(4) == 0 ? Math.Round(subtotal * 0.03m / 10) * 10 : 0;
            sale.TotalBill = subtotal - sale.DiscountOnBill;

            if (rng.Next(100) < 12)
            {
                var customer = customers[rng.Next(customers.Count)];
                sale.PaymentType = "Credit";
                sale.CustomerId = customer.Id;
                sale.CustomerName = customer.Name;
                sale.MobileNumber = customer.CellNo;
                sale.PreBalance = customer.CurrentBalance;
                sale.ReceiveCash = 0;
                sale.Balance = -sale.TotalBill;
                customer.CurrentBalance += sale.TotalBill;
                customer.TotalPurchases += sale.TotalBill;
                customer.LastPurchaseDate = when;
            }
            else
            {
                sale.PaymentType = rng.Next(100) < 15 ? "Credit Card" : "Cash";
                sale.CustomerName = "Cash";
                sale.ReceiveCash = sale.PaymentType == "Cash"
                    ? Math.Ceiling(sale.TotalBill / (sale.TotalBill > 1000 ? 500 : 100)) * (sale.TotalBill > 1000 ? 500 : 100)
                    : sale.TotalBill;
                sale.Balance = sale.ReceiveCash - sale.TotalBill;
            }

            db.Sales.Add(sale);
        }
    }

    // Khata (credit) repayments — two or three per credit customer across the period, so the
    // ledger's payment history has both recent and older entries.
    foreach (var customer in customers.Where(c => c.CurrentBalance > 1000))
    {
        var n = rng.Next(2, 4);
        for (var k = 0; k < n; k++)
        {
            var paid = Math.Max(500, Math.Round(customer.CurrentBalance * 0.2m / 100) * 100);
            customer.CurrentBalance -= paid;
            db.CustomerPayments.Add(new CustomerPayment
            {
                Customer = customer, AmountPaid = paid,
                PaymentDate = today.AddDays(-(k * 6 + rng.Next(0, 5))).AddHours(rng.Next(10, 20)),
                InvoiceNumber = k == 0 ? null : (11016 + rng.Next(0, 300)).ToString(),
                Note = k % 2 == 0 ? "Cash payment" : "Paid by bank transfer"
            });
        }
    }

    db.SaveChanges();
    db.Database.ExecuteSqlRaw("PRAGMA wal_checkpoint(TRUNCATE);");

    Console.WriteLine($"Products {db.Products.Count()}, customers {db.Customers.Count() - 1} (+Cash), " +
                      $"suppliers {db.Suppliers.Count()}, users {db.Users.Count()}, sales {db.Sales.Count()}, " +
                      $"sale items {db.SaleItems.Count()}, revenue Rs. {db.Sales.AsEnumerable().Sum(s => s.TotalBill):N0}");
}

SqliteConnection.ClearAllPools();
Directory.CreateDirectory(Path.GetDirectoryName(output)!);
File.Copy(AppPaths.DatabasePath, output, overwrite: true);
Directory.Delete(work, recursive: true);
Console.WriteLine($"Demo database written to {output}");
Console.WriteLine($"Logins: admin / {DemoPassword}   cashier / {DemoPassword}");

static string Ean13(string twelve)
{
    var sum = twelve.Select((ch, i) => (ch - '0') * (i % 2 == 0 ? 1 : 3)).Sum();
    return twelve + ((10 - sum % 10) % 10);
}
