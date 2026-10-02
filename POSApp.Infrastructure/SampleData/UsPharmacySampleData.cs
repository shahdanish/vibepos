using Microsoft.EntityFrameworkCore;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.Core.Services;
using POSApp.Data;
using POSApp.Infrastructure.Repositories;

namespace POSApp.Infrastructure.SampleData
{
    /// <summary>
    /// Optional sample data for a new US pharmacy front store, offered once in first-run setup
    /// so a shop can try sales straight away.
    ///
    /// The catalogue is real over-the-counter medicines sold in the US, under their generic
    /// names only: no brand names and no NDCs. Barcodes are in-store UPC-A codes (number
    /// system 4, reserved for a store's own labels), so they can never match a real product.
    /// Prices are typical store-brand shelf prices. People and phone numbers are fictional
    /// (555-01xx is reserved for fiction).
    /// </summary>
    public static class UsPharmacySampleData
    {
        /// <param name="QuickKey">Position on the sale screen's quick keys (1 = F1), or 0.</param>
        /// <param name="ExpiresInDays">Fixed expiry for a few items, so "expiring soon" has examples.</param>
        /// <param name="MinimumAge">Photo ID needed at the register (18 for nicotine replacement and DXM).</param>
        /// <param name="PseBaseMg">Pseudoephedrine base per package; above 0 makes it a logbook item.</param>
        public sealed record Item(
            string Name, int Category, decimal Cost, decimal Price, int Stock, int ReorderAt,
            int QuickKey = 0, int? ExpiresInDays = null, int MinimumAge = 0, decimal PseBaseMg = 0);

        public sealed record CategoryInfo(string Name, string Description);

        public sealed record CustomerInfo(string Name, string Phone, decimal OpeningBalance);

        /// <summary>
        /// Sample categories whose items are non-prescription drugs, so they get the shop's OTC tax
        /// category (many states tax them differently from general merchandise).
        /// </summary>
        private static readonly HashSet<int> OtcDrugCategories = new() { 1, 2, 3, 4, 5, 7, 8, 11 };

        /// <summary>
        /// Sample categories flagged FSA/HSA eligible (medicines, first aid, diabetes and home
        /// health; not vitamins). A real shop checks each item against its card processor's list.
        /// </summary>
        private static readonly HashSet<int> FsaEligibleCategories = new() { 1, 2, 3, 4, 5, 6, 7, 8, 10, 11, 12 };

        /// <summary>Category numbers used by <see cref="Items"/> (1-based; also the aisle number).</summary>
        public static IReadOnlyList<CategoryInfo> Categories { get; } = new CategoryInfo[]
        {
            new("Pain & Fever", "Pain relievers and fever reducers"),
            new("Cold, Cough & Flu", "Cough, cold and sore throat relief"),
            new("Allergy & Sinus", "Antihistamines and nasal sprays"),
            new("Digestive Health", "Heartburn, antacids, laxatives and motion sickness"),
            new("Sleep Aids", "Nighttime sleep aids"),
            new("First Aid", "Bandages, antiseptics and wound care"),
            new("Skin Care", "Medicated creams, ointments and sun care"),
            new("Eye & Ear Care", "Eye drops, contact lens care and ear drops"),
            new("Vitamins & Supplements", "Vitamins, minerals and supplements"),
            new("Diabetes Care", "Glucose testing and supplies"),
            new("Smoking Cessation", "Nicotine replacement (customers 18 and over)"),
            new("Home Health", "Thermometers, monitors and everyday health aids"),
        };

