using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NFMWorld.Archive.Migrations
{
    /// <inheritdoc />
    public partial class _20261111160633_AddAssetValidation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsValidated",
                table: "Assets",
                type: "INTEGER",
                nullable: false,
                defaultValue: false
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsValidated",
                table: "Assets"
            );
        }
    }
}
