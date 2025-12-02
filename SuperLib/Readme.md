<div class="container">
<h1 id="superlib-plugin">SuperLib Plugin</h1>
<p>A comprehensive monitoring and system management plugin for <strong>UltimateServer</strong>. This plugin provides an extensive set of API endpoints to check server health, view performance metrics, manage processes, browse the file system, scan networks, and much more - all without requiring any external dependencies.</p>

<h2 id="table-of-contents">Table of Contents</h2>
<ul>
<li><a href="#features">Features</a></li>
<li><a href="#prerequisites">Prerequisites</a></li>
<li><a href="#installation">Installation</a></li>
<li><a href="#configuration">Configuration</a></li>
<li><a href="#authentication">Authentication</a></li>
<li><a href="#api-endpoints">API Endpoints</a></li>
<li><a href="#usage-examples">Usage Examples</a></li>
<li><a href="#troubleshooting">Troubleshooting</a></li>
</ul>

<h2 id="features">Features</h2>
<div class="feature-list">
<div class="feature-card">
<h3 id="-health-check-endpoint">🩺 Health Check Endpoint</h3>
<p>A simple, public endpoint to verify that server and plugin are running with uptime information.</p>
</div>
<div class="feature-card">
<h3 id="-system-information">💻 System Information</h3>
<p>Get detailed information about server's operating system, hardware, and server process itself.</p>
</div>
<div class="feature-card">
<h3 id="-performance-metrics">📈 Performance Metrics</h3>
<p>Monitor real-time CPU usage, memory consumption, and disk space for all fixed drives.</p>
</div>
<div class="feature-card">
<h3 id="-network-information">🌐 Network Information</h3>
<p>View details about active network interfaces, TCP/UDP connections, and network statistics.</p>
</div>
<div class="feature-card">
<h3 id="-process-management">⚙️ Process Management</h3>
<p>List all running processes with detailed information and ability to terminate processes.</p>
</div>
<div class="feature-card">
<h3 id="-file-system-access">📁 File System Access</h3>
<p>Browse directories, read files, and download files from the server's file system.</p>
</div>
<div class="feature-card">
<h3 id="-network-scanning">🔍 Network Scanning</h3>
<p>Scan ports on any host to identify open services and potential security issues.</p>
</div>
<div class="feature-card">
<h3 id="-system-logs">📋 System Logs</h3>
<p>Access system logs from Windows Event Log or Linux log files for troubleshooting.</p>
</div>
<div class="feature-card">
<h3 id="-service-management">🔧 Service Management</h3>
<p>View and manage system services on Windows and Linux platforms.</p>
</div>
<div class="feature-card">
<h3 id="-response-caching">⚡ Response Caching</h3>
<p>Intelligent caching system to improve performance for frequently accessed data.</p>
</div>
<div class="feature-card">
<h3 id="-rate-limiting">🚦 Rate Limiting</h3>
<p>Built-in rate limiting to prevent abuse and ensure system stability.</p>
</div>
<div class="feature-card">
<h3 id="-secure">🔐 Secure</h3>
<p>All sensitive endpoints are protected using the same JWT (JSON Web Token) authentication as the core server.</p>
</div>
</div>

<h2 id="prerequisites">Prerequisites</h2>
<ul>
<li>A running instance of <strong>UltimateServer</strong>.</li>
<li>The server must have the plugin system enabled.</li>
<li>For full functionality, administrative privileges on the host system.</li>
</ul>

<h2 id="installation">Installation</h2>
<ol>
<li><strong>Download</strong>: Download <code>SuperLib.dll</code> into one of your directories.</li>
<li><strong>Upload</strong>: Go to the UltimateServer dashboard in the plugins tab, in the upload section click on <code>Browse Files</code> and choose the <code>SuperLib.dll</code> you downloaded. The plugin manager will automatically detect and load <code>SuperLib</code>.</li>
</ol>
<p>You should see a log message indicating that the plugin has loaded successfully and registered its API routes.</p>

