# BadWriter Runtime (UPM)
Слои:
- BadWriter.Contracts
- Database.Abstractions
- Database.Application
- Database.Local.Sqlite

Зависимости:
- Microsoft.Data.Sqlite (managed)
- SQLitePCLRaw.core + bundle_e_sqlite3 (managed + native)
- System.Text.Json (managed)

> В пакете есть `Runtime/BadWriter.RuntimeInit/SqliteInit.cs`, он вызывает `SQLitePCL.Batteries_V2.Init()` в Unity. Не нужен — удали папку.
