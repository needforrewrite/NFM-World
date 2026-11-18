using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NFMWorld.Archive.Migrations
{
    /// <inheritdoc />
    public partial class _20261118045058_ModerationRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IsAdmin",
                table: "Users",
                newName: "Role"
            );

            // The rename is a table rebuild that keeps the 0/1 values; admins move to 2.
            migrationBuilder.Sql("UPDATE Users SET Role = 2 WHERE Role = 1;");

            migrationBuilder.AddColumn<string>(
                name: "BanReason",
                table: "Users",
                type: "TEXT",
                nullable: true
            );

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "BannedUntil",
                table: "Users",
                type: "TEXT",
                nullable: true
            );

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "Users",
                type: "TEXT",
                nullable: true
            );

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletionRequestedAt",
                table: "Users",
                type: "TEXT",
                nullable: true
            );

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "KickedUntil",
                table: "Users",
                type: "TEXT",
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "ModerationRevision",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.AddColumn<string>(
                name: "MuteReason",
                table: "Users",
                type: "TEXT",
                nullable: true
            );

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "MutedUntil",
                table: "Users",
                type: "TEXT",
                nullable: true
            );

            migrationBuilder.CreateTable(
                name: "ModerationActions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ActorUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    ActorName = table.Column<string>(type: "TEXT", nullable: true),
                    TargetUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    TargetName = table.Column<string>(type: "TEXT", nullable: false),
                    Action = table.Column<byte>(type: "INTEGER", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModerationActions", x => x.Id);
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_Users_DeletedAt",
                table: "Users",
                column: "DeletedAt"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ModerationActions_CreatedAt",
                table: "ModerationActions",
                column: "CreatedAt"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ModerationActions_TargetUserId_Id",
                table: "ModerationActions",
                columns: new[] { "TargetUserId", "Id" }
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ModerationActions"
            );

            migrationBuilder.DropIndex(
                name: "IX_Users_DeletedAt",
                table: "Users"
            );

            migrationBuilder.DropColumn(
                name: "BanReason",
                table: "Users"
            );

            migrationBuilder.DropColumn(
                name: "BannedUntil",
                table: "Users"
            );

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Users"
            );

            migrationBuilder.DropColumn(
                name: "DeletionRequestedAt",
                table: "Users"
            );

            migrationBuilder.DropColumn(
                name: "KickedUntil",
                table: "Users"
            );

            migrationBuilder.DropColumn(
                name: "ModerationRevision",
                table: "Users"
            );

            migrationBuilder.DropColumn(
                name: "MuteReason",
                table: "Users"
            );

            migrationBuilder.DropColumn(
                name: "MutedUntil",
                table: "Users"
            );

            migrationBuilder.RenameColumn(
                name: "Role",
                table: "Users",
                newName: "IsAdmin"
            );
        }
    }
}
