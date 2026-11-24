using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MechanicShop.Infrastructure.Migrations
{
    /// <summary>ترحيل لإضافة الفروع ونطاق الوصول ورمز التزامن.</summary>
    public partial class AddBranchesAndWorkOrderRowVersion : Migration
    {
        /// <summary>تطبيق التغييرات على قاعدة البيانات.</summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [WorkOrders]) THROW 51000, N'يجب إخلاء أوامر الشغل التجريبية بإعادة ضبط صريحة في Development أو Testing قبل تفعيل ملكية الفروع.', 1;");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkOrders_Employees_LaborId",
                table: "WorkOrders");

            migrationBuilder.AddColumn<string>(
                name: "AccessScope",
                table: "Employees",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Branch");

            migrationBuilder.AddColumn<Guid>(
                name: "BranchId",
                table: "WorkOrders",
                type: "uniqueidentifier",
                nullable: false);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "WorkOrders",
                type: "rowversion",
                rowVersion: true,
                nullable: false);

            migrationBuilder.CreateTable(
                name: "Branches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Branches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeBranchMemberships",
                columns: table => new
                {
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeBranchMemberships", x => new { x.EmployeeId, x.BranchId });
                    table.ForeignKey(
                        name: "FK_EmployeeBranchMemberships_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmployeeBranchMemberships_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_BranchId_StartAtUtc_EndAtUtc",
                table: "WorkOrders",
                columns: new[] { "BranchId", "StartAtUtc", "EndAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Branches_Code",
                table: "Branches",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeBranchMemberships_BranchId",
                table: "EmployeeBranchMemberships",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeBranchMemberships_EmployeeId",
                table: "EmployeeBranchMemberships",
                column: "EmployeeId",
                unique: true,
                filter: "[IsDefault] = 1");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkOrders_Branches_BranchId",
                table: "WorkOrders",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkOrders_Employees_LaborId",
                table: "WorkOrders",
                column: "LaborId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <summary>التراجع عن تغييرات قاعدة البيانات.</summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccessScope",
                table: "Employees");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkOrders_Branches_BranchId",
                table: "WorkOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkOrders_Employees_LaborId",
                table: "WorkOrders");

            migrationBuilder.DropTable(
                name: "EmployeeBranchMemberships");

            migrationBuilder.DropTable(
                name: "Branches");

            migrationBuilder.DropIndex(
                name: "IX_WorkOrders_BranchId_StartAtUtc_EndAtUtc",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "WorkOrders");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkOrders_Employees_LaborId",
                table: "WorkOrders",
                column: "LaborId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
