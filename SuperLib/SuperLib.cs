using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UltimateServer.Plugins;
using UltimateServer.Servers;
using UltimateServer.Services;

public class SuperLib : IPlugin
{
    public string Name => "SuperLib";
    public string Version => "2.0.0";

    private IPluginContext _context;
    private AuthenticationService _authService;
    private Dictionary<string, DateTime> _rateLimitTracker = new Dictionary<string, DateTime>();
    private Dictionary<string, object> _cache = new Dictionary<string, object>();
    private Dictionary<string, DateTime> _cacheTimestamps = new Dictionary<string, DateTime>();
    private Config _config;

    // Static fields to track CPU usage between requests
    private static DateTime _lastCpuCheck = DateTime.UtcNow;
    private static TimeSpan _lastCpuTime = Process.GetCurrentProcess().TotalProcessorTime;

    public async Task OnLoadAsync(IPluginContext context)
    {
        await SetupConfig();

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
                    // Apply rate limiting
                    if (IsRateLimited(request))
                    {
                        await SendRateLimitResponseAsync();
                        return;
                    }

                    // Log the request
                    LogRequest(request);

                    await innerHandler(request);
                }
                else
                {
                    await SendUnauthorizedResponseAsync();
                }
            };
        }
        ;

        // Create a wrapper function that adds caching to a handler
        Func<HttpListenerRequest, Task> WithCache(Func<HttpListenerRequest, Task> innerHandler, int cacheMinutes = 5)
        {
            return async (request) =>
            {
                string cacheKey = GenerateCacheKey(request);

                // Check if we have a cached response
                if (_cache.ContainsKey(cacheKey) &&
                    DateTime.UtcNow.Subtract(_cacheTimestamps[cacheKey]).TotalMinutes < cacheMinutes)
                {
                    await SendCachedResponseAsync(cacheKey);
                    return;
                }

                // Execute the handler and cache the response
                var originalResponse = HttpContextHolder.CurrentResponse;
                var memoryStream = new MemoryStream();
                var originalStream = originalResponse.OutputStream;

                try
                {
                    // Capture the response by temporarily redirecting output
                    using (var streamWrapper = new ResponseStreamWrapper(originalStream, memoryStream))
                    {
                        // Execute the handler
                        await innerHandler(request);

                        // Cache the response if successful
                        if (originalResponse.StatusCode == (int)HttpStatusCode.OK)
                        {
                            _cache[cacheKey] = memoryStream.ToArray();
                            _cacheTimestamps[cacheKey] = DateTime.UtcNow;
                        }

                        // Send the captured response to the client
                        memoryStream.Position = 0;
                        await memoryStream.CopyToAsync(originalStream);
                    }
                }
                finally
                {
                    // Ensure the original stream is used for any further operations
                    // No need to restore as we didn't change the OutputStream property
                }
            };
        }
        ;

        // Register the public ping route (no authentication required)
        context.RegisterApiRoute("/api/health", HandlePingAsync);

        // Register all other routes, wrapped with the authentication check
        context.RegisterApiRoute("/api/system/info", WithAuthentication(WithCache(HandleSystemInfoAsync)));
        context.RegisterApiRoute("/api/system/performance", WithAuthentication(WithCache(HandlePerformanceAsync, 1)));

        // New endpoints
        context.RegisterApiRoute("/api/processes/kill", WithAuthentication(HandleKillProcessAsync));
        context.RegisterApiRoute("/api/filesystem/list", WithAuthentication(WithCache(HandleFileSystemListAsync)));
        context.RegisterApiRoute("/api/filesystem/read", WithAuthentication(WithCache(HandleFileSystemReadAsync)));
        context.RegisterApiRoute("/api/filesystem/download", WithAuthentication(HandleFileSystemDownloadAsync));
        context.RegisterApiRoute("/api/network/scan", HandleNetworkScanAsync);
        context.RegisterApiRoute("/api/system/logs", WithAuthentication(WithCache(HandleSystemLogsAsync)));
        context.RegisterApiRoute("/api/system/services", WithAuthentication(WithCache(HandleSystemServicesAsync)));
        context.RegisterApiRoute("/api/config", HandleConfigAsync);
        context.RegisterApiRoute("/api/cache/clear", WithAuthentication(HandleCacheClearAsync));
    }

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

    private bool IsRateLimited(HttpListenerRequest request)
    {
        string clientIp = GetClientIpAddress(request);
        DateTime lastRequest;

        if (_rateLimitTracker.TryGetValue(clientIp, out lastRequest))
        {
            // If the last request was less than the configured rate limit seconds ago, block
            if (DateTime.UtcNow.Subtract(lastRequest).TotalSeconds < _config.RateLimitSeconds)
            {
                return true;
            }
        }

        // Update the last request time
        _rateLimitTracker[clientIp] = DateTime.UtcNow;
        return false;
    }

    private string GetClientIpAddress(HttpListenerRequest request)
    {
        // Try to get the real IP from X-Forwarded-For header if present
        string xForwardedFor = request.Headers["X-Forwarded-For"];
        if (!string.IsNullOrEmpty(xForwardedFor))
        {
            return xForwardedFor.Split(',')[0].Trim();
        }

        // Fall back to the remote endpoint
        return request.RemoteEndPoint?.Address?.ToString() ?? "unknown";
    }

    private void LogRequest(HttpListenerRequest request)
    {
        if (!_config.EnableRequestLogging) return;

        string clientIp = GetClientIpAddress(request);
        string method = request.HttpMethod;
        string url = request.Url?.ToString() ?? "unknown";
        string userAgent = request.Headers["User-Agent"] ?? "unknown";

        _context.Logger.Log($"API Request: {clientIp} {method} {url} - {userAgent}");
    }

    private string GenerateCacheKey(HttpListenerRequest request)
    {
        return $"{request.HttpMethod}:{request.Url?.AbsolutePath}:{request.QueryString}";
    }

    private async Task SendCachedResponseAsync(string cacheKey)
    {
        var response = HttpContextHolder.CurrentResponse;
        if (response != null && _cache.ContainsKey(cacheKey))
        {
            byte[] cachedData = _cache[cacheKey] as byte[];
            if (cachedData != null)
            {
                response.StatusCode = (int)HttpStatusCode.OK;
                response.ContentType = "application/json";
                response.AddHeader("X-Cache", "HIT");

                response.ContentLength64 = cachedData.Length;
                await response.OutputStream.WriteAsync(cachedData, 0, cachedData.Length);
                response.OutputStream.Close();
            }
        }
    }

    private async Task SendRateLimitResponseAsync()
    {
        var response = HttpContextHolder.CurrentResponse;
        if (response != null)
        {
            response.StatusCode = 429; // Too Many Requests
            response.ContentType = "application/json";
            response.AddHeader("Retry-After", _config.RateLimitSeconds.ToString());

            var errorPayload = new
            {
                success = false,
                message = "Rate limit exceeded. Please try again later.",
                retryAfter = _config.RateLimitSeconds
            };
            string errorJson = JsonConvert.SerializeObject(errorPayload);
            byte[] errorBuffer = Encoding.UTF8.GetBytes(errorJson);

            response.ContentLength64 = errorBuffer.Length;
            await response.OutputStream.WriteAsync(errorBuffer, 0, errorBuffer.Length);
            response.OutputStream.Close();
        }
    }

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

    private async Task HandlePingAsync(HttpListenerRequest request)
    {
        var response = HttpContextHolder.CurrentResponse;
        try
        {
            response.StatusCode = (int)HttpStatusCode.OK;
            response.ContentType = "application/json";

            var pingData = new
            {
                status = "OK",
                timestamp = DateTime.UtcNow,
                version = Version,
                uptime = DateTime.UtcNow.Subtract(Process.GetCurrentProcess().StartTime).ToString(@"dd\.hh\:mm\:ss")
            };
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
                osArchitecture = RuntimeInformation.OSArchitecture.ToString(),
                machineName = Environment.MachineName,
                userName = Environment.UserName,
                processorCount = Environment.ProcessorCount,
                systemPageSize = Environment.SystemPageSize,
                clrVersion = Environment.Version.ToString(),
                workingDirectory = Environment.CurrentDirectory,
                systemDirectory = Environment.SystemDirectory,
                logicalDrives = Environment.GetLogicalDrives(),
                process = new
                {
                    id = process.Id,
                    startTime = process.StartTime,
                    threadCount = process.Threads.Count,
                    handleCount = process.HandleCount,
                    mainModule = process.MainModule?.FileName,
                    commandLine = Environment.CommandLine
                },
                hardware = GetHardwareInfo()
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

    private object GetHardwareInfo()
    {
        var hardwareInfo = new Dictionary<string, object>();

        try
        {
            // Basic hardware info without using Management classes
            hardwareInfo["cpu"] = new
            {
                architecture = RuntimeInformation.OSArchitecture,
                processorCount = Environment.ProcessorCount
            };

            hardwareInfo["memory"] = new
            {
                workingSetMB = GC.GetTotalMemory(false) / (1024 * 1024)
            };

            // Try to get disk information
            var drives = DriveInfo.GetDrives()
                .Where(d => d.IsReady)
                .Select(d => new
                {
                    name = d.Name,
                    label = d.VolumeLabel,
                    driveType = d.DriveType.ToString(),
                    totalSizeGB = Math.Round(d.TotalSize / 1024.0 / 1024.0 / 1024.0, 2),
                    freeSpaceGB = Math.Round(d.AvailableFreeSpace / 1024.0 / 1024.0 / 1024.0, 2),
                    format = d.DriveFormat
                }).ToList();

            hardwareInfo["drives"] = drives;
        }
        catch (Exception ex)
        {
            hardwareInfo["error"] = ex.Message;
        }

        return hardwareInfo;
    }

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
            long virtualMemory = currentProcess.VirtualMemorySize64 / 1024 / 1024;
            long privateMemory = currentProcess.PrivateMemorySize64 / 1024 / 1024;

            var drives = DriveInfo.GetDrives()
                .Where(d => d.IsReady)
                .Select(d => new
                {
                    name = d.Name,
                    label = d.VolumeLabel,
                    driveType = d.DriveType.ToString(),
                    totalSizeGB = Math.Round(d.TotalSize / 1024.0 / 1024.0 / 1024.0, 2),
                    freeSpaceGB = Math.Round(d.AvailableFreeSpace / 1024.0 / 1024.0 / 1024.0, 2),
                    usedPercentage = Math.Round((double)(d.TotalSize - d.AvailableFreeSpace) / d.TotalSize * 100, 2),
                    format = d.DriveFormat
                }).ToList();

            // Get network interface statistics
            var networkStats = new List<object>();
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces()
                    .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                                ni.NetworkInterfaceType != NetworkInterfaceType.Loopback))
                {
                    var stats = ni.GetIPStatistics();
                    networkStats.Add(new
                    {
                        name = ni.Name,
                        type = ni.NetworkInterfaceType.ToString(),
                        bytesReceived = stats.BytesReceived,
                        bytesSent = stats.BytesSent,
                        incomingPackets = stats.IncomingPacketsDiscarded,
                        outgoingPackets = stats.OutgoingPacketsDiscarded,
                        speed = ni.Speed / 1000 / 1000 // Mbps
                    });
                }
            }
            catch (Exception ex)
            {
                _context.Logger.Log($"Failed to get network statistics: {ex.Message}");
            }

            var performanceData = new
            {
                timestamp = DateTime.UtcNow,
                process = new
                {
                    cpuUsagePercentage = cpuUsage,
                    memory = new
                    {
                        workingSetMB = memoryUsed,
                        managedMemoryMB = managedMemory,
                        virtualMemoryMB = virtualMemory,
                        privateMemoryMB = privateMemory
                    },
                    handleCount = currentProcess.HandleCount,
                    threadCount = currentProcess.Threads.Count,
                    startTime = currentProcess.StartTime,
                    uptime = DateTime.UtcNow.Subtract(currentProcess.StartTime).ToString(@"dd\.hh\:mm\:ss")
                },
                disk = drives,
                network = networkStats
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

    private string GetMacAddress(NetworkInterface ni)
    {
        try
        {
            byte[] macBytes = ni.GetPhysicalAddress().GetAddressBytes();
            return string.Join(":", macBytes.Select(b => b.ToString("X2")));
        }
        catch
        {
            return "Unknown";
        }
    }

    private string GetProcessPath(Process p)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return p.MainModule?.FileName;
            }
            else
            {
                // For non-Windows systems, try to get the path from /proc
                return File.ReadAllText($"/proc/{p.Id}/cmdline").Replace("\0", " ");
            }
        }
        catch
        {
            return "Unknown";
        }
    }

    private async Task HandleKillProcessAsync(HttpListenerRequest request)
    {
        var response = HttpContextHolder.CurrentResponse;
        try
        {
            // Only allow POST method
            if (request.HttpMethod != "POST")
            {
                response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                await SendErrorResponseAsync(response, new Exception("Only POST method is allowed for this endpoint"));
                return;
            }

            // Read request body
            string requestBody;
            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
            {
                requestBody = await reader.ReadToEndAsync();
            }

            // Parse JSON to get process ID
            dynamic requestData = JsonConvert.DeserializeObject(requestBody);
            int processId = requestData.processId;

            if (processId <= 0)
            {
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                await SendErrorResponseAsync(response, new Exception("Invalid process ID"));
                return;
            }

            // Find and kill the process
            try
            {
                var process = Process.GetProcessById(processId);
                process.Kill();

                response.StatusCode = (int)HttpStatusCode.OK;
                response.ContentType = "application/json";

                var result = new { success = true, message = $"Process {processId} has been terminated" };
                string jsonResponse = JsonConvert.SerializeObject(result);
                byte[] buffer = Encoding.UTF8.GetBytes(jsonResponse);

                response.ContentLength64 = buffer.Length;
                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            }
            catch (ArgumentException)
            {
                response.StatusCode = (int)HttpStatusCode.NotFound;
                await SendErrorResponseAsync(response, new Exception($"Process with ID {processId} not found"));
            }
            catch (Exception ex)
            {
                response.StatusCode = (int)HttpStatusCode.InternalServerError;
                await SendErrorResponseAsync(response, new Exception($"Failed to terminate process {processId}: {ex.Message}"));
            }
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

    private async Task HandleFileSystemListAsync(HttpListenerRequest request)
    {
        var response = HttpContextHolder.CurrentResponse;
        try
        {
            // Get query parameters
            var queryParams = System.Web.HttpUtility.ParseQueryString(request.Url.Query);
            string path = queryParams["path"] ?? Environment.CurrentDirectory;
            bool showHidden = false;
            bool.TryParse(queryParams["showHidden"], out showHidden);

            // Validate path
            if (!Directory.Exists(path))
            {
                response.StatusCode = (int)HttpStatusCode.NotFound;
                await SendErrorResponseAsync(response, new DirectoryNotFoundException($"Directory '{path}' not found"));
                return;
            }

            response.StatusCode = (int)HttpStatusCode.OK;
            response.ContentType = "application/json";

            var directoryInfo = new DirectoryInfo(path);

            // Get directories
            var directories = directoryInfo.GetDirectories()
                .Where(d => showHidden || !d.Attributes.HasFlag(FileAttributes.Hidden))
                .Select(d => new
                {
                    name = d.Name,
                    fullName = d.FullName,
                    creationTime = d.CreationTime,
                    lastAccessTime = d.LastAccessTime,
                    lastWriteTime = d.LastWriteTime,
                    attributes = d.Attributes.ToString()
                })
                .OrderBy(d => d.name)
                .ToList();

            // Get files
            var files = directoryInfo.GetFiles()
                .Where(f => showHidden || !f.Attributes.HasFlag(FileAttributes.Hidden))
                .Select(f => new
                {
                    name = f.Name,
                    fullName = f.FullName,
                    length = f.Length,
                    lengthMB = Math.Round(f.Length / 1024.0 / 1024.0, 2),
                    extension = f.Extension,
                    creationTime = f.CreationTime,
                    lastAccessTime = f.LastAccessTime,
                    lastWriteTime = f.LastWriteTime,
                    attributes = f.Attributes.ToString()
                })
                .OrderBy(f => f.name)
                .ToList();

            var result = new
            {
                path = path,
                parent = directoryInfo.Parent?.FullName,
                directories = directories,
                files = files,
                totalFiles = files.Count,
                totalDirectories = directories.Count,
                totalSizeMB = files.Sum(f => f.lengthMB)
            };

            string jsonResponse = JsonConvert.SerializeObject(result, Formatting.Indented);
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

    private async Task HandleFileSystemReadAsync(HttpListenerRequest request)
    {
        var response = HttpContextHolder.CurrentResponse;
        try
        {
            // Get query parameters
            var queryParams = System.Web.HttpUtility.ParseQueryString(request.Url.Query);
            string path = queryParams["path"];

            if (string.IsNullOrEmpty(path))
            {
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                await SendErrorResponseAsync(response, new ArgumentException("Path parameter is required"));
                return;
            }

            // Validate file exists
            if (!File.Exists(path))
            {
                response.StatusCode = (int)HttpStatusCode.NotFound;
                await SendErrorResponseAsync(response, new FileNotFoundException($"File '{path}' not found"));
                return;
            }

            // Check file size (limit to 10MB for reading)
            var fileInfo = new FileInfo(path);
            if (fileInfo.Length > 10 * 1024 * 1024)
            {
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                await SendErrorResponseAsync(response, new Exception("File too large to read (max 10MB)"));
                return;
            }

            // Determine if file is text or binary
            bool isTextFile = IsTextFile(path);

            if (isTextFile)
            {
                // Read text file
                string content = await File.ReadAllTextAsync(path);

                response.StatusCode = (int)HttpStatusCode.OK;
                response.ContentType = "application/json";

                var result = new
                {
                    path = path,
                    name = fileInfo.Name,
                    size = fileInfo.Length,
                    sizeMB = Math.Round(fileInfo.Length / 1024.0 / 1024.0, 2),
                    creationTime = fileInfo.CreationTime,
                    lastAccessTime = fileInfo.LastAccessTime,
                    lastWriteTime = fileInfo.LastWriteTime,
                    isText = true,
                    content = content
                };

                string jsonResponse = JsonConvert.SerializeObject(result, Formatting.Indented);
                byte[] buffer = Encoding.UTF8.GetBytes(jsonResponse);

                response.ContentLength64 = buffer.Length;
                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            }
            else
            {
                // For binary files, return metadata only
                response.StatusCode = (int)HttpStatusCode.OK;
                response.ContentType = "application/json";

                var result = new
                {
                    path = path,
                    name = fileInfo.Name,
                    size = fileInfo.Length,
                    sizeMB = Math.Round(fileInfo.Length / 1024.0 / 1024.0, 2),
                    creationTime = fileInfo.CreationTime,
                    lastAccessTime = fileInfo.LastAccessTime,
                    lastWriteTime = fileInfo.LastWriteTime,
                    isText = false,
                    message = "Binary file content not displayed. Use the download endpoint to retrieve the file."
                };

                string jsonResponse = JsonConvert.SerializeObject(result, Formatting.Indented);
                byte[] buffer = Encoding.UTF8.GetBytes(jsonResponse);

                response.ContentLength64 = buffer.Length;
                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            }
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

    private bool IsTextFile(string path)
    {
        try
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();
            string[] textExtensions = { ".txt", ".log", ".json", ".xml", ".html", ".htm", ".css", ".js", ".cs", ".vb",
                                       ".py", ".java", ".cpp", ".c", ".h", ".php", ".rb", ".go", ".rs", ".sql",
                                       ".md", ".yaml", ".yml", ".ini", ".cfg", ".conf", ".sh", ".bat", ".ps1" };

            if (textExtensions.Contains(extension))
                return true;

            // Check the file content
            byte[] buffer = new byte[512];
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                fs.Read(buffer, 0, buffer.Length);
            }

            // If the buffer contains null bytes, it's likely binary
            if (buffer.Contains<byte>(0))
                return false;

            // Try to decode as UTF-8
            try
            {
                Encoding.UTF8.GetString(buffer);
                return true;
            }
            catch
            {
                return false;
            }
        }
        catch
        {
            return false;
        }
    }

    private async Task HandleFileSystemDownloadAsync(HttpListenerRequest request)
    {
        var response = HttpContextHolder.CurrentResponse;
        try
        {
            // Get query parameters
            var queryParams = System.Web.HttpUtility.ParseQueryString(request.Url.Query);
            string path = queryParams["path"];

            if (string.IsNullOrEmpty(path))
            {
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                await SendErrorResponseAsync(response, new ArgumentException("Path parameter is required"));
                return;
            }

            // Validate file exists
            if (!File.Exists(path))
            {
                response.StatusCode = (int)HttpStatusCode.NotFound;
                await SendErrorResponseAsync(response, new FileNotFoundException($"File '{path}' not found"));
                return;
            }

            var fileInfo = new FileInfo(path);

            // Set response headers for file download
            response.StatusCode = (int)HttpStatusCode.OK;
            response.ContentType = "application/octet-stream";
            response.AddHeader("Content-Disposition", $"attachment; filename=\"{fileInfo.Name}\"");
            response.ContentLength64 = fileInfo.Length;

            // Stream the file to the response
            using (var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                await fileStream.CopyToAsync(response.OutputStream);
            }
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

    private async Task HandleNetworkScanAsync(HttpListenerRequest request)
    {
        var response = HttpContextHolder.CurrentResponse;
        try
        {
            // Get query parameters
            var queryParams = System.Web.HttpUtility.ParseQueryString(request.Url.Query);
            string host = queryParams["host"] ?? "localhost";
            string portRange = queryParams["ports"] ?? "1-1024";
            int timeout = 1000; // Default timeout in ms
            int.TryParse(queryParams["timeout"], out timeout);

            // Parse port range
            int startPort, endPort;
            if (portRange.Contains("-"))
            {
                var parts = portRange.Split('-');
                int.TryParse(parts[0], out startPort);
                int.TryParse(parts[1], out endPort);
            }
            else
            {
                int.TryParse(portRange, out startPort);
                endPort = startPort;
            }

            // Validate port range
            if (startPort < 1 || endPort > 65535 || startPort > endPort)
            {
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                await SendErrorResponseAsync(response, new ArgumentException("Invalid port range"));
                return;
            }

            response.StatusCode = (int)HttpStatusCode.OK;
            response.ContentType = "application/json";

            var scanTasks = new List<Task<object>>();

            for (int port = startPort; port <= endPort; port++)
            {
                int currentPort = port; // Capture the current port value
                scanTasks.Add(ScanPortAsync(host, currentPort, timeout));
            }

            var scanResults = await Task.WhenAll(scanTasks);
            var openPorts = scanResults.Where(result => ((dynamic)result).isOpen).ToList();

            var result = new
            {
                host = host,
                portRange = $"{startPort}-{endPort}",
                timeout = timeout,
                scanTime = DateTime.UtcNow,
                totalPorts = endPort - startPort + 1,
                openPortsCount = openPorts.Count,
                openPorts = openPorts
            };

            string jsonResponse = JsonConvert.SerializeObject(result, Formatting.Indented);
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

    private async Task<object> ScanPortAsync(string host, int port, int timeout)
    {
        using (var tcpClient = new TcpClient())
        {
            try
            {
                var connectTask = tcpClient.ConnectAsync(host, port);
                var timeoutTask = Task.Delay(timeout);

                if (await Task.WhenAny(connectTask, timeoutTask) == connectTask)
                {
                    return new { port = port, isOpen = true, responseTime = timeout };
                }
                else
                {
                    return new { port = port, isOpen = false, error = "Timeout" };
                }
            }
            catch (Exception ex)
            {
                return new { port = port, isOpen = false, error = ex.Message };
            }
        }
    }

    private async Task HandleSystemLogsAsync(HttpListenerRequest request)
    {
        var response = HttpContextHolder.CurrentResponse;
        try
        {
            // Get query parameters
            var queryParams = System.Web.HttpUtility.ParseQueryString(request.Url.Query);
            string logType = queryParams["type"] ?? "application";
            int limit = 100;
            int.TryParse(queryParams["limit"], out limit);

            response.StatusCode = (int)HttpStatusCode.OK;
            response.ContentType = "application/json";

            var logs = new List<object>();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    // Use PowerShell to get Windows Event Log entries
                    string psCommand = $"Get-EventLog -LogName {logType} -Newest {limit} | ConvertTo-Json";
                    string psResult = RunPowerShellCommand(psCommand);

                    if (!string.IsNullOrEmpty(psResult))
                    {
                        // Parse the JSON result
                        var logEntries = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(psResult);
                        if (logEntries != null)
                        {
                            foreach (var entry in logEntries)
                            {
                                logs.Add(new
                                {
                                    index = entry.ContainsKey("Index") ? entry["Index"] : null,
                                    entryType = entry.ContainsKey("EntryType") ? entry["EntryType"].ToString() : "Unknown",
                                    source = entry.ContainsKey("Source") ? entry["Source"].ToString() : "Unknown",
                                    message = entry.ContainsKey("Message") ? entry["Message"].ToString() : "",
                                    timeGenerated = entry.ContainsKey("TimeGenerated") ? entry["TimeGenerated"] : DateTime.MinValue
                                });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _context.Logger.Log($"Failed to read Windows Event Log: {ex.Message}");
                    logs.Add(new { error = $"Failed to read Windows Event Log: {ex.Message}" });
                }
            }
            else
            {
                // For non-Windows systems, try to read common log files
                string[] logPaths = logType.ToLowerInvariant() switch
                {
                    "system" => new[] { "/var/log/syslog", "/var/log/messages" },
                    "auth" => new[] { "/var/log/auth.log" },
                    "kern" => new[] { "/var/log/kern.log" },
                    _ => new[] { "/var/log/syslog" }
                };

                foreach (var logPath in logPaths)
                {
                    try
                    {
                        if (File.Exists(logPath))
                        {
                            var lines = await File.ReadAllLinesAsync(logPath);
                            var recentLines = lines.Reverse().Take(limit);

                            foreach (var line in recentLines)
                            {
                                logs.Add(new { logFile = logPath, message = line });
                            }

                            break; // Only read the first available log file
                        }
                    }
                    catch (Exception ex)
                    {
                        _context.Logger.Log($"Failed to read log file {logPath}: {ex.Message}");
                    }
                }

                if (logs.Count == 0)
                {
                    logs.Add(new { error = "No log files available or could not be read" });
                }
            }

            var result = new
            {
                logType = logType,
                limit = limit,
                count = logs.Count,
                timestamp = DateTime.UtcNow,
                entries = logs
            };

            string jsonResponse = JsonConvert.SerializeObject(result, Formatting.Indented);
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

    private async Task HandleSystemServicesAsync(HttpListenerRequest request)
    {
        var response = HttpContextHolder.CurrentResponse;
        try
        {
            response.StatusCode = (int)HttpStatusCode.OK;
            response.ContentType = "application/json";

            var services = new List<object>();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    // Use PowerShell to get Windows services
                    string psCommand = "Get-Service | ConvertTo-Json";
                    string psResult = RunPowerShellCommand(psCommand);

                    if (!string.IsNullOrEmpty(psResult))
                    {
                        // Parse the JSON result
                        var serviceList = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(psResult);
                        if (serviceList != null)
                        {
                            foreach (var service in serviceList)
                            {
                                services.Add(new
                                {
                                    name = service.ContainsKey("Name") ? service["Name"].ToString() : "Unknown",
                                    displayName = service.ContainsKey("DisplayName") ? service["DisplayName"].ToString() : "Unknown",
                                    status = service.ContainsKey("Status") ? service["Status"].ToString() : "Unknown",
                                    serviceType = service.ContainsKey("ServiceType") ? service["ServiceType"].ToString() : "Unknown",
                                    startType = service.ContainsKey("StartType") ? service["StartType"].ToString() : "Unknown"
                                });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _context.Logger.Log($"Failed to get Windows services: {ex.Message}");
                    services.Add(new { error = $"Failed to get Windows services: {ex.Message}" });
                }
            }
            else
            {
                try
                {
                    // For non-Windows systems, try to get systemd services
                    var systemctlResult = RunCommand("systemctl list-units --type=service --no-pager --no-legend");
                    var lines = systemctlResult.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

                    foreach (var line in lines)
                    {
                        var parts = Regex.Split(line.Trim(), @"\s+");
                        if (parts.Length >= 4)
                        {
                            string unit = parts[0];
                            string load = parts[1];
                            string active = parts[2];
                            string sub = parts[3];
                            string description = parts.Length > 4 ? string.Join(" ", parts.Skip(4)) : "";

                            services.Add(new
                            {
                                name = unit,
                                description = description,
                                load = load,
                                active = active,
                                sub = sub,
                                status = $"{active} {sub}"
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    _context.Logger.Log($"Failed to get systemd services: {ex.Message}");

                    // Try to get init.d services as fallback
                    try
                    {
                        var initResult = RunCommand("ls /etc/init.d/");
                        var servicesList = initResult.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

                        foreach (var serviceName in servicesList)
                        {
                            if (!string.IsNullOrWhiteSpace(serviceName))
                            {
                                services.Add(new
                                {
                                    name = serviceName,
                                    status = "Unknown"
                                });
                            }
                        }
                    }
                    catch (Exception initEx)
                    {
                        _context.Logger.Log($"Failed to get init.d services: {initEx.Message}");
                        services.Add(new { error = "Failed to retrieve services information" });
                    }
                }
            }

            var result = new
            {
                count = services.Count,
                timestamp = DateTime.UtcNow,
                services = services
            };

            string jsonResponse = JsonConvert.SerializeObject(result, Formatting.Indented);
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

    private async Task HandleConfigAsync(HttpListenerRequest request)
    {
        var response = HttpContextHolder.CurrentResponse;
        try
        {
            if (request.HttpMethod == "GET")
            {
                // Return current configuration (excluding sensitive data)
                response.StatusCode = (int)HttpStatusCode.OK;
                response.ContentType = "application/json";

                var configData = new
                {
                    enableRequestLogging = _config.EnableRequestLogging,
                    rateLimitSeconds = _config.RateLimitSeconds,
                    cacheEnabled = _config.CacheEnabled,
                    cacheMinutes = _config.CacheMinutes
                };

                string jsonResponse = JsonConvert.SerializeObject(configData, Formatting.Indented);
                byte[] buffer = Encoding.UTF8.GetBytes(jsonResponse);

                response.ContentLength64 = buffer.Length;
                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            }
            else if (request.HttpMethod == "POST")
            {
                // Update configuration
                string requestBody;
                using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                {
                    requestBody = await reader.ReadToEndAsync();
                }

                try
                {
                    var newConfig = JsonConvert.DeserializeObject<Config>(requestBody);

                    // Update configuration
                    _config.EnableRequestLogging = newConfig.EnableRequestLogging;
                    _config.RateLimitSeconds = newConfig.RateLimitSeconds;
                    _config.CacheEnabled = newConfig.CacheEnabled;
                    _config.CacheMinutes = newConfig.CacheMinutes;

                    // Save configuration
                    await SaveConfigAsync();

                    response.StatusCode = (int)HttpStatusCode.OK;
                    response.ContentType = "application/json";

                    var result = new { success = true, message = "Configuration updated successfully" };
                    string jsonResponse = JsonConvert.SerializeObject(result);
                    byte[] buffer = Encoding.UTF8.GetBytes(jsonResponse);

                    response.ContentLength64 = buffer.Length;
                    await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                }
                catch (Exception ex)
                {
                    response.StatusCode = (int)HttpStatusCode.BadRequest;
                    await SendErrorResponseAsync(response, new Exception($"Invalid configuration: {ex.Message}"));
                }
            }
            else
            {
                response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                await SendErrorResponseAsync(response, new Exception("Only GET and POST methods are allowed for this endpoint"));
            }
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

    private async Task HandleCacheClearAsync(HttpListenerRequest request)
    {
        var response = HttpContextHolder.CurrentResponse;
        try
        {
            // Clear cache
            _cache.Clear();
            _cacheTimestamps.Clear();

            response.StatusCode = (int)HttpStatusCode.OK;
            response.ContentType = "application/json";

            var result = new { success = true, message = "Cache cleared successfully" };
            string jsonResponse = JsonConvert.SerializeObject(result);
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

    private string RunCommand(string command)
    {
        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "cmd.exe" : "/bin/bash",
                Arguments = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? $"/c {command}" : $"-c \"{command}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using (var process = Process.Start(processInfo))
            {
                process.WaitForExit();
                return process.StandardOutput.ReadToEnd();
            }
        }
        catch (Exception ex)
        {
            return $"Error running command: {ex.Message}";
        }
    }

    private string RunPowerShellCommand(string command)
    {
        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-Command \"{command}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using (var process = Process.Start(processInfo))
            {
                process.WaitForExit();
                return process.StandardOutput.ReadToEnd();
            }
        }
        catch (Exception ex)
        {
            return $"Error running PowerShell command: {ex.Message}";
        }
    }

    private async Task SendErrorResponseAsync(HttpListenerResponse response, Exception ex)
    {
        _context.Logger.Log($"An error occurred in the {Name} plugin: {ex.Message}");
        if (response != null)
        {
            response.StatusCode = (int)HttpStatusCode.InternalServerError;
            response.ContentType = "application/json";

            var errorPayload = new
            {
                success = false,
                message = "An internal server error occurred in the plugin.",
                details = ex.Message,
                stackTrace = _config.IncludeStackTrace ? ex.StackTrace : null
            };
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

    public async Task SetupConfig()
    {
        var pluginFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins", Name);
        var pluginConfig = Path.Combine(pluginFolder, "config.json");

        if (!Directory.Exists(pluginFolder))
            Directory.CreateDirectory(pluginFolder);

        if (!File.Exists(pluginConfig))
        {
            _config = new Config();
            await File.WriteAllTextAsync(pluginConfig, JsonConvert.SerializeObject(_config, Formatting.Indented));
        }
        else
        {
            var data = await File.ReadAllTextAsync(pluginConfig);
            var json = JsonConvert.DeserializeObject<Config>(data);
            _config = json ?? new Config();
        }
    }

    private async Task SaveConfigAsync()
    {
        var pluginFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins", Name);
        var pluginConfig = Path.Combine(pluginFolder, "config.json");

        await File.WriteAllTextAsync(pluginConfig, JsonConvert.SerializeObject(_config, Formatting.Indented));
    }
}

public class Config
{
    public string API_Username { get; set; } = "admin";
    public string API_Password { get; set; } = "password";
    public bool EnableRequestLogging { get; set; } = true;
    public int RateLimitSeconds { get; set; } = 1;
    public bool CacheEnabled { get; set; } = true;
    public int CacheMinutes { get; set; } = 5;
    public bool IncludeStackTrace { get; set; } = false;
}

// Custom stream wrapper to capture response data for caching
public class ResponseStreamWrapper : Stream
{
    private readonly Stream _originalStream;
    private readonly MemoryStream _captureStream;

    public ResponseStreamWrapper(Stream originalStream, MemoryStream captureStream)
    {
        _originalStream = originalStream;
        _captureStream = captureStream;
    }

    public override bool CanRead => _originalStream.CanRead;

    public override bool CanSeek => _originalStream.CanSeek;

    public override bool CanWrite => _originalStream.CanWrite;

    public override long Length => _originalStream.Length;

    public override long Position
    {
        get => _originalStream.Position;
        set => _originalStream.Position = value;
    }

    public override void Flush()
    {
        _originalStream.Flush();
        _captureStream.Flush();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        return _originalStream.Read(buffer, offset, count);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        return _originalStream.Seek(offset, origin);
    }

    public override void SetLength(long value)
    {
        _originalStream.SetLength(value);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        _originalStream.Write(buffer, offset, count);
        _captureStream.Write(buffer, offset, count);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _originalStream.Dispose();
            _captureStream.Dispose();
        }
        base.Dispose(disposing);
    }
}