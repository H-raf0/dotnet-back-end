using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppServerApi.Migrations
{
    /// <inheritdoc />
    public partial class AddPortfolioToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Balance",
                table: "Users",
                type: "REAL",
                nullable: false,
                defaultValue: 10000.0);

            migrationBuilder.AddColumn<string>(
                name: "StocksJson",
                table: "Users",
                type: "TEXT",
                nullable: false,
                defaultValue: "{}");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Balance",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "StocksJson",
                table: "Users");
        }
    }
}
