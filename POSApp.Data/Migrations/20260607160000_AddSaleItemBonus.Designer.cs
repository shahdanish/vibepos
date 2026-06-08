using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using POSApp.Data;

#nullable disable

namespace POSApp.Data.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260607160000_AddSaleItemBonus")]
    partial class AddSaleItemBonus
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
            modelBuilder.HasAnnotation("ProductVersion", "9.0.10");
        }
    }
}