<h2 id="configuration">Configuration</h2>
<p>The plugin includes a comprehensive <code>Config</code> class with various settings that control its behavior:</p>
<pre><code>public class Config
{
public string API_Username { get; set; } = "admin";
public string API_Password { get; set; } = "password";
public bool EnableRequestLogging { get; set; } = true;
public int RateLimitSeconds { get; set; } = 1;
public bool CacheEnabled { get; set; } = true;
public int CacheMinutes { get; set; } = 5;
public bool IncludeStackTrace { get; set; } = false;
}</code></pre>
<p>Configuration options:</p>
<ul>
<li><strong>API_Username/API_Password</strong>: Default credentials (not used in current implementation)</li>
<li><strong>EnableRequestLogging</strong>: Enable/disable logging of API requests</li>
<li><strong>RateLimitSeconds</strong>: Minimum seconds between requests from the same IP</li>
<li><strong>CacheEnabled</strong>: Enable/disable response caching</li>
<li><strong>CacheMinutes</strong>: Default cache duration in minutes</li>
<li><strong>IncludeStackTrace</strong>: Include stack traces in error responses</li>
</ul>
<p>The configuration file is automatically created at <code>plugins/SuperLib/config.json</code> and can be modified directly or through the API.</p>

<h2 id="authentication">Authentication</h2>
<p>To access the protected API endpoints, you must provide a valid JWT token in the <code>Authorization</code> header of your request.</p>
<ul>
<li><strong>How to get a token</strong>: Send a <code>POST</code> request to the server's <code>/api/login</code> endpoint with valid user credentials.</li>
<li><strong>How to use the token</strong>: Include the token in your API requests as a Bearer token.</li>
</ul>
<p><strong>Example Header:</strong></p>
<pre><code>Authorization: Bearer YOUR_JWT_TOKEN_HERE</code></pre>
<p>The <code>/api/health</code> endpoint is the only route that does <strong>not</strong> require authentication.</p>

<h2 id="api-endpoints">API Endpoints</h2>
<p>All requests should be made to <code>http://your-server-address/...</code>.</p>

<h3 id="get-apihealth">GET /api/health</h3>
<p>A simple health check endpoint used to verify that the server and plugin are running correctly.</p>
<ul>
<li><strong>Authentication</strong>: <code>None</code></li>
</ul>
<p><strong>Example Request</strong>:</p>
<pre><code>curl -X GET "http://your-server-address/api/health"</code></pre>
<p><strong>Example Response</strong>:</p>
<pre><code>{
"status": "OK",
"timestamp": "2023-11-28T12:00:00.123Z",
"version": "2.0.0",
"uptime": "00.15:30:45"
}</code></pre>

<h3 id="get-apisysteminfo">GET /api/system/info</h3>
<p>Retrieves detailed system and process information, including OS version, hardware details, and process information.</p>
<ul>
<li><strong>Authentication</strong>: <code>Required (Bearer Token)</code></li>
</ul>
<p><strong>Example Request</strong>:</p>
<pre><code>curl -X GET "http://your-server-address/api/system/info" -H "Authorization: Bearer YOUR_JWT_TOKEN_HERE"</code></pre>
<p><strong>Example Response</strong>:</p>
<pre><code>{
"osVersion": "Microsoft Windows NT 10.0.19045.0",
"osArchitecture": "X64",
"machineName": "DESKTOP-ABC123",
"userName": "Admin",
"processorCount": 8,
"systemPageSize": 4096,
"clrVersion": "6.0.21",
"workingDirectory": "C:\\UltimateServer",
"systemDirectory": "C:\\Windows\\system32",
"logicalDrives": ["C:\\"],
"process": {
"id": 12345,
"startTime": "2023-11-28T10:30:00.123Z",
"threadCount": 25,
"handleCount": 510,
"mainModule": "C:\\UltimateServer\\UltimateServer.exe",
"commandLine": "C:\\UltimateServer\\UltimateServer.exe"
},
"hardware": {
"cpu": {
"architecture": "X64",
"processorCount": 8
},
"memory": {
"workingSetMB": 512
},
"drives": [
{
"name": "C:\\",
"label": "Windows",
"driveType": "Fixed",
"totalSizeGB": 475.89,
"freeSpaceGB": 120.55,
"format": "NTFS"
}
]
}
}</code></pre>

