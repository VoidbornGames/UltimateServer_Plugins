<div class="container">
<h1 id="superlib-plugin">SuperLib Plugin</h1>
<p>A simple monitoring and system information plugin for the <strong>UltimateServer</strong>. This plugin provides a set of useful API endpoints to check server health, view performance metrics, and inspect network details without requiring any external dependencies.</p>
   
<h2 id="table-of-contents">Table of Contents</h2>
<ul>
<li><a href="#features">Features</a></li>
<li><a href="#prerequisites">Prerequisites</a></li>
<li><a href="#installation">Installation</a></li>
<li><a href="#configuration">Configuration</a></li>
<li><a href="#authentication">Authentication</a></li>
<li><a href="#api-endpoints">API Endpoints</a></li>
</ul>

<h2 id="features">Features</h2>
<div class="feature-list">
<div class="feature-card">
<h3 id="-health-check-endpoint">🩺 Health Check Endpoint</h3>
<p>A simple, public endpoint to verify that the server and plugin are running.</p>
</div>
<div class="feature-card">
<h3 id="-system-information">💻 System Information</h3>
<p>Get detailed information about the server's operating system, hardware, and the server process itself.</p>
</div>
<div class="feature-card">
<h3 id="-performance-metrics">📈 Performance Metrics</h3>
<p>Monitor real-time CPU usage, memory consumption, and disk space for all fixed drives.</p>
</div>
<div class="feature-card">
<h3 id="-network-information">🌐 Network Information</h3>
<p>View details about active network interfaces and the total number of active TCP connections.</p>
</div>
<div class="feature-card">
<h3 id="-secure">🔐 Secure</h3>
<p>All sensitive endpoints are protected using the same JWT (JSON Web Token) authentication as the core server.</p>
</div>
</div>

<h2 id="prerequisites">Prerequisites</h2>
<ul>
<li>A running instance of the <strong>Ultimate C# Server</strong>.</li>
<li>The server must have the plugin system enabled.</li>
</ul>

<h2 id="installation">Installation</h2>
<ol>
<li><strong>Download</strong>: Download <code>SuperLib.dll</code> into one of your directories.</li>
<li><strong>Upload</strong>: Go to the UltimateServer dashboard in the plugins tab, in upload section click on the <code>Browse Files</code> and choose the <code>SuperLib.dll</code> you downloaded. The plugin manager will automatically detect and load <code>SuperLib</code>.</li>
</ol>
<p>You should see a log message indicating that the plugin has loaded successfully and registered its API routes.</p>

<h2 id="configuration">Configuration</h2>
<p>The plugin includes a <code>Config</code> class with placeholder settings. While these are not currently used by the API logic, they are good practice for future expansion.</p>
<pre><code>public class Config
{
    public string API_Username { get; set; } = "admin";
    public string API_Password { get; set; } = "password";
}</code></pre>

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
<p>A simple health check endpoint used to verify that the server and the plugin are running correctly.</p>
<ul>
<li><strong>Authentication</strong>: <code>None</code></li>
</ul>
<p><strong>Example Request</strong>:</p>
<pre><code>curl -X GET "http://your-server-address/api/health"</code></pre>
<p><strong>Example Response</strong>:</p>
<pre><code>{
  "status": "OK",
  "timestamp": "2023-11-28T12:00:00.123Z"
}</code></pre>

<h3 id="get-apisysteminfo">GET /api/system/info</h3>
<p>Retrieves basic system and process information, including OS version, machine name, and process details.</p>
<ul>
<li><strong>Authentication</strong>: <code>Required (Bearer Token)</code></li>
</ul>
<p><strong>Example Request</strong>:</p>
<pre><code>curl -X GET "http://your-server-address/api/system/info" -H "Authorization: Bearer YOUR_JWT_TOKEN_HERE"</code></pre>
<p><strong>Example Response</strong>:</p>
<pre><code>{
  "osVersion": "Microsoft Windows NT 10.0.19045.0",
  "machineName": "DESKTOP-ABC123",
  "processorCount": 8,
  "process": {
    "id": 12345,
    "startTime": "2023-11-28T10:30:00.123Z",
    "threadCount": 25,
    "handleCount": 510
  }
}</code></pre>

<h3 id="get-apisystemperformance">GET /api/system/performance</h3>
<p>Provides real-time performance metrics, including current CPU usage, memory usage, and disk space statistics.</p>
<ul>
<li><strong>Authentication</strong>: <code>Required (Bearer Token)</code></li>
</ul>
<p><strong>Example Request</strong>:</p>
<pre><code>curl -X GET "http://your-server-address/api/system/performance" -H "Authorization: Bearer YOUR_JWT_TOKEN_HERE"</code></pre>
<p><strong>Example Response</strong>:</p>
<pre><code>{
  "cpu": {
    "usagePercentage": 15.75
  },
  "memory": {
    "workingSetMB": 512,
    "managedMemoryMB": 128
  },
  "disk": [
    {
      "name": "C:\\",
      "label": "Windows",
      "totalSizeGB": 475.89,
      "freeSpaceGB": 120.55,
      "usedPercentage": 74.68
    }
  ]
}</code></pre>

<h3 id="get-apinetworkinfo">GET /api/network/info</h3>
<p>Returns information about the server's active network interfaces and the current number of TCP connections.</p>
<ul>
<li><strong>Authentication</strong>: <code>Required (Bearer Token)</code></li>
</ul>
<p><strong>Example Request</strong>:</p>
<pre><code>curl -X GET "http://your-server-address/api/network/info" -H "Authorization: Bearer YOUR_JWT_TOKEN_HERE"</code></pre>
<p><strong>Example Response</strong>:</p>
<pre><code>{
  "activeTcpConnections": 12,
  "interfaces": [
    {
      "name": "Ethernet",
      "type": "Ethernet",
      "speed": 1000,
      "status": "Up",
      "ipAddresses": [
        "192.168.1.10"
      ]
    }
  ]
}</code></pre>
</div>
