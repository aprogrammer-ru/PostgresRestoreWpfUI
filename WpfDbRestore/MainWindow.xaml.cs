using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;
using Npgsql;

namespace WpfDbRestore
{
    public partial class MainWindow : Window
    {
        private const string SettingsFile = "settings.json";
        private AppSettings _settings;

        public MainWindow()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsFile))
                {
                    var json = File.ReadAllText(SettingsFile);
                    _settings = JsonSerializer.Deserialize<AppSettings>(json);
                }
                else
                {
                    _settings = new AppSettings();
                }

                txtBackupFile.Text = _settings.BackupFile;
                txtScriptsFolder.Text = _settings.ScriptsFolder;
                txtHost.Text = _settings.Host;
                txtPort.Text = _settings.Port;
                txtUsername.Text = _settings.Username;
                txtDatabase.Text = _settings.Database;
                txtPgRestorePath.Text = _settings.PgRestorePath;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки настроек: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                _settings = new AppSettings();
            }
        }

        private void SaveSettings()
        {
            try
            {
                _settings.BackupFile = txtBackupFile.Text;
                _settings.ScriptsFolder = txtScriptsFolder.Text;
                _settings.Host = txtHost.Text;
                _settings.Port = txtPort.Text;
                _settings.Username = txtUsername.Text;
                _settings.Database = txtDatabase.Text;
                _settings.PgRestorePath = txtPgRestorePath.Text;

                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(_settings, options);
                File.WriteAllText(SettingsFile, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения настроек: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            SaveSettings();
        }

        private void HelpHyperlink_Click(object sender, RoutedEventArgs e)
        {
            var helpWindow = new HelpWindow
            {
                Owner = this
            };
            helpWindow.ShowDialog();
        }

        private void BrowseBackupFile_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "PostgreSQL Backup Files (*.backup)|*.backup|All files (*.*)|*.*",
                InitialDirectory = Path.GetFullPath(Path.GetDirectoryName(txtBackupFile.Text)) ??
                                   Environment.CurrentDirectory,
                FileName = Path.GetFileName(txtBackupFile.Text)
            };

            if (openFileDialog.ShowDialog() == true)
            {
                txtBackupFile.Text = openFileDialog.FileName;
            }
        }

        private void BrowseScriptsFolder_Click(object sender, RoutedEventArgs e)
        {
            var openFolderDialog = new OpenFileDialog
            {
                ValidateNames = false,
                CheckFileExists = false,
                CheckPathExists = true,
                FileName = "Folder Selection",
                InitialDirectory = Path.GetFullPath(txtScriptsFolder.Text) ?? Environment.CurrentDirectory
            };

            if (openFolderDialog.ShowDialog() == true)
            {
                string selectedPath = Path.GetDirectoryName(openFolderDialog.FileName);
                if (!string.IsNullOrEmpty(selectedPath))
                {
                    txtScriptsFolder.Text = selectedPath;
                }
            }
        }

        private void BrowsePgRestorePath_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Executable Files (*.exe)|*.exe|All files (*.*)|*.*",
                InitialDirectory = Path.GetFullPath(Path.GetDirectoryName(txtPgRestorePath.Text)) ??
                                   Environment.CurrentDirectory,
                FileName = Path.GetFileName(txtPgRestorePath.Text)
            };

            if (openFileDialog.ShowDialog() == true)
            {
                txtPgRestorePath.Text = openFileDialog.FileName;
            }
        }

        private void Log(string message)
        {
            txtLog.AppendText($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}{Environment.NewLine}");
            txtLog.ScrollToEnd();
        }

        private bool ValidateInputs(out List<string> missingFields)
        {
            List<string> localMissingFields = new List<string>();
            Dispatcher.Invoke(() =>
            {
                if (string.IsNullOrWhiteSpace(txtBackupFile.Text)) localMissingFields.Add("Backup File");
                if (string.IsNullOrWhiteSpace(txtScriptsFolder.Text)) localMissingFields.Add("Scripts Folder");
                if (string.IsNullOrWhiteSpace(txtHost.Text)) localMissingFields.Add("Host");
                if (string.IsNullOrWhiteSpace(txtPort.Text)) localMissingFields.Add("Port");
                if (string.IsNullOrWhiteSpace(txtUsername.Text)) localMissingFields.Add("Username");
                if (string.IsNullOrWhiteSpace(txtPassword.Password)) localMissingFields.Add("Password");
                if (string.IsNullOrWhiteSpace(txtDatabase.Text)) localMissingFields.Add("Database");
                if (string.IsNullOrWhiteSpace(txtPgRestorePath.Text)) localMissingFields.Add("pg_restore Path");
            });
            missingFields = localMissingFields;
            return missingFields.Count == 0;
        }

        private bool CheckBackupFileExists()
        {
            string backupPath = string.Empty;
            Dispatcher.Invoke(() => { backupPath = Path.GetFullPath(txtBackupFile.Text); });

            if (!File.Exists(backupPath))
            {
                Dispatcher.Invoke(() => Log($"Backup file not found: {backupPath}"));
                return false;
            }

            return true;
        }

        private bool CheckDatabaseExists()
        {
            string connectionString = string.Empty;
            Dispatcher.Invoke(() =>
            {
                connectionString =
                    $"Host={txtHost.Text};Port={txtPort.Text};Username={txtUsername.Text};Password={txtPassword.Password};Database=postgres";
            });

            try
            {
                using (var conn = new NpgsqlConnection(connectionString))
                {
                    conn.Open();
                    string cmdText = string.Empty;
                    Dispatcher.Invoke(() =>
                        cmdText = $"SELECT 1 FROM pg_database WHERE datname = '{txtDatabase.Text}'");
                    using (var cmd = new NpgsqlCommand(
                               $"{cmdText}", conn))
                    {
                        var result = cmd.ExecuteScalar();
                        if (result != null)
                        {
                            Dispatcher.Invoke(() => Log($"Database '{txtDatabase.Text}' already exists on the server"));
                            return true;
                        }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => Log($"Error checking database existence: {ex.Message}"));
                return true; // Assume exists to prevent overwrite
            }
        }

        private void CreateDatabase()
        {
            string connectionString = string.Empty;
            Dispatcher.Invoke(() => connectionString =
                $"Host={txtHost.Text};Port={txtPort.Text};Username={txtUsername.Text};Password={txtPassword.Password};Database=postgres");

            try
            {
                using (var conn = new NpgsqlConnection(connectionString))
                {
                    conn.Open();
                    string cmdText = string.Empty;
                    Dispatcher.Invoke(() => cmdText = $"CREATE DATABASE \"{txtDatabase.Text}\"");
                    using (var cmd = new NpgsqlCommand($"{cmdText}", conn))
                    {
                        cmd.ExecuteNonQuery();
                        Dispatcher.Invoke(() => Log($"База данных '{txtDatabase.Text}' создана"));
                    }
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => Log($"Ошибка создания базы данных: {ex.Message}"));
                throw;
            }
        }

        private async Task RestoreBackup()
        {
            string pgRestorePath = string.Empty;
            Dispatcher.Invoke(() => pgRestorePath = Environment.ExpandEnvironmentVariables(txtPgRestorePath.Text));
            pgRestorePath = Environment.ExpandEnvironmentVariables(
                pgRestorePath.Replace("~", "%USERPROFILE%"));
            string backupPath = string.Empty;
            Dispatcher.Invoke(() => backupPath = Path.GetFullPath(txtBackupFile.Text));

            string arguments = string.Empty;
            Dispatcher.Invoke(() =>
                arguments =
                    $"-h {txtHost.Text} -p {txtPort.Text} -U {txtUsername.Text} -d {txtDatabase.Text} -Fc -v \"{backupPath}\"");
            var pgsPassw = string.Empty;
            Dispatcher.Invoke(()=> pgsPassw = txtPassword.Password);
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = pgRestorePath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    Environment = { ["PGPASSWORD"] = pgsPassw }
                }
            };

            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null) Dispatcher.Invoke(() => Log(e.Data));
            };
            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null) Dispatcher.Invoke(() => Log($"ERROR: {e.Data}"));
            };

            Dispatcher.Invoke(() => Log($"Запуск: {pgRestorePath} {arguments}"));
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            
            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
            {
                Dispatcher.Invoke(() => Log("Восстановление завершено успешно"));
            }
            else
            {
                throw new Exception($"Процесс восстановления завершился с кодом {process.ExitCode}");
            }
        }

        private async Task ExecuteScripts()
        {
            string scriptsFolder = string.Empty;
            Dispatcher.Invoke(() => scriptsFolder = Path.GetFullPath(txtScriptsFolder.Text));

            if (!Directory.Exists(scriptsFolder))
            {
                Dispatcher.Invoke(() => Log($"Scripts folder not found: {scriptsFolder}"));
                return;
            }

            string connectionString = string.Empty;
            Dispatcher.Invoke(() =>
                connectionString =
                    $"Host={txtHost.Text};Port={txtPort.Text};Username={txtUsername.Text};Password={txtPassword.Password};Database={txtDatabase.Text}");

            var scriptFiles = Directory.GetFiles(scriptsFolder, "*.sql", SearchOption.TopDirectoryOnly);
            if (scriptFiles.Length == 0)
            {
                Dispatcher.Invoke(() => Log("No SQL scripts found in the specified folder"));
                return;
            }

            Array.Sort(scriptFiles);

            Dispatcher.Invoke(() => Log($"Found {scriptFiles.Length} SQL scripts to execute"));

            using (var conn = new NpgsqlConnection(connectionString))
            {
                conn.Open();

                foreach (var scriptFile in scriptFiles)
                {
                    Dispatcher.Invoke(() => Log($"Executing script: {Path.GetFileName(scriptFile)}"));

                    try
                    {
                        string scriptContent = File.ReadAllText(scriptFile);
                        using (var cmd = new NpgsqlCommand(scriptContent, conn))
                        {
                            await cmd.ExecuteNonQueryAsync();
                        }

                        Dispatcher.Invoke(() => Log($"Script executed successfully: {Path.GetFileName(scriptFile)}"));
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() =>
                            Log($"Error executing script {Path.GetFileName(scriptFile)}: {ex.Message}"));
                    }
                }
            }
        }

        private async void RestoreButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                restoreButton.IsEnabled = false;
                txtLog.Clear();

                await Task.Run(() => PerformRestoreAsync());
            }
            catch (Exception ex)
            {
                Log($"Ошибка: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Log($"Внутренняя ошибка: {ex.InnerException.Message}");
                }
            }
            finally
            {
                restoreButton.IsEnabled = true;
            }
        }

        private async Task PerformRestoreAsync()
        {
            try
            {
                await Dispatcher.InvokeAsync(() => Log("Начало проверки параметров..."));
                if (!ValidateInputs(out var missingFields))
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        Log("Не заполнены обязательные поля:");
                        foreach (var field in missingFields) Log($"- {field}");
                    });
                    return;
                }

                if (!CheckBackupFileExists()) return;
                if (CheckDatabaseExists()) return;

                await Dispatcher.InvokeAsync(() => Log("Создание базы данных..."));
                await Task.Run(() => CreateDatabase());

                await Dispatcher.InvokeAsync(() => Log("Запуск восстановления бэкапа..."));
                await Task.Run(() => RestoreBackup());

                await Dispatcher.InvokeAsync(() => Log("Выполнение SQL-скриптов..."));
                await Task.Run(() => ExecuteScripts());

                await Dispatcher.InvokeAsync(() => Log("Процесс восстановления завершен успешно!"));
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() => Log($"Ошибка во время восстановления: {ex.Message}"));
                throw;
            }
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
            e.Handled = true;
        }
    }
}