<h3 id="get-apisystemperformance">GET /api/system/performance</h3>
<p>Provides real-time performance metrics, including current CPU usage, memory usage, disk space statistics, and network interface statistics.</p>
<ul>
<li><strong>Authentication</strong>: <code>Required (Bearer Token)</code></li>
</ul>
<p><strong>Example Request</strong>:</p>
<pre><code>curl -X GET "http://your-server-address/api/system/performance" -H "Authorization: Bearer YOUR_JWT_TOKEN_HERE"</code></pre>
<p><strong>Example Response</strong>:</p>
<pre><code>{
"timestamp": "2023-11-28T12:00:00.123Z",
"process": {
"cpuUsagePercentage": 15.75,
"memory": {
"workingSetMB": 512,
"managedMemoryMB": 128,
"virtualMemoryMB": 1024,
"privateMemoryMB": 480
},
"handleCount": 510,
"threadCount": 25,
"startTime": "2023-11-28T10:30:00.123Z",
"uptime": "00.15:30:45"
},
"disk": [
{
"name": "C:\\",
"label": "Windows",
"driveType": "Fixed",
"totalSizeGB": 475.89,
"freeSpaceGB": 120.55,
"usedPercentage": 74.68,
"format": "NTFS"
}
],
"network": [
{
"name": "Ethernet",
"type": "Ethernet",
"bytesReceived": 1234567890,
"bytesSent": 987654321,
"incomingPackets": 1234567,
"outgoingPackets": 987654,
"speed": 1000
}
]
}</code></pre>

<h3 id="get-apinetworkinfo">GET /api/network/info</h3>
<p>Returns detailed information about the server's active network interfaces, TCP/UDP connections, and network statistics.</p>
<ul>
<li><strong>Authentication</strong>: <code>Required (Bearer Token)</code></li>
</ul>
<p><strong>Example Request</strong>:</p>
<pre><code>curl -X GET "http://your-server-address/api/network/info" -H "Authorization: Bearer YOUR_JWT_TOKEN_HERE"</code></pre>
<p><strong>Example Response</strong>:</p>
<pre><code>{
"hostname": "desktop-abc123",
"domainName": "example.com",
"nodeType": "Hybrid",
"activeTcpConnections": 12,
"tcpListeners": 5,
"udpListeners": 3,
"interfaces": [
{
"name": "Ethernet",
"description": "Intel(R) Ethernet Connection",
"type": "Ethernet",
"speed": 1000,
"status": "Up",
"isReceiveOnly": false,
"supportsMulticast": true,
"macAddress": "00:1A:2B:3C:4D:5E",
"ipProperties": {
"dnsSuffix": "example.com",
"dnsEnabled": true,
"ipAddresses": [
{
"address": "192.168.1.10",
"addressFamily": "InterNetwork",
"isDnsEligible": true,
"isTransient": false
}
],
"gatewayAddresses": ["192.168.1.1"],
"dnsServers": ["8.8.8.8", "8.8.4.4"]
},
"statistics": {
"bytesReceived": 1234567890,
"bytesSent": 987654321,
"incomingPacketsDiscarded": 10,
"outgoingPacketsDiscarded": 5
}
}
],
"activeConnections": [
{
"localAddress": "192.168.1.10:54321",
"remoteAddress": "74.125.224.72:443",
"state": "Established"
}
],
"tcpListeners": [
{
"address": "0.0.0.0:80"
}
],
"udpListeners": [
{
"address": "0.0.0.0:53"
}
]
}</code></pre>

<h3 id="get-apiprocesses">GET /api/processes</h3>
<p>Returns a list of all running processes with detailed information.</p>
<ul>
<li><strong>Authentication</strong>: <code>Required (Bearer Token)</code></li>
<li><strong>Query Parameters</strong>:
<ul>
<li><code>name</code> (optional): Filter processes by name (partial match)</li>
<li><code>limit</code> (optional): Limit the number of results</li>
</ul>
</li>
</ul>
<p><strong>Example Request</strong>:</p>
<pre><code>curl -X GET "http://your-server-address/api/processes?name=chrome&limit=10" -H "Authorization: Bearer YOUR_JWT_TOKEN_HERE"</code></pre>
<p><strong>Example Response</strong>:</p>
<pre><code>[
{
"id": 1234,
"name": "chrome",
"startTime": "2023-11-28T10:30:00.123Z",
"workingSetMB": 1024,
"virtualMemoryMB": 2048,
"privateMemoryMB": 960,
"threadCount": 32,
"handleCount": 1024,
"mainModule": "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe",
"mainWindowTitle": "Google Chrome",
"responding": true,
"path": "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe"
}
]</code></pre>

