using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NFMWorld.Archive.Data;

namespace NFMWorld.Archive.Migrations;

[DbContext(typeof(ArchiveDbContext))]
[Migration("20261123223000_ArchiveUploadLimit")]
public sealed partial class ArchiveUploadLimit : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Settings",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false),
                UploadLimitMiB = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_Settings", row => row.Id)
        );

        migrationBuilder.Sql("INSERT INTO Settings (Id, UploadLimitMiB) VALUES (1, 4);");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropTable("Settings");
}