        public static IReadOnlyList<Item> Items { get; } = new Item[]
        {
            // 1 Pain & Fever
            new("Acetaminophen 325 mg Tablets, 100 ct", 1, 3.40m, 5.99m, 48, 12),
            new("Acetaminophen 500 mg Extra Strength Caplets, 100 ct", 1, 4.20m, 7.49m, 60, 12, QuickKey: 1),
            new("Acetaminophen 500 mg Extra Strength Caplets, 24 ct", 1, 1.90m, 3.79m, 36, 10),
            new("Acetaminophen 650 mg Extended-Release Caplets, 100 ct", 1, 6.10m, 10.99m, 24, 6),
            new("Ibuprofen 200 mg Tablets, 100 ct", 1, 3.80m, 6.99m, 55, 12, QuickKey: 2),
            new("Ibuprofen 200 mg Liquid-Filled Capsules, 40 ct", 1, 4.10m, 7.49m, 30, 8),
            new("Naproxen Sodium 220 mg Tablets, 50 ct", 1, 4.30m, 7.99m, 28, 8, QuickKey: 3),
            new("Aspirin 81 mg Low Dose Enteric-Coated Tablets, 120 ct", 1, 2.60m, 4.99m, 40, 10),
            new("Aspirin 325 mg Tablets, 100 ct", 1, 2.30m, 4.49m, 20, 6),
            new("Children's Acetaminophen Oral Suspension 160 mg/5 mL, 4 fl oz", 1, 3.90m, 6.99m, 18, 6, ExpiresInDays: 40),
            new("Children's Ibuprofen Oral Suspension 100 mg/5 mL, 4 fl oz", 1, 4.10m, 7.49m, 16, 6),
            new("Infants' Acetaminophen Oral Suspension 160 mg/5 mL, 1 fl oz", 1, 4.90m, 8.99m, 4, 6),
            new("Diclofenac Sodium Topical Gel 1%, 100 g", 1, 6.80m, 12.99m, 14, 4),
            new("Lidocaine 4% Pain Relief Patches, 5 ct", 1, 5.20m, 9.99m, 12, 4),
            new("Menthol 4% Pain Relieving Gel, 3 oz", 1, 3.60m, 6.99m, 10, 4),

            // 2 Cold, Cough & Flu
            new("Guaifenesin 600 mg Extended-Release Tablets, 20 ct", 2, 6.30m, 11.99m, 26, 8, QuickKey: 4),
            new("Dextromethorphan HBr & Guaifenesin Cough Syrup, 4 fl oz", 2, 4.20m, 7.99m, 22, 6, MinimumAge: 18),
            new("Non-Drowsy Cough & Fever Liquid (Acetaminophen, Dextromethorphan), 8 fl oz", 2, 4.90m, 8.99m, 15, 5, MinimumAge: 18),
            new("Nighttime Cold & Flu Liquid (Acetaminophen, Dextromethorphan, Doxylamine), 8 fl oz", 2, 4.90m, 8.99m, 18, 5, MinimumAge: 18),
            new("Menthol Cough Drops, 30 ct", 2, 1.40m, 2.79m, 50, 15, QuickKey: 5),
            new("Benzocaine & Menthol Sore Throat Lozenges, 18 ct", 2, 2.70m, 4.99m, 20, 6),
            new("Phenol 1.4% Sore Throat Spray, 6 fl oz", 2, 3.30m, 5.99m, 12, 4),
            new("Saline 0.65% Nasal Spray, 1.5 fl oz", 2, 1.60m, 3.29m, 30, 8),
            new("Oxymetazoline HCl 0.05% Nasal Spray, 0.5 fl oz", 2, 2.90m, 5.49m, 20, 6),
            new("Children's Cough & Chest Congestion DM Liquid, 4 fl oz", 2, 4.30m, 7.99m, 10, 4, MinimumAge: 18),
            new("Camphor, Eucalyptus & Menthol Chest Rub, 3.53 oz", 2, 3.40m, 6.49m, 14, 4),

            // 3 Allergy & Sinus
            new("Loratadine 10 mg Tablets, 30 ct", 3, 4.80m, 9.49m, 34, 8, QuickKey: 6),
            new("Cetirizine HCl 10 mg Tablets, 30 ct", 3, 5.30m, 10.49m, 32, 8, QuickKey: 7),
            new("Fexofenadine HCl 180 mg Tablets, 30 ct", 3, 7.60m, 14.99m, 20, 6),
            new("Levocetirizine Dihydrochloride 5 mg Tablets, 35 ct", 3, 7.40m, 13.99m, 12, 4),
            new("Diphenhydramine HCl 25 mg Allergy Tablets, 100 ct", 3, 2.40m, 4.79m, 30, 8),
            new("Fluticasone Propionate 50 mcg Nasal Spray, 120 sprays", 3, 7.90m, 14.99m, 18, 6),
            new("Triamcinolone Acetonide 55 mcg Nasal Spray, 120 sprays", 3, 7.70m, 14.49m, 0, 4),
            new("Children's Loratadine Oral Solution 5 mg/5 mL, 4 fl oz", 3, 4.60m, 8.99m, 10, 4),
            // Kept behind the counter; each sale goes in the PSE logbook (24 × 30 mg HCl ≈ 590 mg base).
            new("Pseudoephedrine HCl 30 mg Tablets, 24 ct", 3, 3.40m, 6.49m, 12, 4, PseBaseMg: 590m),
            new("Pseudoephedrine HCl 120 mg Extended-Release Tablets, 10 ct", 3, 5.10m, 9.99m, 8, 3, PseBaseMg: 983m),

            // 4 Digestive Health
            new("Omeprazole 20 mg Delayed-Release Tablets, 42 ct", 4, 10.40m, 19.99m, 18, 6),
            new("Omeprazole 20 mg Delayed-Release Tablets, 14 ct", 4, 4.40m, 8.49m, 22, 6),
            new("Esomeprazole Magnesium 20 mg Delayed-Release Capsules, 14 ct", 4, 6.10m, 11.49m, 14, 4),
            new("Famotidine 20 mg Tablets, 50 ct", 4, 6.20m, 11.99m, 20, 6),
            new("Calcium Carbonate 750 mg Antacid Chewable Tablets, 96 ct", 4, 3.30m, 6.29m, 30, 8, QuickKey: 8),
            new("Bismuth Subsalicylate 262 mg/15 mL Liquid, 8 fl oz", 4, 3.10m, 5.99m, 18, 6),
            new("Loperamide HCl 2 mg Caplets, 24 ct", 4, 3.10m, 5.99m, 24, 6),
            new("Simethicone 125 mg Softgels, 50 ct", 4, 4.20m, 7.99m, 16, 4),
            new("Docusate Sodium 100 mg Stool Softener Softgels, 100 ct", 4, 3.70m, 6.99m, 20, 6),
            new("Sennosides 8.6 mg Laxative Tablets, 100 ct", 4, 2.80m, 5.29m, 20, 6),
            new("Bisacodyl 5 mg Laxative Tablets, 25 ct", 4, 1.90m, 3.79m, 18, 6),
            new("Polyethylene Glycol 3350 Laxative Powder, 8.3 oz", 4, 6.70m, 12.99m, 16, 4),
            new("Psyllium Husk Fiber Powder, 15 oz", 4, 5.60m, 10.99m, 12, 4),
            new("Lactase Enzyme Caplets, 32 ct", 4, 5.20m, 9.99m, 3, 4),
            new("Meclizine HCl 25 mg Motion Sickness Tablets, 16 ct", 4, 3.60m, 6.99m, 10, 4),
            new("Dimenhydrinate 50 mg Motion Sickness Tablets, 12 ct", 4, 2.40m, 4.49m, 12, 4),
            new("Pediatric Electrolyte Solution, 33.8 fl oz", 4, 3.20m, 5.99m, 24, 6),

            // 5 Sleep Aids
            new("Diphenhydramine HCl 25 mg Nighttime Sleep Aid Caplets, 32 ct", 5, 2.60m, 4.99m, 18, 6),
            new("Doxylamine Succinate 25 mg Sleep Aid Tablets, 32 ct", 5, 3.70m, 6.99m, 14, 4),

            // 6 First Aid
            new("Adhesive Bandages, Assorted Sizes, 100 ct", 6, 3.10m, 5.99m, 30, 8, QuickKey: 9),
            new("Hydrogen Peroxide 3% Topical Solution, 16 fl oz", 6, 0.55m, 1.19m, 40, 10),
            new("Isopropyl Rubbing Alcohol 70%, 16 fl oz", 6, 0.95m, 1.99m, 40, 10),
            new("Triple Antibiotic Ointment (Bacitracin, Neomycin, Polymyxin B), 1 oz", 6, 3.10m, 5.99m, 22, 6),
            new("Bacitracin Zinc Ointment, 1 oz", 6, 2.40m, 4.49m, 14, 4),
            new("Hydrocortisone 1% Anti-Itch Cream, 1 oz", 6, 1.90m, 3.79m, 26, 8),
            new("Sterile Gauze Pads 4 in x 4 in, 25 ct", 6, 2.60m, 4.99m, 16, 4),
            new("Elastic Bandage Wrap, 3 in", 6, 2.70m, 4.99m, 12, 4),
            new("Paper First Aid Tape, 1 in x 10 yd", 6, 1.40m, 2.79m, 18, 6),
            new("Antiseptic Wipes (Benzalkonium Chloride), 40 ct", 6, 2.20m, 3.99m, 16, 4),
            new("Instant Cold Pack", 6, 0.95m, 1.99m, 20, 6),
            new("Calamine Lotion, 6 fl oz", 6, 1.60m, 3.29m, 12, 4),
            new("Aloe Vera Burn Relief Gel with Lidocaine, 16 oz", 6, 3.10m, 5.99m, 10, 4),

            // 7 Skin Care
            new("Clotrimazole 1% Antifungal Cream, 1 oz", 7, 3.10m, 5.99m, 16, 4),
            new("Terbinafine HCl 1% Antifungal Cream, 1 oz", 7, 5.60m, 10.49m, 10, 4),
            new("Miconazole Nitrate 2% Antifungal Cream, 1 oz", 7, 2.90m, 5.49m, 10, 4),
            new("Benzoyl Peroxide 10% Acne Treatment Gel, 1 oz", 7, 3.30m, 6.29m, 12, 4),
            new("Adapalene 0.1% Acne Treatment Gel, 15 g", 7, 6.90m, 12.99m, 8, 4),
            new("Docosanol 10% Cold Sore Cream, 2 g", 7, 8.10m, 14.99m, 6, 3, ExpiresInDays: 75),
            new("White Petrolatum Skin Protectant, 13 oz", 7, 2.30m, 4.49m, 14, 4),
            new("Zinc Oxide 40% Diaper Rash Paste, 4 oz", 7, 4.10m, 7.49m, 12, 4),
            new("Broad Spectrum SPF 50 Sunscreen Lotion, 8 fl oz", 7, 4.80m, 8.99m, 16, 4),
            new("Minoxidil 5% Topical Solution for Men, 2 fl oz", 7, 9.80m, 18.99m, 8, 3),
            new("Permethrin 1% Lice Treatment Creme Rinse, 2 fl oz", 7, 6.40m, 11.99m, 6, 3),
            new("Pyrithione Zinc 1% Dandruff Shampoo, 13.5 fl oz", 7, 2.90m, 5.49m, 12, 4),

            // 8 Eye & Ear Care
            new("Lubricant Eye Drops, 0.5 fl oz", 8, 4.20m, 7.99m, 18, 6),
            new("Ketotifen 0.025% Antihistamine Eye Drops, 5 mL", 8, 5.90m, 10.99m, 10, 4),
            new("Naphazoline & Pheniramine Redness Relief Eye Drops, 0.5 fl oz", 8, 3.70m, 6.99m, 10, 4),
            new("Saline Solution for Contact Lenses, 12 fl oz", 8, 2.60m, 4.99m, 12, 4),
            new("Carbamide Peroxide 6.5% Earwax Removal Drops, 0.5 fl oz", 8, 3.30m, 6.29m, 10, 4),
            new("Sterile Eyewash Solution, 4 fl oz", 8, 2.40m, 4.49m, 8, 3),

            // 9 Vitamins & Supplements
            new("Adult Multivitamin Tablets, 100 ct", 9, 4.80m, 9.49m, 20, 6),
            new("Vitamin D3 25 mcg (1,000 IU) Softgels, 100 ct", 9, 3.10m, 5.99m, 24, 6, QuickKey: 10),
            new("Vitamin C 500 mg Tablets, 100 ct", 9, 2.80m, 5.49m, 24, 6, ExpiresInDays: 25),
            new("Vitamin B12 1,000 mcg Tablets, 100 ct", 9, 4.10m, 7.99m, 16, 4),
            new("Calcium 600 mg + Vitamin D3 Tablets, 120 ct", 9, 5.20m, 9.99m, 14, 4),
            new("Fish Oil 1,000 mg Softgels, 100 ct", 9, 5.10m, 9.79m, 16, 4),
            new("Magnesium Oxide 400 mg Tablets, 100 ct", 9, 3.30m, 6.29m, 12, 4),
            new("Melatonin 3 mg Tablets, 120 ct", 9, 3.60m, 6.99m, 20, 6),
            new("Ferrous Sulfate 325 mg Iron Tablets, 100 ct", 9, 2.40m, 4.79m, 12, 4),
            new("Folic Acid 400 mcg Tablets, 250 ct", 9, 2.90m, 5.49m, 10, 4),
            new("Prenatal Multivitamin with Folic Acid Tablets, 100 ct", 9, 5.10m, 9.99m, 8, 3),
            new("Children's Multivitamin Gummies, 70 ct", 9, 4.60m, 8.99m, 12, 4),
            new("Zinc 50 mg Tablets, 100 ct", 9, 2.90m, 5.49m, 10, 4),
            new("Probiotic 10 Billion CFU Capsules, 30 ct", 9, 8.20m, 15.99m, 8, 3),

            // 10 Diabetes Care
            new("Glucose Tablets 4 g, Orange, 50 ct", 10, 3.10m, 5.99m, 14, 4),
            new("Blood Glucose Test Strips, 50 ct", 10, 10.50m, 19.99m, 12, 4),
            new("Blood Glucose Meter Kit", 10, 9.20m, 17.99m, 5, 2),
            new("Sterile Lancets 30G, 100 ct", 10, 2.90m, 5.49m, 14, 4),
            new("Alcohol Prep Pads, 100 ct", 10, 1.30m, 2.69m, 30, 8),

            // 11 Smoking Cessation
            new("Nicotine Polacrilex Gum 2 mg, Mint, 20 ct", 11, 6.20m, 11.99m, 8, 3, MinimumAge: 18),
            new("Nicotine Polacrilex Gum 4 mg, Mint, 100 ct", 11, 21.50m, 39.99m, 6, 2, MinimumAge: 18),
            new("Nicotine Transdermal Patch 21 mg/24 hr, Step 1, 14 ct", 11, 21.00m, 38.99m, 5, 2, MinimumAge: 18),
            new("Nicotine Polacrilex Lozenge 2 mg, 72 ct", 11, 18.90m, 34.99m, 4, 2, MinimumAge: 18),

            // 12 Home Health
            new("Digital Oral Thermometer", 12, 4.90m, 8.99m, 10, 3),
            new("Upper Arm Blood Pressure Monitor", 12, 21.00m, 39.99m, 4, 2),
            new("7-Day Pill Organizer, AM/PM", 12, 2.40m, 4.99m, 12, 4),
            new("Early Result Pregnancy Test, 2 ct", 12, 4.40m, 8.49m, 10, 4),
            new("Disposable Face Masks, 50 ct", 12, 3.60m, 6.99m, 14, 4),
            new("Nitrile Exam Gloves, 100 ct", 12, 5.10m, 9.99m, 8, 3),
        };

