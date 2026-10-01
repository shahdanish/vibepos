using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POSApp.Data.Migrations
{
    /// <summary>
    /// Additive only: four non-unique lookup indexes. Written as IF NOT EXISTS so the migration
    /// is safe to re-run against a database where an index was already created by hand.
    /// </summary>
    public partial class AddLookupIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_Sales_InvoiceNumber\" ON \"Sales\" (\"InvoiceNumber\");");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_Sales_SaleDate\" ON \"Sales\" (\"SaleDate\");");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_SaleItems_ProductId\" ON \"SaleItems\" (\"ProductId\");");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_Products_ProductId\" ON \"Products\" (\"ProductId\");");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Sales_InvoiceNumber\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Sales_SaleDate\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_SaleItems_ProductId\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Products_ProductId\";");
        }
    }
}