<h3 id="post-apiprocesseskill">POST /api/processes/kill</h3>
<p>Terminates a process by its ID.</p>
<ul>
<li><strong>Authentication</strong>: <code>Required (Bearer Token)</code></li>
<li><strong>Request Body</strong>:
<ul>
<li><code>processId</code> (required): ID of the process to terminate</li>
</ul>
</li>
</ul>
<p><strong>Example Request</strong>:</p>
<pre><code>curl -X POST "http://your-server-address/api/processes/kill" \
-H "Authorization: Bearer YOUR_JWT_TOKEN_HERE" \
-H "Content-Type: application/json" \
-d '{"processId": 1234}'</code></pre>
<p><strong>Example Response</strong>:</p>
<pre><code>{
"success": true,
"message": "Process 1234 has been terminated"
}</code></pre>

<h3 id="get-apifilesystemlist">GET /api/filesystem/list</h3>
<p>Returns a list of files and directories in the specified path.</p>
<ul>
<li><strong>Authentication</strong>: <code>Required (Bearer Token)</code></li>
<li><strong>Query Parameters</strong>:
<ul>
<li><code>path</code> (optional): Directory path to list (default: current working directory)</li>
<li><code>showHidden</code> (optional): Include hidden files and directories (default: false)</li>
</ul>
</li>
</ul>
<p><strong>Example Request</strong>:</p>
<pre><code>curl -X GET "http://your-server-address/api/filesystem/list?path=C:\\Temp&showHidden=true" -H "Authorization: Bearer YOUR_JWT_TOKEN_HERE"</code></pre>
<p><strong>Example Response</strong>:</p>
<pre><code>{
"path": "C:\\Temp",
"parent": "C:\\",
"directories": [
{
"name": "Logs",
"fullName": "C:\\Temp\\Logs",
"creationTime": "2023-11-20T10:30:00.123Z",
"lastAccessTime": "2023-11-28T12:00:00.123Z",
"lastWriteTime": "2023-11-27T15:45:00.123Z",
"attributes": "Directory"
}
],
"files": [
{
"name": "report.pdf",
"fullName": "C:\\Temp\\report.pdf",
"length": 1048576,
"lengthMB": 1.0,
"extension": ".pdf",
"creationTime": "2023-11-25T10:30:00.123Z",
"lastAccessTime": "2023-11-28T12:00:00.123Z",
"lastWriteTime": "2023-11-27T15:45:00.123Z",
"attributes": "Archive"
}
],
"totalFiles": 1,
"totalDirectories": 1,
"totalSizeMB": 1.0
}</code></pre>

<h3 id="get-apifilesystemread">GET /api/filesystem/read</h3>
<p>Reads the content of a text file (up to 10MB).</p>
<ul>
<li><strong>Authentication</strong>: <code>Required (Bearer Token)</code></li>
<li><strong>Query Parameters</strong>:
<ul>
<li><code>path</code> (required): Path to the file to read</li>
</ul>
</li>
</ul>
<p><strong>Example Request</strong>:</p>
<pre><code>curl -X GET "http://your-server-address/api/filesystem/read?path=C:\\Temp\\log.txt" -H "Authorization: Bearer YOUR_JWT_TOKEN_HERE"</code></pre>
<p><strong>Example Response</strong>:</p>
<pre><code>{
"path": "C:\\Temp\\log.txt",
"name": "log.txt",
"size": 1024,
"sizeMB": 0.001,
"creationTime": "2023-11-25T10:30:00.123Z",
"lastAccessTime": "2023-11-28T12:00:00.123Z",
"lastWriteTime": "2023-11-27T15:45:00.123Z",
"isText": true,
"content": "This is the content of the log file..."
}</code></pre>

<h3 id="get-apifilesystemdownload">GET /api/filesystem/download</h3>
<p>Downloads a file from the server.</p>
<ul>
<li><strong>Authentication</strong>: <code>Required (Bearer Token)</code></li>
<li><strong>Query Parameters</strong>:
<ul>
<li><code>path</code> (required): Path to the file to download</li>
</ul>
</li>
</ul>
<p><strong>Example Request</strong>:</p>
<pre><code>curl -X GET "http://your-server-address/api/filesystem/download?path=C:\\Temp\\report.pdf" -H "Authorization: Bearer YOUR_JWT_TOKEN_HERE" -o report.pdf</code></pre>

