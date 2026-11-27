using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NFMWorld.Archive.Data;

namespace NFMWorld.Archive.Migrations;

[DbContext(typeof(ArchiveDbContext))]
[Migration("20261127120000_AssetTags")]
public sealed class AssetTags : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("Tags", "Assets", type: "TEXT", nullable: true);
        migrationBuilder.AddColumn<string>("TagsSearch", "Assets", type: "TEXT", nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("Tags", "Assets");
        migrationBuilder.DropColumn("TagsSearch", "Assets");
    }
}
