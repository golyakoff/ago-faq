using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ago.Faq.Infrastructure.Postgres.Migrations;

/// <inheritdoc />
public partial class Stage19CreateFaqSchema : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "faq_module_tasks",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                site_id = table.Column<Guid>(type: "uuid", nullable: false),
                state = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_faq_module_tasks", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "knowledge_bases",
            columns: table => new
            {
                site_id = table.Column<Guid>(type: "uuid", nullable: false),
                text = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_knowledge_bases", x => x.site_id);
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "faq_module_tasks");

        migrationBuilder.DropTable(
            name: "knowledge_bases");
    }
}