<h3 id="get-apinetworkscan">GET /api/network/scan</h3>
<p>Scans ports on a specified host to identify open services.</p>
<ul>
<li><strong>Authentication</strong>: <code>Required (Bearer Token)</code></li>
<li><strong>Query Parameters</strong>:
<ul>
<li><code>host</code> (optional): Host to scan (default: localhost)</li>
<li><code>ports</code> (optional): Port range to scan (default: 1-1024)</li>
<li><code>timeout</code> (optional): Connection timeout in milliseconds (default: 1000)</li>
</ul>
</li>
</ul>
<p><strong>Example Request</strong>:</p>
<pre><code>curl -X GET "http://your-server-address/api/network/scan?host=192.168.1.1&ports=80,443,8080&timeout=500" -H "Authorization: Bearer YOUR_JWT_TOKEN_HERE"</code></pre>
<p><strong>Example Response</strong>:</p>
<pre><code>{
"host": "192.168.1.1",
"portRange": "80-8080",
"timeout": 500,
"scanTime": "2023-11-28T12:00:00.123Z",
"totalPorts": 3,
"openPortsCount": 2,
"openPorts": [
{
"port": 80,
"isOpen": true,
"responseTime": 50
},
{
"port": 443,
"isOpen": true,
"responseTime": 60
},
{
"port": 8080,
"isOpen": false,
"error": "Timeout"
}
]
}</code></pre>

<h3 id="get-apisystemlogs">GET /api/system/logs</h3>
<p>Retrieves system logs from Windows Event Log or Linux log files.</p>
<ul>
<li><strong>Authentication</strong>: <code>Required (Bearer Token)</code></li>
<li><strong>Query Parameters</strong>:
<ul>
<li><code>type</code> (optional): Log type (default: application)</li>
<li><code>limit</code> (optional): Number of entries to retrieve (default: 100)</li>
</ul>
</li>
</ul>
<p><strong>Example Request</strong>:</p>
<pre><code>curl -X GET "http://your-server-address/api/system/logs?type=system&limit=50" -H "Authorization: Bearer YOUR_JWT_TOKEN_HERE"</code></pre>
<p><strong>Example Response</strong>:</p>
<pre><code>{
"logType": "system",
"limit": 50,
"count": 50,
"timestamp": "2023-11-28T12:00:00.123Z",
"entries": [
{
"index": 12345,
"entryType": "Information",
"source": "Microsoft-Windows-Kernel-Power",
"message": "The system is entering sleep mode.",
"timeGenerated": "2023-11-28T11:30:00.123Z"
}
]
}</code></pre>

<h3 id="get-apisystemservices">GET /api/system/services</h3>
<p>Retrieves information about system services.</p>
<ul>
<li><strong>Authentication</strong>: <code>Required (Bearer Token)</code></li>
</ul>
<p><strong>Example Request</strong>:</p>
<pre><code>curl -X GET "http://your-server-address/api/system/services" -H "Authorization: Bearer YOUR_JWT_TOKEN_HERE"</code></pre>
<p><strong>Example Response</strong>:</p>
<pre><code>{
"count": 150,
"timestamp": "2023-11-28T12:00:00.123Z",
"services": [
{
"name": "UltimateServer",
"displayName": "UltimateServer Service",
"status": "Running",
"serviceType": "Win32OwnProcess",
"startType": "Automatic"
}
]
}</code></pre>

<h3 id="get-apiconfig">GET /api/config</h3>
<p>Retrieves the current plugin configuration (excluding sensitive data).</p>
<ul>
<li><strong>Authentication</strong>: <code>Required (Bearer Token)</code></li>
</ul>
<p><strong>Example Request</strong>:</p>
<pre><code>curl -X GET "http://your-server-address/api/config" -H "Authorization: Bearer YOUR_JWT_TOKEN_HERE"</code></pre>
<p><strong>Example Response</strong>:</p>
<pre><code>{
"enableRequestLogging": true,
"rateLimitSeconds": 1,
"cacheEnabled": true,
"cacheMinutes": 5
}</code></pre>

