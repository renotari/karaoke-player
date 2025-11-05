using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KaraokePlayer.Models.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppSettings",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    MediaDirectory = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayMode = table.Column<string>(type: "TEXT", nullable: false),
                    Volume = table.Column<double>(type: "REAL", nullable: false),
                    AudioBoostEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    AudioOutputDevice = table.Column<string>(type: "TEXT", nullable: false),
                    CrossfadeEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CrossfadeDuration = table.Column<int>(type: "INTEGER", nullable: false),
                    AutoPlayEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    ShuffleMode = table.Column<bool>(type: "INTEGER", nullable: false),
                    VisualizationStyle = table.Column<string>(type: "TEXT", nullable: false),
                    Theme = table.Column<string>(type: "TEXT", nullable: false),
                    FontSize = table.Column<int>(type: "INTEGER", nullable: false),
                    PreloadBufferSize = table.Column<int>(type: "INTEGER", nullable: false),
                    CacheSize = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModified = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MediaFiles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", nullable: false),
                    Filename = table.Column<string>(type: "TEXT", nullable: false),
                    Type = table.Column<string>(type: "TEXT", nullable: false),
                    Format = table.Column<string>(type: "TEXT", nullable: false),
                    ThumbnailPath = table.Column<string>(type: "TEXT", nullable: true),
                    MetadataLoaded = table.Column<bool>(type: "INTEGER", nullable: false),
                    ThumbnailLoaded = table.Column<bool>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModified = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaFiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MediaMetadata",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    MediaFileId = table.Column<string>(type: "TEXT", nullable: false),
                    Duration = table.Column<double>(type: "REAL", nullable: false),
                    Artist = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Album = table.Column<string>(type: "TEXT", nullable: true),
                    ResolutionWidth = table.Column<int>(type: "INTEGER", nullable: true),
                    ResolutionHeight = table.Column<int>(type: "INTEGER", nullable: true),
                    FileSize = table.Column<long>(type: "INTEGER", nullable: false),
                    HasSubtitles = table.Column<bool>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaMetadata", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaMetadata_MediaFiles_MediaFileId",
                        column: x => x.MediaFileId,
                        principalTable: "MediaFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlaylistItems",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    MediaFileId = table.Column<string>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsDuplicate = table.Column<bool>(type: "INTEGER", nullable: false),
                    Error = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaylistItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlaylistItems_MediaFiles_MediaFileId",
                        column: x => x.MediaFileId,
                        principalTable: "MediaFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MediaFiles_Filename",
                table: "MediaFiles",
                column: "Filename");

            migrationBuilder.CreateIndex(
                name: "IX_MediaFiles_FilePath",
                table: "MediaFiles",
                column: "FilePath",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MediaMetadata_Artist",
                table: "MediaMetadata",
                column: "Artist");

            migrationBuilder.CreateIndex(
                name: "IX_MediaMetadata_MediaFileId",
                table: "MediaMetadata",
                column: "MediaFileId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MediaMetadata_Title",
                table: "MediaMetadata",
                column: "Title");

            migrationBuilder.CreateIndex(
                name: "IX_PlaylistItems_MediaFileId",
                table: "PlaylistItems",
                column: "MediaFileId");

            migrationBuilder.CreateIndex(
                name: "IX_PlaylistItems_Position",
                table: "PlaylistItems",
                column: "Position");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppSettings");

            migrationBuilder.DropTable(
                name: "MediaMetadata");

            migrationBuilder.DropTable(
                name: "PlaylistItems");

            migrationBuilder.DropTable(
                name: "MediaFiles");
        }
    }
}
