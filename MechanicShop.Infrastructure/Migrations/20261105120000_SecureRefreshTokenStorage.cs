using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MechanicShop.Infrastructure.Migrations;

public partial class SecureRefreshTokenStorage : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "FamilyId",
            table: "RefreshTokens",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "RevokedOnUtc",
            table: "RefreshTokens",
            type: "datetimeoffset",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ReplacedByTokenHash",
            table: "RefreshTokens",
            type: "nvarchar(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "TokenHash",
            table: "RefreshTokens",
            type: "nvarchar(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.Sql(
            "DELETE FROM [RefreshTokens] WHERE [Token] IS NULL OR LTRIM(RTRIM([Token])) = ''; " +
            "UPDATE [RefreshTokens] SET [TokenHash] = CONVERT(varchar(64), HASHBYTES('SHA2_256', CONVERT(varchar(max), [Token])), 2), [FamilyId] = NEWID();");

        migrationBuilder.DropIndex(
            name: "IX_RefreshTokens_Token",
            table: "RefreshTokens");

        migrationBuilder.AlterColumn<Guid>(
            name: "FamilyId",
            table: "RefreshTokens",
            type: "uniqueidentifier",
            nullable: false,
            oldClrType: typeof(Guid),
            oldType: "uniqueidentifier",
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "TokenHash",
            table: "RefreshTokens",
            type: "nvarchar(64)",
            maxLength: 64,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(64)",
            oldMaxLength: 64,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "UserId",
            table: "RefreshTokens",
            type: "nvarchar(450)",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(max)");

        migrationBuilder.DropColumn(
            name: "Token",
            table: "RefreshTokens");

        migrationBuilder.CreateIndex(
            name: "IX_RefreshTokens_TokenHash",
            table: "RefreshTokens",
            column: "TokenHash",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_RefreshTokens_UserId_FamilyId",
            table: "RefreshTokens",
            columns: new[] { "UserId", "FamilyId" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_RefreshTokens_TokenHash",
            table: "RefreshTokens");

        migrationBuilder.DropIndex(
            name: "IX_RefreshTokens_UserId_FamilyId",
            table: "RefreshTokens");

        migrationBuilder.AlterColumn<string>(
            name: "UserId",
            table: "RefreshTokens",
            type: "nvarchar(max)",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(450)");

        migrationBuilder.AddColumn<string>(
            name: "Token",
            table: "RefreshTokens",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.Sql(
            "UPDATE [RefreshTokens] SET [Token] = CONCAT('revoked-', [TokenHash]);");

        migrationBuilder.DropColumn(name: "FamilyId", table: "RefreshTokens");
        migrationBuilder.DropColumn(name: "RevokedOnUtc", table: "RefreshTokens");
        migrationBuilder.DropColumn(name: "ReplacedByTokenHash", table: "RefreshTokens");
        migrationBuilder.DropColumn(name: "TokenHash", table: "RefreshTokens");

        migrationBuilder.CreateIndex(
            name: "IX_RefreshTokens_Token",
            table: "RefreshTokens",
            column: "Token",
            unique: true,
            filter: "[Token] IS NOT NULL");
    }
}
