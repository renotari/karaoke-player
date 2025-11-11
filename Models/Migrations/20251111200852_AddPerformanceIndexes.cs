using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KaraokePlayer.Models.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "KeyboardShortcutsJson",
                table: "AppSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_SearchHistory_SearchTerm",
                table: "SearchHistory",
                column: "SearchTerm");

            migrationBuilder.CreateIndex(
                name: "IX_MediaMetadata_Artist_Title",
                table: "MediaMetadata",
                columns: new[] { "Artist", "Title" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SearchHistory_SearchTerm",
                table: "SearchHistory");

            migrationBuilder.DropIndex(
                name: "IX_MediaMetadata_Artist_Title",
                table: "MediaMetadata");

            migrationBuilder.DropColumn(
                name: "KeyboardShortcutsJson",
                table: "AppSettings");
        }
    }
}
