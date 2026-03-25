using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskManager.Data.Migrations;

public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "lists",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                device_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_lists", x => x.id));

        migrationBuilder.CreateTable(
            name: "tasks",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                device_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                list_id = table.Column<Guid>(type: "uuid", nullable: false),
                title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                is_complete = table.Column<bool>(type: "boolean", nullable: false),
                created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                tag = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                priority = table.Column<int>(type: "integer", nullable: false),
                importance = table.Column<int>(type: "integer", nullable: false),
                complexity = table.Column<int>(type: "integer", nullable: false),
                due_date = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tasks", x => x.id);
                table.ForeignKey(
                    name: "FK_tasks_lists_list_id",
                    column: x => x.list_id,
                    principalTable: "lists",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_lists_device_id",
            table: "lists",
            column: "device_id");

        migrationBuilder.CreateIndex(
            name: "IX_tasks_device_id",
            table: "tasks",
            column: "device_id");

        migrationBuilder.CreateIndex(
            name: "IX_tasks_list_id",
            table: "tasks",
            column: "list_id");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "tasks");
        migrationBuilder.DropTable(name: "lists");
    }
}
