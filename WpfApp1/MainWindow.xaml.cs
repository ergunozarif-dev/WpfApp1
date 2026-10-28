using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Data.SqlClient;

namespace WpfApp1
{
    public partial class MainWindow : Window
    {
        private static readonly HttpClient client = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(10)
        };

        private readonly OllamaService ollamaService = new OllamaService();

        private const string ConnectionString =
            @"Server=.\SQLEXPRESS05;Database=MasaustuAsistanDB;Integrated Security=True;TrustServerCertificate=True;";

        private const string OllamaUrl = "http://localhost:11434";
        private const string ModelName = "qwen3:8b";

        private const int MaxMessageLength = 4000;
        private const int MaxHistoryMessages = 10;

        private bool isSending;
        private bool isDatabaseReady;
        private bool isAssistantReady;
        private bool isStartingAssistant;
        private bool isStoppingAssistant;

        public ObservableCollection<ChatMessage> Messages { get; }
            = new ObservableCollection<ChatMessage>();

        public MainWindow()
        {
            InitializeComponent();

            DataContext = this;
            ChatMessages.ItemsSource = Messages;

            MessageInput.IsEnabled = false;
            SendButton.IsEnabled = false;

            UpdatePowerButtonVisual();

            StatusText.Text = "Veritabanı kontrol ediliyor...";

            _ = InitializeChatAsync();
        }

        // ---------------------------------------------------------
        // ASİSTANI AÇ / KAPAT
        // ---------------------------------------------------------

        private async void StartAssistant_Click(
            object sender, RoutedEventArgs e)
        {
            if (isStartingAssistant ||
                isStoppingAssistant ||
                isSending)
            {
                return;
            }

            if (isAssistantReady)
                await TurnAssistantOffAsync();
            else
                await TurnAssistantOnAsync();
        }

