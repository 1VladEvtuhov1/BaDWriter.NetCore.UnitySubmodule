using Microsoft.Data.Sqlite;

namespace Database.Local.Sqlite.Sqlite
{
    public interface ISqliteConnectionFactory
    {
        SqliteConnection Create();
    }
}