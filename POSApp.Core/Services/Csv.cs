using System.Globalization;
using System.Text;

namespace POSApp.Core.Services
{
    /// <summary>
    /// Writes spreadsheet-ready CSV (UTF-8 with BOM so Excel shows symbols correctly). Free text
    /// that starts like a formula (= + - @) is prefixed with an apostrophe so an exported
    /// customer or product name can never run as a formula when the file is opened.
    /// </summary>
    public static class Csv
    {
        public static string Text(string? value)
        {
            var s = value ?? string.Empty;
            return s.Length > 0 && "=+-@\t\r".IndexOf(s[0]) >= 0 ? "'" + s : s;
        }

        public static string Money(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);

        public static string Number(decimal value) => value.ToString("0.###", CultureInfo.InvariantCulture);

        public static string DateTime(System.DateTime value) => value.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

        public static string Date(System.DateTime value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        /// <summary>One CSV line; fields are already formatted (use <see cref="Text"/> for free text).</summary>
        public static string Line(IEnumerable<string> fields) =>
            string.Join(",", fields.Select(f => f.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + f.Replace("\"", "\"\"") + "\"" : f));

        public static string Build(IEnumerable<IEnumerable<string>> rows) =>
            string.Concat(rows.Select(r => Line(r) + "\r\n"));

        public static void Write(string path, IEnumerable<IEnumerable<string>> rows) =>
            File.WriteAllText(path, Build(rows), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }
}
