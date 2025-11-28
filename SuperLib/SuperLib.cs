using Microsoft.Extensions.DependencyInjection; // Required for GetRequiredService
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using UltimateServer.Plugins;
using UltimateServer.Services;

public class SuperLib : IPlugin
{
    public string Name => "SuperLib";
    public string Version => "1.1.0";

    private IPluginContext _context;
    private AuthenticationService _authService; // Cache the auth service

    // Static fields to track CPU usage between requests
    private static DateTime _lastCpuCheck = DateTime.UtcNow;
    private static TimeSpan _lastCpuTime = Process.GetCurrentProcess().TotalProcessorTime;
    private static Config config;

    public async Task OnLoadAsync(IPluginContext context)
    {
        var pluginFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins", Name);
        var pluginConfig = Path.Combine(pluginFolder, "config.json");

        if (!Directory.Exists(pluginFolder))
            Directory.CreateDirectory(pluginFolder);

        if (!File.Exists(pluginConfig))
        {
            await File.WriteAllTextAsync(pluginConfig, JsonConvert.SerializeObject(new Config(), Formatting.Indented));
            config = new Config();
        }
        else
        {
            var data = await File.ReadAllTextAsync(pluginConfig);
            var json = JsonConvert.DeserializeObject<Config>(data);
            config = json != null ? json : new Config();
        }

        _context = context;

        // Get the authentication service from the server's dependency injection container
        _authService = _context.ServiceProvider.GetRequiredService<AuthenticationService>();

        // Create a wrapper function that adds authentication to a handler
        Func<HttpListenerRequest, Task> WithAuthentication(Func<HttpListenerRequest, Task> innerHandler)
        {
            return async (request) =>
            {
                if (IsAuthenticated(request))
                {
                    await innerHandler(request);
                }
                else
                {
                    await SendUnauthorizedResponseAsync();
                }
            };
        }
        ;

        // Register the public ping route (no authentication required)
        context.RegisterApiRoute("/api/health", HandlePingAsync);

        // Register all other routes, wrapped with the authentication check
        context.RegisterApiRoute("/api/system/info", WithAuthentication(HandleSystemInfoAsync));
        context.RegisterApiRoute("/api/system/performance", WithAuthentication(HandlePerformanceAsync));
        context.RegisterApiRoute("/api/network/info", WithAuthentication(HandleNetworkInfoAsync));
    }

    /// <summary>
    /// Checks if the request contains a valid JWT token.
    /// This replicates the server's own authentication logic.
    /// </summary>
    private bool IsAuthenticated(HttpListenerRequest request)
    {
        string authHeader = request.Headers["Authorization"];
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
        {
            return false;
        }

        string token = authHeader.Substring("Bearer ".Length);
        return _authService.ValidateJwtToken(token);
    }

    /// <summary>
    /// Sends a 401 Unauthorized response.
    /// </summary>
    private async Task SendUnauthorizedResponseAsync()
    {
        var response = HttpContextHolder.CurrentResponse;
        if (response != null)
        {
            response.StatusCode = 401; // Unauthorized
            response.AddHeader("WWW-Authenticate", "Bearer");
            response.ContentType = "application/json";

            var errorPayload = new { success = false, message = "Authentication is required to access this resource." };
            string errorJson = JsonConvert.SerializeObject(errorPayload);
            byte[] errorBuffer = Encoding.UTF8.GetBytes(errorJson);

            response.ContentLength64 = errorBuffer.Length;
            await response.OutputStream.WriteAsync(errorBuffer, 0, errorBuffer.Length);
            response.OutputStream.Close();
        }
    }

    // --- All handler methods below this line are now protected by authentication ---

    /// <summary>
    /// Handles a simple ping/health check request (NO AUTH REQUIRED).
    /// </summary>
    private async Task HandlePingAsync(HttpListenerRequest request)
    {
        var response = HttpContextHolder.CurrentResponse;
        try
        {
            response.StatusCode = (int)HttpStatusCode.OK;
            response.ContentType = "application/json";

            var pingData = new { status = "OK", timestamp = DateTime.UtcNow };
            string jsonResponse = JsonConvert.SerializeObject(pingData);
            byte[] buffer = Encoding.UTF8.GetBytes(jsonResponse);

            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
        }
        catch (Exception ex)
        {
            await SendErrorResponseAsync(response, ex);
        }
        finally
        {
            response?.OutputStream.Close();
        }
    }

    /// <summary>
    /// Handles a request for basic system and process information.
    /// </summary>
    private async Task HandleSystemInfoAsync(HttpListenerRequest request)
    {
        var response = HttpContextHolder.CurrentResponse;
        try
        {
            var process = Process.GetCurrentProcess();
            response.StatusCode = (int)HttpStatusCode.OK;
            response.ContentType = "application/json";

            var systemInfo = new
            {
                osVersion = Environment.OSVersion.ToString(),
                machineName = Environment.MachineName,
                processorCount = Environment.ProcessorCount,
                process = new
                {
                    id = process.Id,
                    startTime = process.StartTime,
                    threadCount = process.Threads.Count,
                    handleCount = process.HandleCount
                }
            };

            string jsonResponse = JsonConvert.SerializeObject(systemInfo, Formatting.Indented);
            byte[] buffer = Encoding.UTF8.GetBytes(jsonResponse);

            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
        }
        catch (Exception ex)
        {
            await SendErrorResponseAsync(response, ex);
        }
        finally
        {
            response?.OutputStream.Close();
        }
    }

