using Microsoft.Data.Sqlite;

namespace Database.Local.Sqlite.Sqlite {

public sealed class FileSqliteConnectionFactory : ISqliteConnectionFactory
{
    private readonly string _cs;

    public FileSqliteConnectionFactory(string path)
    {
        var sb = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        };
        
        _cs = sb + ";Foreign Keys=True;";
    }

    public SqliteConnection Create()
    {
        var conn = new SqliteConnection(_cs);
        conn.Open();

        // Единообразные PRAGMA для всех соединений
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys=ON; PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;";
        cmd.ExecuteNonQuery();

        return conn;
    }
}
}
