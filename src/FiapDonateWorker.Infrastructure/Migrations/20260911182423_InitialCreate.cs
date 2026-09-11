using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FiapDonateWorker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Doacoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IdCampanha = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ValorDoacao = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DataHoraRecebida = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DataHoraProcessada = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Doacoes", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Doacoes");
        }
    }
}
