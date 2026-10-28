using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniWallet.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FinancialGuards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE FUNCTION wallet_immutable_entry() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN RAISE EXCEPTION 'Financial history is immutable' USING ERRCODE = '23514'; END; $$;
                CREATE TRIGGER immutable_entry BEFORE UPDATE OR DELETE ON "FinancialEntries"
                  FOR EACH ROW EXECUTE FUNCTION wallet_immutable_entry();

                CREATE FUNCTION wallet_check_win() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF NEW."Type" = 'WIN' AND NOT EXISTS (
                    SELECT 1 FROM "FinancialEntries" b WHERE b."Id" = NEW."OriginalBetId"
                      AND b."Type" = 'BET' AND b."ProviderId" = NEW."ProviderId"
                      AND b."PlayerId" = NEW."PlayerId" AND b."Currency" = NEW."Currency"
                  ) THEN RAISE EXCEPTION 'WIN requires a matching accepted BET' USING ERRCODE = '23514'; END IF;
                  RETURN NEW;
                END; $$;
                CREATE TRIGGER matching_win BEFORE INSERT ON "FinancialEntries"
                  FOR EACH ROW EXECUTE FUNCTION wallet_check_win();

                CREATE FUNCTION wallet_immutable_outcome() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF OLD."Status" <> 0 THEN
                    RAISE EXCEPTION 'Committed outcome is immutable' USING ERRCODE = '23514';
                  END IF;
                  RETURN NEW;
                END; $$;
                CREATE TRIGGER immutable_outcome BEFORE UPDATE OR DELETE ON "TransactionOutcomes"
                  FOR EACH ROW EXECUTE FUNCTION wallet_immutable_outcome();

                CREATE FUNCTION wallet_final_outcome() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE final_status integer; final_response text;
                BEGIN
                  SELECT "Status", "Response" INTO final_status, final_response FROM "TransactionOutcomes"
                    WHERE "ProviderId" = NEW."ProviderId" AND "ExternalTransactionId" = NEW."ExternalTransactionId";
                  IF final_status IS NULL OR final_status = 0 OR final_response IS NULL THEN
                    RAISE EXCEPTION 'Outcome must finalize before commit' USING ERRCODE = '23514';
                  END IF;
                  RETURN NULL;
                END; $$;
                CREATE CONSTRAINT TRIGGER finalized_outcome AFTER INSERT OR UPDATE ON "TransactionOutcomes"
                  DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION wallet_final_outcome();
                """);
            migrationBuilder.DropCheckConstraint(
                name: "CK_Wallets_Scale",
                table: "Wallets");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Wallets_Scale",
                table: "Wallets",
                sql: "(\"Currency\" = 'BTC' AND \"Balance\" = trunc(\"Balance\",8)) OR (\"Currency\" IN ('USD','EUR') AND \"Balance\" = trunc(\"Balance\",2))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER finalized_outcome ON "TransactionOutcomes";
                DROP FUNCTION wallet_final_outcome();
                DROP TRIGGER immutable_outcome ON "TransactionOutcomes";
                DROP FUNCTION wallet_immutable_outcome();
                DROP TRIGGER matching_win ON "FinancialEntries";
                DROP FUNCTION wallet_check_win();
                DROP TRIGGER immutable_entry ON "FinancialEntries";
                DROP FUNCTION wallet_immutable_entry();
                """);
            migrationBuilder.DropCheckConstraint(
                name: "CK_Wallets_Scale",
                table: "Wallets");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Wallets_Scale",
                table: "Wallets",
                sql: "\"Currency\" = 'BTC' OR \"Balance\" = trunc(\"Balance\", 2)");
        }
    }
}
