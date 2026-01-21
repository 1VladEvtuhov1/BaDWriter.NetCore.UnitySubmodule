namespace Database.Local.Sqlite.Mappers
{
    public static class CardSql
    {
        public const string ColId = "id";
        public const string ColParentId = "parent_id";
        public const string ColName = "name";
        public const string ColDescription = "description";
        public const string ColArtPath = "art_path";
        public const string ColOrder = "order";
        public const string ColVersion = "version";
        public const string ColUpdatedAtUtc = "updated_at_utc";
        public const string ColIsDeleted = "is_deleted";
        public const string ColHasLayout = "has_layout";
        public const string ColLayoutVersion = "layout_version";
        public const string ColLayoutUpdatedAtUtc = "layout_updated_at_utc";

        public static string SelectColumns(string alias)
        {
            // Quote only "order" (reserved word)
            var a = alias?.Trim();
            if (string.IsNullOrEmpty(a)) a = "";
            else a += ".";

            return $@"
{a}{ColId}                   AS {ColId},
{a}{ColParentId}            AS {ColParentId},
{a}{ColName}                AS {ColName},
{a}{ColDescription}         AS {ColDescription},
{a}{ColArtPath}             AS {ColArtPath},
{a}""{ColOrder}""            AS ""{ColOrder}"",
{a}{ColVersion}             AS {ColVersion},
{a}{ColUpdatedAtUtc}        AS {ColUpdatedAtUtc},
{a}{ColIsDeleted}           AS {ColIsDeleted},
{a}{ColHasLayout}           AS {ColHasLayout},
{a}{ColLayoutVersion}       AS {ColLayoutVersion},
{a}{ColLayoutUpdatedAtUtc}  AS {ColLayoutUpdatedAtUtc}".Trim();
        }
    }
}