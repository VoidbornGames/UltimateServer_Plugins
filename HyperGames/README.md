<div class="container">
        <header>
            <h1>HyperGames Plugin</h1>
            <p>A powerful plugin for UltimateServer that provides a web-based interface for creating, managing, and monitoring game servers. Features a modern dashboard, asynchronous server installation, and a template-based system for easy deployment.</p>
        </header>

<nav>
            <h2>Table of Contents</h2>
            <ul>
                <li><a href="#features">Features</a></li>
                <li><a href="#requirements">Requirements</a></li>
                <li><a href="#installation">Installation</a></li>
                <li><a href="#usage">Usage</a></li>
                <li><a href="#api-endpoints">API Endpoints</a></li>
                <li><a href="#server-templates">Server Templates</a></li>
                <li><a href="#plugin-development">Plugin Development</a></li>
                <li><a href="#troubleshooting">Troubleshooting</a></li>
                <li><a href="#license">License</a></li>
            </ul>
        </nav>

 <main>
            <section id="features">
                <h2>Features</h2>
                <div class="feature-card">
                    <h3>🎮 Game Server Management</h3>
                    <ul>
                        <li><strong>Web-based Dashboard:</strong> A modern, responsive interface for managing all your game servers from one place.</li>
                        <li><strong>Server Lifecycle:</strong> Create, start, stop, and uninstall servers with simple clicks.</li>
                        <li><strong>Real-time Status:</strong> Monitor server status (running, stopped, installing, error) in real-time.</li>
                        <li><strong>Resource Control:</strong> Configure RAM limits and port assignments for each server instance.</li>
                    </ul>
                </div>
                <div class="feature-card">
                    <h3>🔐 Security & Authentication</h3>
                    <ul>
                        <li><strong>Secure Login:</strong> Integrates with UltimateServer's user authentication service using JWT tokens.</li>
                        <li><strong>Protected APIs:</strong> All management endpoints are secured to ensure only authorized users can make changes.</li>
                    </ul>
                </div>
                <div class="feature-card">
                    <h3>⚙️ Template System</h3>
                    <ul>
                        <li><strong>Reusable Templates:</strong> Quickly deploy new servers using pre-configured templates.</li>
                        <li><strong>Extensible Design:</strong> The system is designed for easy addition of new game server templates.</li>
                    </ul>
                </div>
                <div class="feature-card">
                    <h3>🔄 Asynchronous Processing</h3>
                    <ul>
                        <li><strong>Non-blocking Installation:</strong> Server installations are handled in the background, keeping the UI responsive.</li>
                        <li><strong>Job Queue:</strong> A robust queue system manages multiple installation requests efficiently.</li>
                    </ul>
                </div>
            </section>

   <section id="requirements">
                <h2>Requirements</h2>
                <ul>
                    <li>UltimateServer (7.10.5+)</li>
                    <li>.NET 8.0 or higher runtime</li>
                    <li>Java Runtime Environment (JRE) for running Minecraft servers</li>
                    <li>Sufficient disk space for game server files</li>
                    <li>Administrative privileges (for port binding and process management)</li>
                </ul>
            </section>

<section id="installation">
                <h2>Installation</h2>
                <ol>
                    <li>In UltimateServer Go To Market Tab, Find HyperGames And Click Install.</li>
                </ol>
            </section>

  <section id="usage">
                <h2>Usage</h2>
                <h3>Web Interface</h3>
                <ol>
                    <li><strong>Login:</strong> Navigate to the web interface and log in with your UltimateServer credentials.</li>
                    <li><strong>Dashboard:</strong> The main page shows an overview of all your game servers, their status, and key statistics.</li>
                    <li><strong>Create a Server:</strong>
                        <ul>
                            <li>Navigate to the "Servers" page.</li>
                            <li>Click the "Create Server" button.</li>
                            <li>Fill in the details: Server Name, Max RAM, Allowed Ports, and select a Server Template.</li>
                            <li>Click "Create Server". The installation will begin in the background.</li>
                        </ul>
                    </li>
                    <li><strong>Manage Servers:</strong> From the server list, you can start, stop, or uninstall any server using the action buttons on each server card.</li>
                </ol>
            </section>

