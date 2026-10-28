
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace WpfApp1
{
    public class OllamaService
    {
        private const string OllamaUrl = "http://localhost:11434";
        private const string ModelName = "qwen3:8b";

        private static readonly HttpClient HttpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(10)
        };

        private Process? ollamaProcess;
        private readonly SemaphoreSlim startLock = new SemaphoreSlim(1, 1);

        // Ollama sunucusunun erişilebilir olup olmadığını kontrol eder.
        public async Task<bool> IsRunningAsync(
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var response = await HttpClient.GetAsync(
                    $"{OllamaUrl}/api/tags",
                    cancellationToken);

                return response.IsSuccessStatusCode;
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ollama bağlantı kontrolü başarısız: {ex}");
                return false;
            }
        }

        // ollama.exe dosyasının olası konumlarını arar.
        private string? FindOllamaExecutable()
        {
            string? path = Environment.GetEnvironmentVariable("PATH");

            if (!string.IsNullOrWhiteSpace(path))
            {
                foreach (string folder in path.Split(
                    Path.PathSeparator,
                    StringSplitOptions.RemoveEmptyEntries))
                {
                    try
                    {
                        string candidate = Path.Combine(
                            folder.Trim().Trim('"'),
                            "ollama.exe");

                        if (File.Exists(candidate))
                            return candidate;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(
                            $"Ollama yolu kontrol edilemedi: {ex.Message}");
                    }
                }
            }

            string localAppData = Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);

            string programFiles = Environment.GetFolderPath(
                Environment.SpecialFolder.ProgramFiles);

            string programFilesX86 = Environment.GetFolderPath(
                Environment.SpecialFolder.ProgramFilesX86);

            string[] candidates =
            {
                Path.Combine(
                    localAppData, "Programs", "Ollama", "ollama.exe"),

                Path.Combine(
                    programFiles, "Ollama", "ollama.exe"),

                Path.Combine(
                    programFilesX86, "Ollama", "ollama.exe")
            };

            foreach (string candidate in candidates)
            {
                if (File.Exists(candidate))
                    return candidate;
            }

            return null;
        }

        // Sunucu çalışmıyorsa Ollama'yı başlatır.
        public async Task<bool> StartOllamaAsync(
            CancellationToken cancellationToken = default)
        {
            await startLock.WaitAsync(cancellationToken);

            try
            {
                // Başka bir uygulama veya Ollama servisi zaten çalışıyor olabilir.
                if (await IsRunningAsync(cancellationToken))
                    return true;

                cancellationToken.ThrowIfCancellationRequested();

                string? executable = FindOllamaExecutable();

                if (executable == null)
                {
                    Debug.WriteLine(
                        "ollama.exe bulunamadı. Ollama kurulumunu kontrol et.");

                    return false;
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = executable,
                    Arguments = "serve",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                var process = new Process
                {
                    StartInfo = startInfo,
                    EnableRaisingEvents = true
                };

                if (!process.Start())
                {
                    process.Dispose();

                    return await IsRunningAsync(cancellationToken);
                }

                ollamaProcess = process;

                // Çıktı tamponlarının dolup süreci durdurmasını önler.
                _ = process.StandardOutput.ReadToEndAsync();
                _ = process.StandardError.ReadToEndAsync();

                // Sunucunun başlaması için en fazla yaklaşık 20 saniye bekle.
                for (int i = 0; i < 40; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (await IsRunningAsync(cancellationToken))
                        return true;

                    if (process.HasExited)
                    {
                        Debug.WriteLine(
                            $"Ollama işlemi sonlandı. Çıkış kodu: {process.ExitCode}");

                        break;
                    }

                    await Task.Delay(500, cancellationToken);
                }

                return await IsRunningAsync(cancellationToken);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ollama başlatma hatası: {ex}");

                return await IsRunningAsync(cancellationToken);
            }
            finally
            {
                startLock.Release();
            }
        }

        // qwen3:8b modelinin bilgisayarda kurulu olup olmadığını kontrol eder.
        public async Task<bool> IsModelAvailableAsync(
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var response = await HttpClient.GetAsync(
                    $"{OllamaUrl}/api/tags",
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                    return false;

                string json = await response.Content.ReadAsStringAsync(
                    cancellationToken);

                using var document = JsonDocument.Parse(json);

                if (!document.RootElement.TryGetProperty(
                    "models", out JsonElement models) ||
                    models.ValueKind != JsonValueKind.Array)
                {
                    return false;
                }

                foreach (JsonElement model in models.EnumerateArray())
                {
                    if (!model.TryGetProperty(
                        "name", out JsonElement name))
                    {
                        continue;
                    }

                    string? installedName = name.GetString();

                    if (string.Equals(
                        installedName,
                        ModelName,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Model kontrolü başarısız: {ex}");
                return false;
            }
        }

        // true  = model bellekte yüklü
        // false = model bellekte yüklü değil
        // null  = durum kontrol edilemedi
        private async Task<bool?> CheckModelLoadedAsync(
            CancellationToken cancellationToken)
        {
            try
            {
                using var response = await HttpClient.GetAsync(
                    $"{OllamaUrl}/api/ps",
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                    return null;

                string json = await response.Content.ReadAsStringAsync(
                    cancellationToken);

                using var document = JsonDocument.Parse(json);

                if (!document.RootElement.TryGetProperty(
                    "models", out JsonElement models) ||
                    models.ValueKind != JsonValueKind.Array)
                {
                    return null;
                }

                foreach (JsonElement model in models.EnumerateArray())
                {
                    if (!model.TryGetProperty(
                        "name", out JsonElement name))
                    {
                        continue;
                    }

                    string? loadedName = name.GetString();

                    if (string.Equals(
                        loadedName,
                        ModelName,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Yüklü model kontrolü başarısız: {ex}");
                return null;
            }
        }

        // Modelin bellekte yüklü olup olmadığını kontrol eder.
        public async Task<bool> IsModelLoadedAsync(
            CancellationToken cancellationToken = default)
        {
            return await CheckModelLoadedAsync(cancellationToken) == true;
        }

        // Modeli belirtilen keep_alive süresiyle belleğe yükler.
        public async Task<bool> LoadModelAsync(
            string keepAlive = "5m",
            CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (string.IsNullOrWhiteSpace(keepAlive))
                    keepAlive = "5m";

                string json = JsonSerializer.Serialize(new
                {
                    model = ModelName,
                    prompt = "",
                    stream = false,
                    keep_alive = keepAlive
                });

                using var content = new StringContent(
                    json,
                    Encoding.UTF8,
                    "application/json");

                using var response = await HttpClient.PostAsync(
                    $"{OllamaUrl}/api/generate",
                    content,
                    cancellationToken);

                string body = await response.Content.ReadAsStringAsync(
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine(
                        $"Model yükleme HTTP {(int)response.StatusCode}: {body}");

                    return false;
                }

                return true;
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Model yükleme hatası: {ex}");
                return false;
            }
        }

        // Modeli Ollama belleğinden çıkarmayı dener.
        public async Task<bool> UnloadModelAsync(
            CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                string json = JsonSerializer.Serialize(new
                {
                    model = ModelName,
                    prompt = "",
                    stream = false,
                    keep_alive = 0
                });

                using var content = new StringContent(
                    json,
                    Encoding.UTF8,
                    "application/json");

                using var response = await HttpClient.PostAsync(
                    $"{OllamaUrl}/api/generate",
                    content,
                    cancellationToken);

                string body = await response.Content.ReadAsStringAsync(
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine(
                        $"Model boşaltma HTTP {(int)response.StatusCode}: {body}");

                    return false;
                }

                // Modelin /api/ps listesinden çıkmasını doğrula.
                for (int i = 0; i < 10; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    bool? loaded = await CheckModelLoadedAsync(
                        cancellationToken);

                    if (loaded == false)
                        return true;

                    // Kontrol yapılamıyorsa başarı varsayma.
                    if (loaded == null)
                        return false;

                    await Task.Delay(300, cancellationToken);
                }

                return await CheckModelLoadedAsync(cancellationToken) == false;
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Model bellekten çıkarma hatası: {ex}");
                return false;
            }
        }

        // Sunucuyu başlatır ve modelin kurulu olduğunu doğrular.
        // Modeli belleğe yüklemez.
        public async Task<bool> EnsureServerAndModelAvailableAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!await StartOllamaAsync(cancellationToken))
                return false;

            cancellationToken.ThrowIfCancellationRequested();

            return await IsModelAvailableAsync(cancellationToken);
        }

        // Varsayılan olarak modeli 5 dakika bellekte tutacak şekilde hazırlar.
        public async Task<bool> EnsureReadyAsync(
            CancellationToken cancellationToken = default)
        {
            return await EnsureReadyAsync("5m", cancellationToken);
        }

        // Sunucuyu başlatır, modeli kontrol eder ve belleğe yükler.
        public async Task<bool> EnsureReadyAsync(
            string keepAlive,
            CancellationToken cancellationToken = default)
        {
            if (!await EnsureServerAndModelAvailableAsync(cancellationToken))
                return false;

            return await LoadModelAsync(keepAlive, cancellationToken);
        }

        // Yalnızca bu servis tarafından başlatılan süreci kapatır.
        // Önceden çalışan Ollama sürecini kapatmaz.
        public void StopOwnedProcess()
        {
            Process? process = ollamaProcess;

            if (process == null)
                return;

            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(3000);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ollama süreci kapatılamadı: {ex}");
            }
            finally
            {
                process.Dispose();
                ollamaProcess = null;
            }
        }
    }
}