using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using POSApp.Data;

#nullable disable

namespace POSApp.Data.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260607140000_RemovePharmacyFromAdmin")]
    partial class RemovePharmacyFromAdmin
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
            modelBuilder.HasAnnotation("ProductVersion", "9.0.10");
        }
    }
}
