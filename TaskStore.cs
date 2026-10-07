using Microsoft.Data.Sqlite;

namespace TaskApp;

public record TaskItem(long Id, string Title, bool IsComplete);

public class TaskStore
{
    private readonly string connectionString;

    public TaskStore(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var directory = configuration["DataDirectory"] ?? Path.Combine(environment.ContentRootPath, "data");
        Directory.CreateDirectory(directory);
        connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(directory, "tasks.db")
        }.ToString();
    }

    public void Initialize() => Execute("CREATE TABLE IF NOT EXISTS Tasks (Id INTEGER PRIMARY KEY AUTOINCREMENT, Title TEXT NOT NULL, IsComplete INTEGER NOT NULL DEFAULT 0)");

    public List<TaskItem> List()
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Title, IsComplete FROM Tasks ORDER BY IsComplete, Id DESC";
        using var reader = command.ExecuteReader();
        var tasks = new List<TaskItem>();
        while (reader.Read())
        {
            tasks.Add(new TaskItem(reader.GetInt64(0), reader.GetString(1), reader.GetBoolean(2)));
        }
        return tasks;
    }

    public void Create(string title) => Execute("INSERT INTO Tasks (Title) VALUES ($title)", title: title);
    public void Update(long id, string title) => Execute("UPDATE Tasks SET Title = $title WHERE Id = $id", id, title);
    public void Delete(long id) => Execute("DELETE FROM Tasks WHERE Id = $id", id);
    public void Toggle(long id) => Execute("UPDATE Tasks SET IsComplete = 1 - IsComplete WHERE Id = $id", id);

    private void Execute(string sql, long id = 0, string? title = null)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$title", (object?)title ?? DBNull.Value);
        command.ExecuteNonQuery();
    }
}