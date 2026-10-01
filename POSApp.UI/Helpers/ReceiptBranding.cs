using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace POSApp.UI.Helpers
{
    /// <summary>
    /// Per-client branding printed at the top and bottom of every receipt, invoice and
    /// report. Each installation edits these values from Admin → Receipt Settings, so one
    /// build of the software can be shipped to any shop without recompiling.
    /// </summary>
    public sealed class ReceiptBrandingSettings
    {
        /// <summary>Shop name — the large bold line at the very top of the printout.</summary>
        public string StoreName { get; set; } = "Your Store Name";

        /// <summary>Street / city line printed under the shop name.</summary>
        public string StoreAddress { get; set; } = "Your Store Address";

        /// <summary>Contact number printed under the address.</summary>
        public string StorePhone { get; set; } = string.Empty;

        /// <summary>
        /// Optional extra header line (tax / registration number, tagline, second phone). Left blank
        /// by default and skipped entirely when empty.
        /// </summary>
        public string HeaderNote { get; set; } = string.Empty;

        /// <summary>Bold closing line at the bottom of the printout.</summary>
        public string FooterMessage { get; set; } = "Thank You For Your Business!";

        /// <summary>Small grey line under the closing message. Skipped when empty.</summary>
        public string FooterNote { get; set; } = "Please keep this invoice for your records.";
    }

    /// <summary>
    /// Loads/saves <see cref="ReceiptBrandingSettings"/> and builds the shared header and
    /// footer paragraphs used by every print document in the app.
    ///
    /// The file lives under %ProgramData% (machine-wide, not per Windows user) so every
    /// cashier on the till prints the same branding and an app upgrade does not reset it.
    /// </summary>
    public static class ReceiptBranding
    {
        private static readonly string SettingsFilePath =
            POSApp.Core.Services.SharedSettingsFiles.PathOf(POSApp.Core.Services.SharedSettingsFiles.ReceiptBranding);

        private static ReceiptBrandingSettings? _cached;

        /// <summary>Raised after the branding was written to disk.</summary>
        public static event Action? Saved;

        /// <summary>Forgets the cached branding so the next read comes from disk.</summary>
        public static void Reload() => _cached = null;

        /// <summary>Current branding, loaded from disk on first use and cached thereafter.</summary>
        public static ReceiptBrandingSettings Current
        {
            get
            {
                if (_cached != null)
                    return _cached;

                try
                {
                    _cached = File.Exists(SettingsFilePath)
                        ? JsonSerializer.Deserialize<ReceiptBrandingSettings>(File.ReadAllText(SettingsFilePath))
                          ?? new ReceiptBrandingSettings()
                        : new ReceiptBrandingSettings();
                }
                catch
                {
                    // Missing or hand-edited file: fall back to defaults rather than
                    // blocking printing.
                    _cached = new ReceiptBrandingSettings();
                }

                return _cached;
            }
        }

        /// <summary>Persists the branding and refreshes the cache. Returns false if the file could not be written.</summary>
        public static bool Save(ReceiptBrandingSettings settings)
        {
            try
            {
                var dir = Path.GetDirectoryName(SettingsFilePath)!;
                Directory.CreateDirectory(dir);

                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsFilePath, json);

                _cached = settings;
            }
            catch
            {
                return false;
            }

            try { Saved?.Invoke(); }
            catch { /* a listener failing must not undo a successful save */ }
            return true;
        }

        /// <summary>
        /// Builds the centred store header block. Font sizes vary between documents
        /// (invoices print larger than reports), so callers pass their own.
        /// </summary>
        public static Paragraph BuildHeader(double nameFontSize = 24, double lineFontSize = 14)
        {
            var b = Current;
            var header = new Paragraph
            {
                Margin = new Thickness(0, 0, 0, 2),
                TextAlignment = TextAlignment.Center
            };

            var first = true;

            void AddLine(Inline inline)
            {
                if (!first) header.Inlines.Add(new LineBreak());
                header.Inlines.Add(inline);
                first = false;
            }

            if (!string.IsNullOrWhiteSpace(b.StoreName))
                AddLine(new Bold(new Run(b.StoreName)) { FontSize = nameFontSize });
            if (!string.IsNullOrWhiteSpace(b.StoreAddress))
                AddLine(new Run(b.StoreAddress) { FontSize = lineFontSize });
            if (!string.IsNullOrWhiteSpace(b.StorePhone))
                AddLine(new Run(b.StorePhone) { FontSize = lineFontSize });
            if (!string.IsNullOrWhiteSpace(b.HeaderNote))
                AddLine(new Run(b.HeaderNote) { FontSize = lineFontSize });

            return header;
        }

        /// <summary>
        /// Builds the centred closing block printed at the bottom of the document.
        /// Reports pass <paramref name="includeNote"/> = false since the "keep this
        /// invoice" note only makes sense on a customer's receipt.
        /// </summary>
        public static Paragraph BuildFooter(double messageFontSize = 14, double noteFontSize = 10, bool includeNote = true)
        {
            var b = Current;
            var footer = new Paragraph
            {
                Margin = new Thickness(0, 5, 0, 0),
                TextAlignment = TextAlignment.Center
            };

            if (!string.IsNullOrWhiteSpace(b.FooterMessage))
                footer.Inlines.Add(new Bold(new Run(b.FooterMessage)) { FontSize = messageFontSize });

            if (includeNote && !string.IsNullOrWhiteSpace(b.FooterNote))
            {
                if (footer.Inlines.Count > 0) footer.Inlines.Add(new LineBreak());
                footer.Inlines.Add(new Run(b.FooterNote) { FontSize = noteFontSize, Foreground = Brushes.Gray });
            }

            return footer;
        }
    }
}
