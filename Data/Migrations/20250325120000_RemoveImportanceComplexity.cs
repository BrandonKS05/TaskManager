using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskManager.Data.Migrations;

public partial class RemoveImportanceComplexity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "importance",
            table: "tasks");

        migrationBuilder.DropColumn(
            name: "complexity",
            table: "tasks");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "importance",
            table: "tasks",
            type: "integer",
            nullable: false,
            defaultValue: 3);

        migrationBuilder.AddColumn<int>(
            name: "complexity",
            table: "tasks",
            type: "integer",
            nullable: false,
            defaultValue: 3);
    }
}
