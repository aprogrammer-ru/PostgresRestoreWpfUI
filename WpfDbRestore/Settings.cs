namespace WpfDbRestore;

public class AppSettings
{
    public string BackupFile { get; set; } = @".\backups\2025.backup";
    public string ScriptsFolder { get; set; } = @".\scripts";
    public string Host { get; set; } = "localhost";
    public string Port { get; set; } = "5432";
    public string Username { get; set; } = "postgres";
    public string Database { get; set; } = "bpmsoft";
    public string PgRestorePath { get; set; } = @"~\AppData\Local\Programs\pgAdmin 4\runtime\pg_restore.exe";
}