        private async Task TurnAssistantOnAsync()
        {
            isStartingAssistant = true;

            UpdatePowerButtonVisual();
            UpdateChatControls();

            StartAssistantButton.Content = "...";
            StatusText.Text = "Ollama ve model kontrol ediliyor...";

            try
            {
                string keepAlive = GetKeepAliveValue();

                bool ready =
                    await ollamaService.EnsureReadyAsync(keepAlive);

                if (!ready)
                {
                    isAssistantReady = false;

                    StatusText.Text =
                        "Başlatılamadı. Ollama ve qwen3:8b modelini kontrol et.";

                    MessageBox.Show(
                        "AE başlatılamadı.\n\n" +
                        "Şunları kontrol et:\n" +
                        "• Ollama bilgisayarında kurulu mu?\n" +
                        "• qwen3:8b modeli indirilmiş mi?\n" +
                        "• Ollama servisi çalışıyor mu?\n\n" +
                        "Ayrıntılar için Visual Studio Output penceresini kontrol et.",
                        "AE başlatılamadı",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }

                isAssistantReady = true;

                StatusText.Text = isDatabaseReady
                    ? $"AE hazır. Mod: {GetSelectedModeName()}."
                    : "AE hazır; veritabanı bağlantısı bekleniyor.";
            }
            catch (Exception ex)
            {
                isAssistantReady = false;

                Debug.WriteLine("AE başlatma hatası:");
                Debug.WriteLine(ex);

                StatusText.Text =
                    "Asistan başlatılırken hata oluştu.";

                MessageBox.Show(
                    "AE başlatılırken hata oluştu.\n\n" + ex.Message,
                    "Başlatma hatası",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                isStartingAssistant = false;

                UpdatePowerButtonVisual();
                UpdateChatControls();

                if (isAssistantReady && isDatabaseReady)
                    MessageInput.Focus();
            }
        }

        private async Task TurnAssistantOffAsync()
        {
            isStoppingAssistant = true;

            UpdatePowerButtonVisual();
            UpdateChatControls();

            StartAssistantButton.Content = "Kapatılıyor...";
            StatusText.Text = "Model belleği boşaltılıyor...";

            try
            {
                bool unloaded =
                    await ollamaService.UnloadModelAsync();

                if (!unloaded)
                {
                    isAssistantReady = true;

                    StatusText.Text =
                        "Modelin kapatılması doğrulanamadı.";

                    MessageBox.Show(
                        "Model belleği boşaltılamadı veya Ollama işlemi " +
                        "doğrulayamadı.\n\nAE açık durumda bırakıldı.",
                        "Kapatma doğrulanamadı",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }

                isAssistantReady = false;

                StatusText.Text =
                    "AE kapalı. Yeniden açmak için güç düğmesine bas.";

                AddMessage(
                    "Bilgi",
                    "AE kapatıldı. Sohbet geçmişin ve kayıtlı hafızaların korunuyor.",
                    false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Modeli kapatma hatası:");
                Debug.WriteLine(ex);

                isAssistantReady = true;

                StatusText.Text =
                    "Model kapatılamadı. Ollama bağlantısını kontrol et.";

                MessageBox.Show(
                    "Model kapatılamadı veya Ollama yanıt vermedi.\n\n" +
                    ex.Message,
                    "Kapatma hatası",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            finally
            {
                isStoppingAssistant = false;

                UpdatePowerButtonVisual();
                UpdateChatControls();
            }
        }

        private void UpdatePowerButtonVisual()
        {
            if (StartAssistantButton == null)
                return;

            StartAssistantButton.Content = "⏻";

            StartAssistantButton.Background = new SolidColorBrush(
                isAssistantReady
                    ? Color.FromRgb(34, 197, 94)
                    : Color.FromRgb(220, 38, 38));

            StartAssistantButton.Foreground = Brushes.White;

            StartAssistantButton.ToolTip = isAssistantReady
                ? "AE'yi kapat"
                : "AE'yi aç";

            StartAssistantButton.IsEnabled =
                !isStartingAssistant &&
                !isStoppingAssistant &&
                !isSending;
        }

        private void UpdateChatControls()
        {
            bool canChat =
                isDatabaseReady &&
                isAssistantReady &&
                !isSending &&
                !isStartingAssistant &&
                !isStoppingAssistant;

            MessageInput.IsEnabled = canChat;
            SendButton.IsEnabled = canChat;

            if (StartAssistantButton != null)
            {
                StartAssistantButton.IsEnabled =
                    !isStartingAssistant &&
                    !isStoppingAssistant &&
                    !isSending;
            }
        }

        // ---------------------------------------------------------
        // ÇALIŞMA MODU
        // ---------------------------------------------------------

        private string GetKeepAliveValue()
        {
            string? selectedMode =
                ModeSelector.SelectedValue as string;

            return selectedMode switch
            {
                "performance" => "-1m",
                "balanced" => "5m",
                _ => "5m"
            };
        }

        private string GetSelectedModeName()
        {
            string? selectedMode =
                ModeSelector.SelectedValue as string;

            return selectedMode switch
            {
                "performance" => "Performans",
                _ => "Dengeli"
            };
        }

        private void ModeSelector_SelectionChanged(
            object sender, SelectionChangedEventArgs e)
        {
            if (StatusText == null)
                return;

            if (isAssistantReady &&
                !isSending &&
                !isStartingAssistant &&
                !isStoppingAssistant)
            {
                StatusText.Text =
                    $"AE hazır. Seçilen mod: {GetSelectedModeName()}.";
            }
        }

        // ---------------------------------------------------------
        // VERİTABANI VE SOHBET GEÇMİŞİ
        // ---------------------------------------------------------

        private async Task InitializeChatAsync()
        {
            try
            {
                using var connection =
                    new SqlConnection(ConnectionString);

                await connection.OpenAsync();

                const string createTables = @"
                    IF OBJECT_ID(N'dbo.ChatMessages', N'U') IS NULL
                    BEGIN
                        CREATE TABLE dbo.ChatMessages
                        (
                            Id INT IDENTITY(1,1) PRIMARY KEY,
                            Role NVARCHAR(20) NOT NULL,
                            Content NVARCHAR(MAX) NOT NULL,
                            CreatedAt DATETIME2 NOT NULL
                                CONSTRAINT DF_ChatMessages_CreatedAt
                                DEFAULT SYSDATETIME()
                        );
                    END;

                    IF OBJECT_ID(N'dbo.AssistantMemories', N'U') IS NULL
                    BEGIN
                        CREATE TABLE dbo.AssistantMemories
                        (
                            Id INT IDENTITY(1,1) PRIMARY KEY,
                            Content NVARCHAR(1000) NOT NULL,
                            CreatedAt DATETIME2 NOT NULL
                                CONSTRAINT DF_AssistantMemories_CreatedAt
                                DEFAULT SYSDATETIME()
                        );
                    END;";

                using (var command =
                    new SqlCommand(createTables, connection))
                {
                    await command.ExecuteNonQueryAsync();
                }

                const string query = @"
                    SELECT Role, Content
                    FROM dbo.ChatMessages
                    ORDER BY Id";

                using var readCommand =
                    new SqlCommand(query, connection);

                using var reader =
                    await readCommand.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    string role = reader.GetString(0);
                    string content = reader.GetString(1);

                    if (role == "Sen" || role == "Asistan")
                        AddMessage(role, content, role == "Sen");
                }

                isDatabaseReady = true;

                if (Messages.Count == 0)
                {
                    AddMessage(
                        "Bilgi",
                        "Merhaba! Ben AE. Benimle sohbet edebilir, sorular " +
                        "sorabilir ve fikirlerini paylaşabilirsin. Başlamak " +
                        "için güç düğmesine basarak asistanı aç.",
                        false);
                }

                StatusText.Text = isAssistantReady
                    ? "AE hazır."
                    : "Veritabanı hazır. AE'yi açabilirsin.";
            }
            catch (Exception ex)
            {
                isDatabaseReady = false;

                Debug.WriteLine("Veritabanı başlatma hatası:");
                Debug.WriteLine(ex);

                StatusText.Text =
                    "Veritabanı bağlantısı başarısız.";

                MessageBox.Show(
                    "Veritabanına bağlanılamadı.\n\n" + ex.Message,
                    "Bağlantı hatası",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                UpdateChatControls();
                UpdatePowerButtonVisual();
            }
        }

        private async Task SaveMessageAsync(
            string role, string content)
        {
            using var connection =
                new SqlConnection(ConnectionString);

            await connection.OpenAsync();

            const string query = @"
                INSERT INTO dbo.ChatMessages (Role, Content)
                VALUES (@Role, @Content)";

            using var command =
                new SqlCommand(query, connection);

            command.Parameters.Add(
                "@Role",
                System.Data.SqlDbType.NVarChar,
                20).Value = role;

            command.Parameters.Add(
                "@Content",
                System.Data.SqlDbType.NVarChar,
                -1).Value = content;

            await command.ExecuteNonQueryAsync();
        }

        private void AddMessage(
            string role, string content, bool isUser)
        {
            Messages.Add(new ChatMessage
            {
                RoleLabel = role,
                Content = content,

                BubbleBackground = new SolidColorBrush(
                    isUser
                        ? Color.FromRgb(48, 61, 100)
                        : Color.FromRgb(42, 47, 63)),

                LabelForeground = new SolidColorBrush(
                    isUser
                        ? Color.FromRgb(190, 205, 255)
                        : Color.FromRgb(170, 190, 255)),

                TextForeground = Brushes.White
            });

            ChatScrollViewer.ScrollToEnd();
        }

        // ---------------------------------------------------------
        // HAFIZA İŞLEMLERİ
        // ---------------------------------------------------------

        private async Task<List<MemoryItem>> LoadMemoriesAsync()
        {
            var memories = new List<MemoryItem>();

            using var connection =
                new SqlConnection(ConnectionString);

            await connection.OpenAsync();

            const string query = @"
                SELECT Id, Content
                FROM dbo.AssistantMemories
                ORDER BY Id";

            using var command =
                new SqlCommand(query, connection);

            using var reader =
                await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                memories.Add(new MemoryItem
                {
                    Id = reader.GetInt32(0),
                    Content = reader.GetString(1)
                });
            }

            return memories;
        }

        private async Task AddMemoryAsync(string content)
        {
            content = content.Trim();

            if (content.Length == 0 || content.Length > 1000)
                throw new ArgumentException(
                    "Hafıza 1-1000 karakter arasında olmalıdır.");

            using var connection =
                new SqlConnection(ConnectionString);

            await connection.OpenAsync();

            const string query = @"
                INSERT INTO dbo.AssistantMemories (Content)
                VALUES (@Content)";

            using var command =
                new SqlCommand(query, connection);

            command.Parameters.Add(
                "@Content",
                System.Data.SqlDbType.NVarChar,
                1000).Value = content;

            await command.ExecuteNonQueryAsync();
        }

        private async Task UpdateMemoryAsync(
            int id, string content)
        {
            content = content.Trim();

            if (content.Length == 0 || content.Length > 1000)
                throw new ArgumentException(
                    "Hafıza 1-1000 karakter arasında olmalıdır.");

            using var connection =
                new SqlConnection(ConnectionString);

            await connection.OpenAsync();

            const string query = @"
                UPDATE dbo.AssistantMemories
                SET Content = @Content
                WHERE Id = @Id";

            using var command =
                new SqlCommand(query, connection);

            command.Parameters.Add(
                "@Id",
                System.Data.SqlDbType.Int).Value = id;

            command.Parameters.Add(
                "@Content",
                System.Data.SqlDbType.NVarChar,
                1000).Value = content;

            await command.ExecuteNonQueryAsync();
        }

        private async Task DeleteMemoryAsync(int id)
        {
            using var connection =
                new SqlConnection(ConnectionString);

            await connection.OpenAsync();

            const string query = @"
                DELETE FROM dbo.AssistantMemories
                WHERE Id = @Id";

            using var command =
                new SqlCommand(query, connection);

            command.Parameters.Add(
                "@Id",
                System.Data.SqlDbType.Int).Value = id;

            await command.ExecuteNonQueryAsync();
        }

        private async void ManageMemories_Click(
            object sender, RoutedEventArgs e)
        {
            if (!isDatabaseReady ||
                isSending ||
                isStartingAssistant ||
                isStoppingAssistant)
            {
                return;
            }

            try
            {
                var window = new MemoryManagerWindow(
                    LoadMemoriesAsync,
                    AddMemoryAsync,
                    UpdateMemoryAsync,
                    DeleteMemoryAsync)
                {
                    Owner = this
                };

                window.ShowDialog();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);

                MessageBox.Show(
                    "Hafıza penceresi açılamadı.\n" + ex.Message,
                    "Hata",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // ---------------------------------------------------------
        // SOHBET GEÇMİŞİNİ TEMİZLE
        // ---------------------------------------------------------

        private async void ClearChat_Click(
            object sender, RoutedEventArgs e)
        {
            if (!isDatabaseReady ||
                isSending ||
                isStartingAssistant ||
                isStoppingAssistant)
            {
                return;
            }

            var result = MessageBox.Show(
                "Bütün sohbet mesajları kalıcı olarak silinecek.\n" +
                "Kaydettiğin hafızalar korunacak.\n\n" +
                "Devam etmek istiyor musun?",
                "Sohbet geçmişini temizle",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                using var connection =
                    new SqlConnection(ConnectionString);

                await connection.OpenAsync();

                using var transaction =
                    connection.BeginTransaction();

                try
                {
                    using var command = new SqlCommand(
                        "DELETE FROM dbo.ChatMessages",
                        connection,
                        transaction);

                    await command.ExecuteNonQueryAsync();

                    transaction.Commit();
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }

                Messages.Clear();

                AddMessage(
                    "Bilgi",
                    "Sohbet geçmişi temizlendi. Kayıtlı hafızaların korunuyor.",
                    false);

                StatusText.Text = isAssistantReady
                    ? "Sohbet geçmişi temizlendi."
                    : "Geçmiş temizlendi. AE'yi açabilirsin.";
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    "Sohbet geçmişini temizleme hatası:");

                Debug.WriteLine(ex);

                MessageBox.Show(
                    "Sohbet geçmişi temizlenemedi.\n\n" + ex.Message,
                    "Hata",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // ---------------------------------------------------------
        // MESAJ GÖNDERME
        // ---------------------------------------------------------

        private async void SendButton_Click(
            object sender, RoutedEventArgs e)
        {
            await SendMessageAsync();
        }

        // Enter mesaj gönderir, Shift + Enter yeni satır açar.
        private async void MessageInput_PreviewKeyDown(
            object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Return)
                return;

            if ((Keyboard.Modifiers & ModifierKeys.Shift) ==
                ModifierKeys.Shift)
            {
                return;
            }

            e.Handled = true;

            await SendMessageAsync();
        }

        private async Task SendMessageAsync()
        {
            if (isSending ||
                !isDatabaseReady ||
                !isAssistantReady ||
                isStartingAssistant ||
                isStoppingAssistant)
            {
                return;
            }

            string message = MessageInput.Text.Trim();

            if (string.IsNullOrWhiteSpace(message))
                return;

            if (message.Length > MaxMessageLength)
            {
                StatusText.Text =
                    $"Mesaj en fazla {MaxMessageLength} karakter olabilir.";

                return;
            }

            isSending = true;

            UpdateChatControls();
            UpdatePowerButtonVisual();

            SendButton.Content = "Cevap hazırlanıyor...";

            try
            {
                await SaveMessageAsync("Sen", message);

                AddMessage("Sen", message, true);
                MessageInput.Clear();

                // Her iki çalışma modu da string kullanır.
                string keepAlive = GetKeepAliveValue();

                var systemPrompt = new StringBuilder();

                systemPrompt.Append(
                    "Sen AE adlı kişisel asistansın. ");

                systemPrompt.Append(
                    "Türkçe, samimi, doğal ve gündelik konuş. ");

                systemPrompt.Append(
                    "Yazım hatalarını ve argo ifadeleri bağlama göre anla. ");

                systemPrompt.Append(
                    "Hazır kalıp cevaplar vermek yerine konuşmanın bağlamını değerlendir. ");

                systemPrompt.Append(
                    "Kullanıcı sormadıkça geçmiş konuları kendiliğinden açma. ");

                systemPrompt.Append(
                    "Bilmediğin bilgileri uydurma; emin olmadığında bunu belirt. ");

                systemPrompt.Append(
                    "Hafızaya yeni bilgi kaydettiğini iddia etme. " +
                    "Kalıcı hafıza yalnızca uygulamanın hafıza yönetiminden eklenebilir. ");

                systemPrompt.Append(
                    "Kendinden söz ederken AE adını kullan.");

                List<MemoryItem> memories =
                    await LoadMemoriesAsync();

                if (memories.Count > 0)
                {
                    systemPrompt.AppendLine();

                    systemPrompt.AppendLine(
                        "KULLANICININ ELLE KAYDETTİĞİ BİLGİLER:");

                    foreach (MemoryItem memory in memories)
                    {
                        systemPrompt.Append("- ");
                        systemPrompt.AppendLine(memory.Content);
                    }

                    systemPrompt.AppendLine(
                        "Bu bilgileri yalnızca ilgili olduklarında kullan. " +
                        "Kayıtlı bilgileri kullanıcı mesajı gibi değerlendirme.");
                }

                var apiMessages = new List<object>
                {
                    new
                    {
                        role = "system",
                        content = systemPrompt.ToString()
                    }
                };

                List<ChatMessage> history = Messages
                    .Where(m =>
                        m.RoleLabel == "Sen" ||
                        m.RoleLabel == "Asistan")
                    .ToList();

                history = history
                    .Skip(Math.Max(
                        0,
                        history.Count - MaxHistoryMessages))
                    .ToList();

                foreach (ChatMessage item in history)
                {
                    apiMessages.Add(new
                    {
                        role = item.RoleLabel == "Sen"
                            ? "user"
                            : "assistant",

                        content = item.Content
                    });
                }

                StatusText.Text =
                    "Ollama ve model kontrol ediliyor...";

                bool ollamaReady =
                    await ollamaService
                        .EnsureServerAndModelAvailableAsync();

                if (!ollamaReady)
                {
                    isAssistantReady = false;

                    ShowSafeError(
                        "Ollama başlatılamadı veya qwen3:8b modeli kurulu değil. " +
                        "Ollama kurulumunu ve model adını kontrol et.");

                    return;
                }

                string json = JsonSerializer.Serialize(new
                {
                    model = ModelName,
                    messages = apiMessages,
                    stream = false,
                    keep_alive = keepAlive
                });

                using var requestContent = new StringContent(
                    json,
                    Encoding.UTF8,
                    "application/json");

                StatusText.Text =
                    $"AE cevap hazırlıyor... ({GetSelectedModeName()} modu)";

                using var response = await client.PostAsync(
                    $"{OllamaUrl}/api/chat",
                    requestContent);

                string result =
                    await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine(
                        $"Ollama HTTP hatası: {(int)response.StatusCode} " +
                        $"{response.ReasonPhrase}");

                    Debug.WriteLine("Ollama yanıtı:");
                    Debug.WriteLine(result);

                    throw new HttpRequestException(
                        $"Ollama HTTP {(int)response.StatusCode}: " +
                        $"{response.ReasonPhrase}. Ayrıntı: {result}");
                }

                using var document =
                    JsonDocument.Parse(result);

                if (!document.RootElement.TryGetProperty(
                        "message",
                        out JsonElement messageElement) ||
                    !messageElement.TryGetProperty(
                        "content",
                        out JsonElement contentElement) ||
                    contentElement.ValueKind != JsonValueKind.String)
                {
                    throw new InvalidOperationException(
                        "Ollama geçerli bir sohbet yanıtı döndürmedi.");
                }

                string answer =
                    contentElement.GetString()?.Trim() ?? "";

                if (string.IsNullOrWhiteSpace(answer))
                {
                    throw new InvalidOperationException(
                        "Yapay zekâ boş yanıt verdi.");
                }

                await SaveMessageAsync("Asistan", answer);

                AddMessage("Asistan", answer, false);

                StatusText.Text =
                    $"AE hazır. Mod: {GetSelectedModeName()}.";
            }
            catch (HttpRequestException ex)
            {
                Debug.WriteLine("Ollama HTTP/bağlantı hatası:");
                Debug.WriteLine(ex);

                ShowSafeError(
                    "Ollama isteği başarısız oldu. Ollama'nın çalıştığını, " +
                    "qwen3:8b modelinin kurulu olduğunu kontrol et.\n\n" +
                    ex.Message);
            }
            catch (TaskCanceledException ex)
            {
                Debug.WriteLine("Ollama zaman aşımı:");
                Debug.WriteLine(ex);

                ShowSafeError(
                    "İstek 10 dakika içinde tamamlanamadı. Ollama'nın " +
                    "çalıştığını kontrol edip tekrar dene.");
            }
            catch (SqlException ex)
            {
                Debug.WriteLine("SQL Server hatası:");
                Debug.WriteLine(ex);

                ShowSafeError(
                    "Veritabanı işlemi başarısız oldu. SQL Server bağlantısını " +
                    "kontrol et.\n\n" + ex.Message);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Mesaj gönderme hatası:");
                Debug.WriteLine(ex);

                ShowSafeError(
                    "Bir hata oluştu.\n\n" + ex.Message);
            }
            finally
            {
                isSending = false;

                UpdateChatControls();
                UpdatePowerButtonVisual();

                SendButton.Content = "Gönder";

                if (isDatabaseReady && isAssistantReady)
                    MessageInput.Focus();
            }
        }

        private void ShowSafeError(string message)
        {
            AddMessage("Bilgi", message, false);

            StatusText.Text = "İşlem tamamlanamadı";
        }

        // ---------------------------------------------------------
        // MESAJI PANoya KOPYALA
        // ---------------------------------------------------------

        private void CopyMessage_Click(
            object sender, RoutedEventArgs e)
        {
            if (sender is not Button button ||
                button.DataContext is not ChatMessage message)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(message.Content))
            {
                StatusText.Text = "Kopyalanacak mesaj boş.";
                return;
            }

            try
            {
                Clipboard.SetText(message.Content);
                StatusText.Text = "Mesaj panoya kopyalandı.";
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Mesaj kopyalama hatası:");
                Debug.WriteLine(ex);

                StatusText.Text = "Mesaj panoya kopyalanamadı.";
            }
        }
    }

    // -------------------------------------------------------------
    // SOHBET MESAJI MODELİ
    // -------------------------------------------------------------

    public class ChatMessage
    {
        public string RoleLabel { get; set; } = "";
        public string Content { get; set; } = "";

        public Brush BubbleBackground { get; set; }
            = Brushes.Transparent;

        public Brush LabelForeground { get; set; }
            = Brushes.White;

        public Brush TextForeground { get; set; }
            = Brushes.White;
    }

    // -------------------------------------------------------------
    // HAFIZA MODELİ
    // -------------------------------------------------------------

    public class MemoryItem
    {
        public int Id { get; set; }
        public string Content { get; set; } = "";
    }

    // -------------------------------------------------------------
    // HAFIZA YÖNETİM PENCERESİ
    // -------------------------------------------------------------

    public class MemoryManagerWindow : Window
    {
        private readonly Func<Task<List<MemoryItem>>> loadMemories;
        private readonly Func<string, Task> addMemory;
        private readonly Func<int, string, Task> updateMemory;
        private readonly Func<int, Task> deleteMemory;

        private readonly ListBox memoryList;
        private readonly TextBox memoryInput;
        private readonly TextBlock status;

        private List<MemoryItem> items = new List<MemoryItem>();

        public MemoryManagerWindow(
            Func<Task<List<MemoryItem>>> load,
            Func<string, Task> add,
            Func<int, string, Task> update,
            Func<int, Task> delete)
        {
            loadMemories = load;
            addMemory = add;
            updateMemory = update;
            deleteMemory = delete;

            Title = "AE - Hafızayı Yönet";
            Width = 620;
            Height = 500;
            MinWidth = 480;
            MinHeight = 380;

            WindowStartupLocation =
                WindowStartupLocation.CenterOwner;

            Background = new SolidColorBrush(
                Color.FromRgb(23, 25, 35));

            var root = new Grid
            {
                Margin = new Thickness(16)
            };

            root.RowDefinitions.Add(
                new RowDefinition
                {
                    Height = GridLength.Auto
                });

            root.RowDefinitions.Add(
                new RowDefinition
                {
                    Height = new GridLength(2, GridUnitType.Star)
                });

            root.RowDefinitions.Add(
                new RowDefinition
                {
                    Height = GridLength.Auto
                });

            root.RowDefinitions.Add(
                new RowDefinition
                {
                    Height = GridLength.Auto
                });

            root.RowDefinitions.Add(
                new RowDefinition
                {
                    Height = GridLength.Auto
                });

            var heading = new TextBlock
            {
                Text = "AE'nin hatırlamasını istediğin bilgiler",
                Foreground = Brushes.White,
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 12)
            };

            root.Children.Add(heading);

            memoryList = new ListBox
            {
                Background = new SolidColorBrush(
                    Color.FromRgb(32, 36, 50)),

                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(0, 0, 0, 12),
                DisplayMemberPath = "Content"
            };

            Grid.SetRow(memoryList, 1);
            root.Children.Add(memoryList);

            memoryInput = new TextBox
            {
                Height = 72,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,

                VerticalScrollBarVisibility =
                    ScrollBarVisibility.Auto,

                MaxLength = 1000,
                Padding = new Thickness(10),

                Background = new SolidColorBrush(
                    Color.FromRgb(36, 40, 55)),

                Foreground = Brushes.White,

                BorderBrush = new SolidColorBrush(
                    Color.FromRgb(81, 90, 115)),

                Margin = new Thickness(0, 0, 0, 10),

                ToolTip =
                    "Örnek: C# öğreniyorum ve kısa, adım adım açıklamaları tercih ediyorum."
            };

            Grid.SetRow(memoryInput, 2);
            root.Children.Add(memoryInput);

            var buttons = new WrapPanel
            {
                Margin = new Thickness(0, 0, 0, 8)
            };

            AddButton(buttons, "Ekle", AddSelectedAsync);
            AddButton(buttons, "Seçileni düzenle", EditSelectedAsync);
            AddButton(buttons, "Seçileni sil", DeleteSelectedAsync);

            Grid.SetRow(buttons, 3);
            root.Children.Add(buttons);

            status = new TextBlock
            {
                Text = "Bilgiler yükleniyor...",

                Foreground = new SolidColorBrush(
                    Color.FromRgb(197, 203, 224)),

                Margin = new Thickness(0, 4, 0, 0)
            };

            Grid.SetRow(status, 4);
            root.Children.Add(status);

            Content = root;

            Loaded += async (_, _) => await RefreshAsync();

            memoryList.SelectionChanged += (_, _) =>
            {
                if (memoryList.SelectedItem is MemoryItem selected)
                    memoryInput.Text = selected.Content;
            };
        }

        private void AddButton(
            Panel panel,
            string text,
            Func<Task> action)
        {
            var button = new Button
            {
                Content = text,

                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 0, 8, 0),

                Background = new SolidColorBrush(
                    Color.FromRgb(79, 70, 229)),

                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };

            button.Click += async (_, _) =>
            {
                try
                {
                    button.IsEnabled = false;

                    await action();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Hafıza işlemi hatası:");
                    Debug.WriteLine(ex);

                    MessageBox.Show(
                        "Hafıza işlemi başarısız oldu.\n" + ex.Message,
                        "Hata",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
                finally
                {
                    button.IsEnabled = true;
                }
            };

            panel.Children.Add(button);
        }

        private async Task RefreshAsync()
        {
            items = await loadMemories();

            memoryList.ItemsSource = null;
            memoryList.ItemsSource = items;

            status.Text =
                $"Kayıtlı hafıza sayısı: {items.Count}";
        }

        private async Task AddSelectedAsync()
        {
            string content = memoryInput.Text.Trim();

            if (content.Length == 0)
            {
                status.Text = "Önce kaydedilecek bilgiyi yaz.";
                return;
            }

            await addMemory(content);

            memoryInput.Clear();

            await RefreshAsync();

            status.Text = "Hafıza kaydedildi.";
        }

        private async Task EditSelectedAsync()
        {
            if (memoryList.SelectedItem is not MemoryItem selected)
            {
                status.Text = "Önce düzenlenecek hafızayı seç.";
                return;
            }

            string content = memoryInput.Text.Trim();

            if (content.Length == 0)
            {
                status.Text = "Hafıza boş bırakılamaz.";
                return;
            }

            await updateMemory(selected.Id, content);

            await RefreshAsync();

            status.Text = "Hafıza güncellendi.";
        }

        private async Task DeleteSelectedAsync()
        {
            if (memoryList.SelectedItem is not MemoryItem selected)
            {
                status.Text = "Önce silinecek hafızayı seç.";
                return;
            }

            var result = MessageBox.Show(
                "Seçilen hafızayı kalıcı olarak silmek istiyor musun?\n\n" +
                selected.Content,
                "Hafızayı sil",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            await deleteMemory(selected.Id);

            memoryInput.Clear();

            await RefreshAsync();

            status.Text = "Hafıza silindi.";
        }
    }
}