        /// <summary>Charge-account customers. Fictional names; 555-01xx numbers are reserved for fiction.</summary>
        public static IReadOnlyList<CustomerInfo> Customers { get; } = new CustomerInfo[]
        {
            new("Linda Thompson", "5555550142", 0m),
            new("Robert Garcia", "5555550178", 24.50m),
            new("Karen Wilson", "5555550193", 0m),
        };

        /// <summary>
        /// Staff logins for trying roles out. They are printed on the setup screen and in the
        /// docs, so a shop must delete them (Admin → Users) before going live.
        /// </summary>
        public static IReadOnlyList<SampleLogin> Logins { get; } = new SampleLogin[]
        {
            new("sarah.mitchell", "Mitchell#2026!", "Manager"),
            new("james.parker", "Parker#2026!", "Cashier"),
        };

        /// <summary>
        /// The in-store UPC-A for item <paramref name="itemNumber"/>: number system 4 (store's own
        /// labels), a fixed 5-digit block, the item number and the check digit.
        /// </summary>
        public static string InStoreUpc(int itemNumber)
        {
            var body = $"4{20260:D5}{itemNumber:D5}";
            var sum = 0;
            for (var i = 0; i < 11; i++)
                sum += (body[i] - '0') * (i % 2 == 0 ? 3 : 1);
            return body + (10 - sum % 10) % 10;
        }

