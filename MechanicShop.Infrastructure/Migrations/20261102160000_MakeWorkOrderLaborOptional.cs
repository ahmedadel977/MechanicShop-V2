using MechanicShop.Infrastructure.Date;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MechanicShop.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261102160000_MakeWorkOrderLaborOptional")]
public partial class MakeWorkOrderLaborOptional : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<Guid>(
            name: "LaborId",
            table: "WorkOrders",
            type: "uniqueidentifier",
            nullable: true,
            oldClrType: typeof(Guid),
            oldType: "uniqueidentifier");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [WorkOrders] WHERE [LaborId] IS NULL) THROW 51000, 'Cannot restore required LaborId while unassigned work orders exist.', 1;");

        migrationBuilder.AlterColumn<Guid>(
            name: "LaborId",
            table: "WorkOrders",
            type: "uniqueidentifier",
            nullable: false,
            oldClrType: typeof(Guid?),
            oldType: "uniqueidentifier",
            oldNullable: true);
    }
}