<h3 id="post-apiconfig">POST /api/config</h3>
<p>Updates the plugin configuration.</p>
<ul>
<li><strong>Authentication</strong>: <code>Required (Bearer Token)</code></li>
<li><strong>Request Body</strong>: Configuration object with updated values</li>
</ul>
<p><strong>Example Request</strong>:</p>
<pre><code>curl -X POST "http://your-server-address/api/config" \
-H "Authorization: Bearer YOUR_JWT_TOKEN_HERE" \
-H "Content-Type: application/json" \
-d '{"enableRequestLogging": false, "rateLimitSeconds": 2}'</code></pre>
<p><strong>Example Response</strong>:</p>
<pre><code>{
"success": true,
"message": "Configuration updated successfully"
}</code></pre>

<h3 id="post-apicacheclear">POST /api/cache/clear</h3>
<p>Clears the plugin's response cache.</p>
<ul>
<li><strong>Authentication</strong>: <code>Required (Bearer Token)</code></li>
</ul>
<p><strong>Example Request</strong>:</p>
<pre><code>curl -X POST "http://your-server-address/api/cache/clear" -H "Authorization: Bearer YOUR_JWT_TOKEN_HERE"</code></pre>
<p><strong>Example Response</strong>:</p>
<pre><code>{
"success": true,
"message": "Cache cleared successfully"
}</code></pre>

<h2 id="usage-examples">Usage Examples</h2>

<h3 id="monitoring-server-health">Monitoring Server Health</h3>
<p>Create a simple script to check server health every minute:</p>
<pre><code>#!/bin/bash
while true; do
response=$(curl -s "http://your-server-address/api/health")
status=$(echo $response | jq -r '.status')
timestamp=$(echo $response | jq -r '.timestamp')

echo "[$timestamp] Server status: $status"
sleep 60
done</code></pre>

<h3 id="analyzing-performance-trends">Analyzing Performance Trends</h3>
<p>Collect performance data over time:</p>
<pre><code>#!/bin/bash
token="YOUR_JWT_TOKEN_HERE"
server="http://your-server-address"

for i in {1..60}; do
timestamp=
(date+"response=
(curl -s -H "Authorization: Bearer $token" "$server/api/system/performance")

cpu=
(echo $response | jq -r '.process.cpuUsagePercentage')
memory=
(echo $response | jq -r '.process.memory.workingSetMB')

echo "$timestamp,$cpu,$memory" >> performance_log.csv
sleep 60
done</code></pre>

<h3 id="security-audit">Security Audit</h3>
<p>Scan for open ports on your server:</p>
<pre><code>#!/bin/bash
token="YOUR_JWT_TOKEN_HERE"
server="http://your-server-address"
host="your-server-address"

response=$(curl -s -H "Authorization: Bearer $token" "$server/api/network/scan?host=$host&ports=1-65535")
echo $response | jq -r '.openPorts[] | "Port: (.port), Open: (.isOpen)"'</code></pre>

<h2 id="troubleshooting">Troubleshooting</h2>

<h3 id="plugin-fails-to-load">Plugin Fails to Load</h3>
<ul>
<li>Check the server logs for any error messages during plugin loading.</li>
<li>Ensure the plugin file is not corrupted.</li>
<li>Verify that the server has permission to access the plugin file.</li>
</ul>

<h3 id="authentication-issues">Authentication Issues</h3>
<ul>
<li>Verify that your JWT token is valid and not expired.</li>
<li>Check that the token is properly included in the Authorization header.</li>
<li>Ensure the token has the necessary permissions.</li>
</ul>

<h3 id="performance-issues">Performance Issues</h3>
<ul>
<li>Consider increasing the cache duration for frequently accessed data.</li>
<li>Adjust the rate limiting settings if you're making many requests.</li>
<li>Be mindful of the impact of resource-intensive operations like network scans.</li>
</ul>

<h3 id="file-access-issues">File Access Issues</h3>
<ul>
<li>Ensure the server process has permission to access the requested files.</li>
<li>Check that the file paths are correctly formatted for the operating system.</li>
<li>Be aware of file size limits when reading files.</li>
</ul>

<h3 id="network-scanning-issues">Network Scanning Issues</h3>
<ul>
<li>Some hosts may block port scans, resulting in false negatives.</li>
<li>Adjust the timeout value for slower networks.</li>
<li>Be aware that scanning many ports can take a significant amount of time.</li>
</ul>
</div>