        /// <summary>
        /// Adds the catalogue, customers, quick keys and staff logins. Products whose barcode is
        /// already present and logins whose username is taken are skipped, so running it twice
        /// does not duplicate anything.
        /// </summary>
        public static async Task<FirstRunSetupResult> LoadAsync(
            AppDbContext db, int addedByUserId, DateTime today, CancellationToken ct = default)
        {
            // ── Categories (reuse one with the same name) ───────────────────────
            var categoryIds = new int[Categories.Count + 1];
            for (var c = 0; c < Categories.Count; c++)
            {
                var info = Categories[c];
                var category = await db.Categories.FirstOrDefaultAsync(x => x.Name == info.Name, ct);
                if (category == null)
                {
                    category = new Category { Name = info.Name, Description = info.Description, CreatedDate = today };
                    db.Categories.Add(category);
                    await db.SaveChangesAsync(ct);
                }
                categoryIds[c + 1] = category.Id;
            }

            // ── Products ────────────────────────────────────────────────────────
            var otcTaxCategory = await db.TaxCategories
                .Where(c => c.Name == TaxRepository.OtcCategoryName)
                .Select(c => (int?)c.Id)
                .FirstOrDefaultAsync(ct);
            var barcodes = (await db.Products.IgnoreQueryFilters().Select(p => p.Barcode).ToListAsync(ct)).ToHashSet();
            var productIds = (await db.Products.IgnoreQueryFilters().Select(p => p.ProductId).ToListAsync(ct)).ToHashSet();
            var sequence = new Dictionary<int, int>();
            var added = new List<(Product Product, int QuickKey)>();

            for (var i = 0; i < Items.Count; i++)
            {
                var item = Items[i];
                var seq = sequence[item.Category] = sequence.GetValueOrDefault(item.Category) + 1;
                var barcode = InStoreUpc(item.Category * 100 + seq);
                if (!barcodes.Add(barcode)) continue;

                var expiry = today.Date.AddDays(item.ExpiresInDays ?? 240 + i * 53 % 600);
                var product = new Product
                {
                    ProductId = NextProductId(categoryIds[item.Category], productIds),
                    Barcode = barcode,
                    ProductName = item.Name,
                    CostPrice = item.Cost,
                    UnitPrice = item.Price,
                    WholesalePrice = Math.Round(item.Price * 0.85m, 2),
                    Stock = item.Stock,
                    MinStockThreshold = item.ReorderAt,
                    Rack = $"{item.Category}{(char)('A' + (seq - 1) % 6)}",
                    BatchNo = $"L{expiry:yyMM}{(char)('A' + i % 20)}{(i * 37 % 900) + 100}",
                    ExpiryDate = expiry,
                    CategoryId = categoryIds[item.Category],
                    TaxCategoryId = OtcDrugCategories.Contains(item.Category) ? otcTaxCategory : null,
                    MinimumAge = item.MinimumAge,
                    IsPse = item.PseBaseMg > 0,
                    PseBaseMgPerPack = item.PseBaseMg,
                    IsFsaEligible = FsaEligibleCategories.Contains(item.Category),
                    CreatedDate = today
                };
                db.Products.Add(product);
                added.Add((product, item.QuickKey));
            }
            await db.SaveChangesAsync(ct);

            // ── Quick keys (shop-wide tiles on the sale screens) ────────────────
            var starred = (await db.UserFavorites.Select(f => f.ProductId).ToListAsync(ct)).ToHashSet();
            foreach (var (product, position) in added.Where(a => a.QuickKey > 0).OrderBy(a => a.QuickKey))
            {
                if (!starred.Add(product.Id)) continue;
                db.UserFavorites.Add(new UserFavorite
                {
                    UserId = addedByUserId,
                    ProductId = product.Id,
                    ProductName = product.ProductName,
                    SortOrder = position,
                    AddedDate = today
                });
            }

            // ── Charge-account customers ────────────────────────────────────────
            var customerNumbers = await db.Customers.Select(c => c.CustomerId).ToListAsync(ct);
            var nextCustomer = customerNumbers
                .Select(id => int.TryParse(id.TrimStart('C'), out var n) ? n : 0)
                .DefaultIfEmpty(0).Max();
            var customersAdded = 0;
            foreach (var info in Customers)
            {
                if (await db.Customers.AnyAsync(c => c.Name == info.Name, ct)) continue;
                db.Customers.Add(new Customer
                {
                    CustomerId = $"C{++nextCustomer:D4}",
                    Name = info.Name,
                    Phone = info.Phone,
                    CellNo = info.Phone,
                    PreBalance = info.OpeningBalance,
                    CurrentBalance = info.OpeningBalance,
                    CreatedDate = today
                });
                customersAdded++;
            }

            // ── Staff logins ────────────────────────────────────────────────────
            var logins = new List<SampleLogin>();
            foreach (var login in Logins)
            {
                if (await db.Users.AnyAsync(u => u.Username == login.Username, ct)) continue;
                var roleId = await db.Roles.Where(r => r.Name == login.Role).Select(r => (int?)r.Id).FirstOrDefaultAsync(ct);
                if (roleId == null) continue;
                db.Users.Add(new User
                {
                    Username = login.Username,
                    PasswordHash = PasswordHasher.Hash(login.Password),
                    RoleId = roleId.Value,
                    IsActive = true,
                    CreatedDate = today
                });
                logins.Add(login);
            }

            await db.SaveChangesAsync(ct);
            return new FirstRunSetupResult(added.Count, customersAdded, logins);
        }

        /// <summary>
        /// The app's own product-code scheme (category number, then a running number, five
        /// digits in all), skipping any code already in use.
        /// </summary>
        private static string NextProductId(int categoryId, HashSet<string> used)
        {
            var prefix = categoryId.ToString();
            for (var n = 1; ; n++)
            {
                var id = prefix + n.ToString().PadLeft(Math.Max(1, 5 - prefix.Length), '0');
                if (used.Add(id)) return id;
            }
        }
    }
}
