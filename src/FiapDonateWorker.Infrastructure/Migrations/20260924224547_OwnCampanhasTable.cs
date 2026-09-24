using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FiapDonateWorker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OwnCampanhasTable : Migration
    {
        // A tabela Campanhas era mapeada com ExcludeFromMigrations (a API era dona dela),
        // por isso permaneceu no snapshot do modelo mas nunca teve um CreateTable emitido.
        // Ao passar a posse para este Worker (database-per-service), este CreateTable é
        // escrito explicitamente - o snapshot ja refletia a entidade, entao apenas o
        // Up/Down precisou ser preenchido a mao.
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Campanhas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ValorArrecadado = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Campanhas", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Campanhas");
        }
    }
}