    /// <summary>
    /// Handles a request for performance metrics like CPU, Memory, and Disk.
    /// </summary>
    private async Task HandlePerformanceAsync(HttpListenerRequest request)
    {
        var response = HttpContextHolder.CurrentResponse;
        try
        {
            response.StatusCode = (int)HttpStatusCode.OK;
            response.ContentType = "application/json";

            var currentProcess = Process.GetCurrentProcess();
            var currentTime = DateTime.UtcNow;
            var currentCpuTime = currentProcess.TotalProcessorTime;
            double cpuUsageMs = (currentCpuTime - _lastCpuTime).TotalMilliseconds;
            double elapsedMs = (currentTime - _lastCpuCheck).TotalMilliseconds;
            double cpuUsage = Math.Round(cpuUsageMs / (elapsedMs * Environment.ProcessorCount) * 100, 2);

            _lastCpuCheck = currentTime;
            _lastCpuTime = currentCpuTime;

            long memoryUsed = currentProcess.WorkingSet64 / 1024 / 1024;
            long managedMemory = GC.GetTotalMemory(false) / 1024 / 1024;

            var drives = DriveInfo.GetDrives()
                .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
                .Select(d => new
                {
                    name = d.Name,
                    label = d.VolumeLabel,
                    totalSizeGB = Math.Round(d.TotalSize / 1024.0 / 1024.0 / 1024.0, 2),
                    freeSpaceGB = Math.Round(d.AvailableFreeSpace / 1024.0 / 1024.0 / 1024.0, 2),
                    usedPercentage = Math.Round((double)(d.TotalSize - d.AvailableFreeSpace) / d.TotalSize * 100, 2)
                }).ToList();

            var performanceData = new
            {
                cpu = new { usagePercentage = cpuUsage },
                memory = new { workingSetMB = memoryUsed, managedMemoryMB = managedMemory },
                disk = drives
            };

            string jsonResponse = JsonConvert.SerializeObject(performanceData, Formatting.Indented);
            byte[] buffer = Encoding.UTF8.GetBytes(jsonResponse);

            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
        }
        catch (Exception ex)
        {
            await SendErrorResponseAsync(response, ex);
        }
        finally
        {
            response?.OutputStream.Close();
        }
    }

    /// <summary>
    /// Handles a request for network interface and connection information.
    /// </summary>
    private async Task HandleNetworkInfoAsync(HttpListenerRequest request)
    {
        var response = HttpContextHolder.CurrentResponse;
        try
        {
            response.StatusCode = (int)HttpStatusCode.OK;
            response.ContentType = "application/json";

            var properties = IPGlobalProperties.GetIPGlobalProperties();
            var activeConnections = properties.GetActiveTcpConnections();

            var networkInfo = new
            {
                activeTcpConnections = activeConnections.Count(),
                interfaces = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(ni => ni.OperationalStatus == OperationalStatus.Up)
                    .Select(ni => new
                    {
                        name = ni.Name,
                        type = ni.NetworkInterfaceType.ToString(),
                        speed = ni.Speed / 1000 / 1000,
                        status = ni.OperationalStatus.ToString(),
                        ipAddresses = ni.GetIPProperties().UnicastAddresses
                            .Where(ip => ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                            .Select(ip => ip.Address.ToString())
                            .ToList()
                    }).ToList()
            };

            string jsonResponse = JsonConvert.SerializeObject(networkInfo, Formatting.Indented);
            byte[] buffer = Encoding.UTF8.GetBytes(jsonResponse);

            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
        }
        catch (Exception ex)
        {
            await SendErrorResponseAsync(response, ex);
        }
        finally
        {
            response?.OutputStream.Close();
        }
    }

    /// <summary>
    /// A helper method to send a standardized JSON error response.
    /// </summary>
    private async Task SendErrorResponseAsync(HttpListenerResponse response, Exception ex)
    {
        _context.Logger.LogError($"An error occurred in the {Name} plugin: {ex.Message}");
        if (response != null)
        {
            response.StatusCode = (int)HttpStatusCode.InternalServerError;
            response.ContentType = "application/json";

            var errorPayload = new { success = false, message = "An internal server error occurred in the plugin.", details = ex.Message };
            string errorJson = JsonConvert.SerializeObject(errorPayload);
            byte[] errorBuffer = Encoding.UTF8.GetBytes(errorJson);

            response.ContentLength64 = errorBuffer.Length;
            await response.OutputStream.WriteAsync(errorBuffer, 0, errorBuffer.Length);
        }
    }

    public Task OnUnloadAsync()
    {
        return Task.CompletedTask;
    }

    public Task OnUpdateAsync(IPluginContext context)
    {
        return Task.CompletedTask;
    }
}

public class Config
{
    public string API_Username { get; set; } = "admin";
    public string API_Password { get; set; } = "password";
}
