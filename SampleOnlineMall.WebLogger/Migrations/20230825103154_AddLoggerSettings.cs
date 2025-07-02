using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SampleOnlineMall.WebLogger.Migrations
{
    public partial class AddLoggerSettings : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WebLoggerSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RefreshPeriodMs = table.Column<int>(type: "integer", nullable: false),
                    ItemsPerPage = table.Column<int>(type: "integer", nullable: false),
                    LogsKeepingPeriod = table.Column<TimeSpan>(type: "interval", nullable: false),
                    SysMessage = table.Column<string>(type: "text", nullable: true),
                    CreatedDateTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    LastModifiedDatTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebLoggerSettings", x => x.Id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WebLoggerSettings");
        }
    }
}
