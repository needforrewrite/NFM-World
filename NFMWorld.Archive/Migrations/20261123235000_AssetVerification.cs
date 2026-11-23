using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NFMWorld.Archive.Data;

namespace NFMWorld.Archive.Migrations;

[DbContext(typeof(ArchiveDbContext))]
[Migration("20261123235000_AssetVerification")]
public sealed partial class AssetVerification : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>("IsVerifiedUploader", "Users", type: "INTEGER", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<int>("ReviewStatus", "Assets", type: "INTEGER", nullable: false, defaultValue: 0);
        migrationBuilder.CreateIndex("IX_Assets_ReviewStatus_Id", "Assets", new[] { "ReviewStatus", "Id" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("IX_Assets_ReviewStatus_Id", "Assets");
        migrationBuilder.DropColumn("ReviewStatus", "Assets");
        migrationBuilder.DropColumn("IsVerifiedUploader", "Users");
    }
}
