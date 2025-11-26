using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SftpApi.Migrations
{
    /// <inheritdoc />
    public partial class UpdateFailedUpload : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FailedUploads",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SftpAuthKeyId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    LocalFilePath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RemotePath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FailedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsRetried = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FailedUploads", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FailedUploads");
        }
    }
}
