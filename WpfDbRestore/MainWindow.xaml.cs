using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;
using Npgsql;

namespace WpfDbRestore
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
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

                // Применяем настройки к элементам управления
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
                // Обновляем настройки из элементов управления
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
                Owner = this // Делаем главное окно владельцем
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
            missingFields = new List<string>();

            if (string.IsNullOrWhiteSpace(txtBackupFile.Text)) missingFields.Add("Backup File");
            if (string.IsNullOrWhiteSpace(txtScriptsFolder.Text)) missingFields.Add("Scripts Folder");
            if (string.IsNullOrWhiteSpace(txtHost.Text)) missingFields.Add("Host");
            if (string.IsNullOrWhiteSpace(txtPort.Text)) missingFields.Add("Port");
            if (string.IsNullOrWhiteSpace(txtUsername.Text)) missingFields.Add("Username");
            if (string.IsNullOrWhiteSpace(txtPassword.Password)) missingFields.Add("Password");
            if (string.IsNullOrWhiteSpace(txtDatabase.Text)) missingFields.Add("Database");
            if (string.IsNullOrWhiteSpace(txtPgRestorePath.Text)) missingFields.Add("pg_restore Path");

            return missingFields.Count == 0;
        }

        private bool CheckBackupFileExists()
        {
            string backupPath = Path.GetFullPath(txtBackupFile.Text);
            if (!File.Exists(backupPath))
            {
                Log($"Backup file not found: {backupPath}");
                return false;
            }

            return true;
        }

        private bool CheckDatabaseExists()
        {
            string connectionString =
                $"Host={txtHost.Text};Port={txtPort.Text};Username={txtUsername.Text};Password={txtPassword.Password};Database=postgres";

            try
            {
                using (var conn = new NpgsqlConnection(connectionString))
                {
                    conn.Open();
                    using (var cmd = new NpgsqlCommand(
                               $"SELECT 1 FROM pg_database WHERE datname = '{txtDatabase.Text}'", conn))
                    {
                        var result = cmd.ExecuteScalar();
                        if (result != null)
                        {
                            Log($"Database '{txtDatabase.Text}' already exists on the server");
                            return true;
                        }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                Log($"Error checking database existence: {ex.Message}");
                return true; // Assume exists to prevent overwrite
            }
        }

        private void CreateDatabase()
        {
            string connectionString =
                $"Host={txtHost.Text};Port={txtPort.Text};Username={txtUsername.Text};Password={txtPassword.Password};Database=postgres";

            try
            {
                using (var conn = new NpgsqlConnection(connectionString))
                {
                    conn.Open();
                    using (var cmd = new NpgsqlCommand($"CREATE DATABASE \"{txtDatabase.Text}\"", conn))
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

        private void RestoreBackup()
        {
            string pgRestorePath = Environment.ExpandEnvironmentVariables(txtPgRestorePath.Text);
            string backupPath = Path.GetFullPath(txtBackupFile.Text);

            string arguments =
                $"-h {txtHost.Text} -p {txtPort.Text} -U {txtUsername.Text} -d {txtDatabase.Text} -Fc -v \"{backupPath}\"";

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
                    Environment = { ["PGPASSWORD"] = txtPassword.Password }
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
            process.WaitForExit();

            if (process.ExitCode == 0)
            {
                Dispatcher.Invoke(() => Log("Восстановление завершено успешно"));
            }
            else
            {
                throw new Exception($"Процесс восстановления завершился с кодом {process.ExitCode}");
            }
        }

        private void ExecuteScripts()
        {
            string scriptsFolder = Path.GetFullPath(txtScriptsFolder.Text);
            if (!Directory.Exists(scriptsFolder))
            {
                Log($"Scripts folder not found: {scriptsFolder}");
                return;
            }

            string connectionString =
                $"Host={txtHost.Text};Port={txtPort.Text};Username={txtUsername.Text};Password={txtPassword.Password};Database={txtDatabase.Text}";

            var scriptFiles = Directory.GetFiles(scriptsFolder, "*.sql", SearchOption.TopDirectoryOnly);
            if (scriptFiles.Length == 0)
            {
                Log("No SQL scripts found in the specified folder");
                return;
            }

            Array.Sort(scriptFiles); // Execute in alphabetical order

            Log($"Found {scriptFiles.Length} SQL scripts to execute");

            using (var conn = new NpgsqlConnection(connectionString))
            {
                conn.Open();

                foreach (var scriptFile in scriptFiles)
                {
                    Log($"Executing script: {Path.GetFileName(scriptFile)}");

                    try
                    {
                        string scriptContent = File.ReadAllText(scriptFile);
                        using (var cmd = new NpgsqlCommand(scriptContent, conn))
                        {
                            cmd.ExecuteNonQuery();
                        }

                        Log($"Script executed successfully: {Path.GetFileName(scriptFile)}");
                    }
                    catch (Exception ex)
                    {
                        Log($"Error executing script {Path.GetFileName(scriptFile)}: {ex.Message}");
                    }
                }
            }
        }

        private async void RestoreButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Блокируем кнопку на время выполнения
                restoreButton.IsEnabled = false;
                txtLog.Clear();

                // Запускаем процесс восстановления в фоновом потоке
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
                // 1. Валидация
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

                // 2. Создание БД
                await Dispatcher.InvokeAsync(() => Log("Создание базы данных..."));
                await Task.Run(() => CreateDatabase());

                // 3. Восстановление
                await Dispatcher.InvokeAsync(() => Log("Запуск восстановления бэкапа..."));
                await Task.Run(() => RestoreBackup());

                // 4. Выполнение скриптов
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
            // Открываем ссылку в браузере по умолчанию
            Process.Start(new ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
            e.Handled = true;
        }
    }
}