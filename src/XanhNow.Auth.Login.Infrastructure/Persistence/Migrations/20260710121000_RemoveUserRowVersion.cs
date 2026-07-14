using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XanhNow.Auth.Login.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [Migration("20260710121000_RemoveUserRowVersion")]
    public partial class RemoveUserRowVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "row_version",
                schema: "auth",
                table: "users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "row_version",
                schema: "auth",
                table: "users",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);
        }
    }
}