<section id="api-endpoints">
                <h2>API Endpoints</h2>
                <p>The plugin exposes RESTful API endpoints for server management. All endpoints (except <code>/login</code>) require a Bearer token in the <code>Authorization</code> header.</p>
                <table>
                    <thead>
                        <tr>
                            <th>Endpoint</th>
                            <th>Method</th>
                            <th>Description</th>
                        </tr>
                    </thead>
                    <tbody>
                        <tr>
                            <td><code>/api/HyperGames/login</code></td>
                            <td>POST</td>
                            <td>Authenticates a user and returns a JWT token.</td>
                        </tr>
                        <tr>
                            <td><code>/api/HyperGames/logout</code></td>
                            <td>POST</td>
                            <td>Invalidates the current session (placeholder).</td>
                        </tr>
                        <tr>
                            <td><code>/api/HyperGames/servers</code></td>
                            <td>GET</td>
                            <td>Retrieves a list of all configured game servers.</td>
                        </tr>
                        <tr>
                            <td><code>/api/HyperGames/servers</code></td>
                            <td>POST</td>
                            <td>Creates and starts the installation of a new game server.</td>
                        </tr>
                        <tr>
                            <td><code>/api/HyperGames/servers/start</code></td>
                            <td>POST</td>
                            <td>Starts a specific server by name.</td>
                        </tr>
                        <tr>
                            <td><code>/api/HyperGames/servers/stop</code></td>
                            <td>POST</td>
                            <td>Stops a specific server by name.</td>
                        </tr>
                        <tr>
                            <td><code>/api/HyperGames/servers/uninstall</code></td>
                            <td>POST</td>
                            <td>Stops and completely removes a server and its files.</td>
                        </tr>
                        <tr>
                            <td><code>/api/HyperGames/templates</code></td>
                            <td>GET</td>
                            <td>Lists all available server templates.</td>
                        </tr>
                    </tbody>
                </table>
            </section>

  <section id="server-templates">
                <h2>Server Templates</h2>
                <p>HyperGames uses a template system to define how different game servers are installed and managed.</p>
                <h3>Minecraft Server Template</h3>
                <p>The plugin includes a template for Minecraft Paper servers with the following features:</p>
                <ul>
                    <li><strong>Version:</strong> Minecraft Paper 1.20.4</li>
                    <li><strong>Installation:</strong> Automatically downloads the server JAR file.</li>
                    <li><strong>Configuration:</strong> Pre-configures <code>eula.txt</code> and basic <code>server.properties</code>.</li>
                </ul>

 <h3>Creating Custom Templates</h3>
                <p>To add support for a new game, you can create a custom template by implementing the <code>IServerTemplate</code> interface.</p>
                <pre><code>
public class CustomGameTemplate : IServerTemplate
{
    public string Name { get; set; } = "Custom Game Server";
    public string Version { get; set; } = "1.0.0";
    public int[] AllowedPorts { get; set; }
    public int MaxRamMB { get; set; }
    public string ServerPath { get; set; }
    public Process Process { get; set; }
    // Methods
    public async Task InstallServerFiles() { /* ... */ }
    public async Task RunServer() { /* ... */ }
    public async Task StopServer() { /* ... */ }
    public async Task UninstallServer() { /* ... */ }
    // ... other required members
}</code></pre>
            </section>

<section id="plugin-development">
                <h2>Plugin Development</h2>
                <p>The plugin is structured for easy extension:</p>
                <ul>
                    <li><strong>Main Class:</strong> <code>HyperGames.cs</code> handles plugin lifecycle, API routing, and job processing.</li>
                    <li><strong>Models:</strong> <code>GameServer.cs</code> defines the data model for a server instance.</li>
                    <li><strong>Templates:</strong> The <code>MinecraftServerTemplate.cs</code> file provides an example implementation of <code>IServerTemplate</code>.</li>
                    <li><strong>Frontend:</strong> The web UI is a single-page application embedded within the C# code, which can be customized or extracted.</li>
                </ul>
                <p>To add new features, you can register new API routes in the <code>OnLoadAsync</code> method and implement new server templates.</p>
            </section>

<section id="troubleshooting">
                <h2>Troubleshooting</h2>
                <h3>Server Installation Fails</h3>
                <ul>
                    <li>Check if the server has enough disk space.</li>
                    <li>Verify network connectivity for downloading server files.</li>
                    <li>Ensure Java is installed and correctly configured in the system's PATH.</li>
                </ul>
                <h3>Server Won't Start</h3>
                <ul>
                    <li>Check the UltimateServer logs for detailed error messages.</li>
                    <li>Verify that the allocated RAM is sufficient.</li>
                    <li>Ensure the specified ports are not already in use by another application.</li>
                </ul>
                <h3>Authentication Issues</h3>
                <ul>
                    <li>Confirm that UltimateServer's core authentication service is running.</li>
                    <li>Ensure you are using valid UltimateServer user credentials.</li>
                </ul>
            </section>

<section id="license">
                <h2>License</h2>
                <p>This plugin is released under the MIT License. See the LICENSE file for more details.</p>
            </section>
        </main>
    </div>
