using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text;
using UltimateServer.Models;
using UltimateServer.Plugins;
using UltimateServer.Servers;
using UltimateServer.Services;

namespace HyperGames
{
    public class HyperGames : IPlugin
    {
        public string Name => "HyperGames";
        public string Version => "1.0.0";

        private IPluginContext _context;
        private AuthenticationService _authService;
        private UserService _userService;

        private List<GameServer> _servers = new List<GameServer>();
        private List<IServerTemplate> _templates = new List<IServerTemplate>();

        private readonly ConcurrentQueue<GameServer> _jobQueue = new ConcurrentQueue<GameServer>();
        private readonly SemaphoreSlim _jobSignal = new SemaphoreSlim(0);
        private readonly CancellationTokenSource _cancel = new CancellationTokenSource();
        private Task _jobTask;

        private string serversFile = Path.Combine(AppContext.BaseDirectory, "plugins", "HyperGames", "servers.json");
        private string frontend = @"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>HyperServer Panel</title>
    <link href=""https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.min.css"" rel=""stylesheet"">
    <link rel=""stylesheet"" href=""https://cdn.jsdelivr.net/npm/bootstrap-icons@1.10.0/font/bootstrap-icons.css"">
    <style>
        :root {
            --bg-primary: #121212;
            --bg-secondary: #1e1e1e;
            --bg-tertiary: #2d2d2d;
            --text-primary: #ffffff;
            --text-secondary: #b3b3b3;
            --accent: #6200ee;
            --accent-hover: #7c4dff;
            --success: #03dac6;
            --warning: #fbc02d;
            --danger: #cf6679;
        }

        body {
            background-color: var(--bg-primary);
            color: var(--text-primary);
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
        }

        .server-card {
            transition: transform 0.2s;
            border-left: 4px solid #6c757d;
            background-color: var(--bg-secondary);
            border: none;
            box-shadow: 0 4px 8px rgba(0,0,0,0.2);
        }
        
        .server-card.running {
            border-left-color: var(--success);
        }
        
        .server-card.stopped {
            border-left-color: var(--danger);
        }
        
        .server-card.installing, .server-card.error {
            border-left-color: var(--warning);
        }
        
        .server-card:hover {
            transform: translateY(-5px);
            box-shadow: 0 10px 20px rgba(0,0,0,0.3);
        }
        
        .status-running {
            color: var(--success);
        }
        
        .status-stopped {
            color: var(--danger);
        }

        .server-actions button {
            margin-right: 5px;
        }
        
        .hidden {
            display: none !important;
        }
        
        .login-container {
            min-height: 100vh;
            display: flex;
            align-items: center;
            justify-content: center;
            background: linear-gradient(135deg, #1a237e 0%, #311b92 100%);
        }
        
        .login-card {
            border-radius: 15px;
            box-shadow: 0 15px 35px rgba(0,0,0,0.3);
            overflow: hidden;
            background-color: var(--bg-secondary);
        }
        
        .login-header {
            background: linear-gradient(135deg, #1a237e 0%, #311b92 100%);
            color: white;
            padding: 2rem;
            text-align: center;
        }
        
        .nav-link.active {
            font-weight: bold;
            color: var(--accent) !important;
        }
        
        .nav-link {
            color: var(--text-secondary) !important;
        }
        
        .nav-link:hover {
            color: var(--text-primary) !important;
        }
        
        .toast-container {
            position: fixed;
            top: 20px;
            right: 20px;
            z-index: 1050;
        }
        
        .spinner-border-sm {
            width: 1rem;
            height: 1rem;
        }
        
        .server-status {
            position: absolute;
            top: 10px;
            right: 10px;
            width: 12px;
            height: 12px;
            border-radius: 50%;
        }
        
        .server-status.running {
            background-color: var(--success);
            box-shadow: 0 0 10px var(--success);
        }
        
        .server-status.stopped {
            background-color: var(--danger);
            box-shadow: 0 0 10px var(--danger);
        }

        .server-status.installing, .server-status.error {
            background-color: var(--warning);
            box-shadow: 0 0 10px var(--warning);
        }

        /* Dark theme overrides for Bootstrap */
        .navbar-dark {
            background-color: var(--bg-secondary) !important;
        }
        
        .card {
            background-color: var(--bg-secondary) !important;
            border: none !important;
        }
        
        .card-header {
            background-color: var(--bg-tertiary) !important;
            border-bottom: 1px solid rgba(255,255,255,0.1) !important;
        }
        
        .modal-content {
            background-color: var(--bg-secondary) !important;
            color: var(--text-primary) !important;
        }
        
        .modal-header {
            border-bottom: 1px solid rgba(255,255,255,0.1) !important;
        }
        
        .modal-footer {
            border-top: 1px solid rgba(255,255,255,0.1) !important;
        }
        
        .form-control {
            background-color: var(--bg-tertiary) !important;
            border: 1px solid rgba(255,255,255,0.2) !important;
            color: var(--text-primary) !important;
        }
        
        .form-control:focus {
            background-color: var(--bg-tertiary) !important;
            border-color: var(--accent) !important;
            color: var(--text-primary) !important;
            box-shadow: 0 0 0 0.25rem rgba(98, 0, 238, 0.25) !important;
        }
        
        .form-select {
            background-color: var(--bg-tertiary) !important;
            border: 1px solid rgba(255,255,255,0.2) !important;
            color: var(--text-primary) !important;
        }
        
        .form-select:focus {
            background-color: var(--bg-tertiary) !important;
            border-color: var(--accent) !important;
            color: var(--text-primary) !important;
            box-shadow: 0 0 0 0.25rem rgba(98, 0, 238, 0.25) !important;
        }
        
        .btn-primary {
            background-color: var(--accent) !important;
            border-color: var(--accent) !important;
        }
        
        .btn-primary:hover {
            background-color: var(--accent-hover) !important;
            border-color: var(--accent-hover) !important;
        }
        
        .btn-success {
            background-color: var(--success) !important;
            border-color: var(--success) !important;
        }
        
        .btn-danger {
            background-color: var(--danger) !important;
            border-color: var(--danger) !important;
        }
        
        .btn-warning {
            background-color: var(--warning) !important;
            border-color: var(--warning) !important;
            color: var(--bg-primary) !important;
        }
        
        .dropdown-menu {
            background-color: var(--bg-secondary) !important;
            border: 1px solid rgba(255,255,255,0.1) !important;
        }
        
        .dropdown-item {
            color: var(--text-primary) !important;
        }
        
        .dropdown-item:hover {
            background-color: var(--bg-tertiary) !important;
        }
        
        .dropdown-divider {
            border-color: rgba(255,255,255,0.1) !important;
        }
        
        .text-white {
            color: var(--text-primary) !important;
        }
        
        .text-muted {
            color: var(--text-secondary) !important;
        }
        
        .bg-primary {
            background-color: var(--accent) !important;
        }
        
        .bg-success {
            background-color: var(--success) !important;
        }
        
        .bg-danger {
            background-color: var(--danger) !important;
        }
        
        .toast {
            background-color: var(--bg-secondary) !important;
            color: var(--text-primary) !important;
        }
        
        .toast-header {
            background-color: var(--bg-tertiary) !important;
            border-bottom: 1px solid rgba(255,255,255,0.1) !important;
        }
    </style>
</head>
<body>
    <!-- Login Page -->
    <div id=""login-page"" class=""login-container"">
        <div class=""container"">
            <div class=""row justify-content-center"">
                <div class=""col-md-6 col-lg-4"">
                    <div class=""login-card"">
                        <div class=""login-header"">
                            <h2><i class=""bi bi-server""></i> HyperServer Panel</h2>
                            <p class=""mb-0"">Manage your game servers</p>
                        </div>
                        <div class=""card-body p-4"">
                            <form id=""login-form"">
                                <div class=""mb-3"">
                                    <label for=""username"" class=""form-label"">Username</label>
                                    <div class=""input-group"">
                                        <span class=""input-group-text""><i class=""bi bi-person""></i></span>
                                        <input type=""text"" class=""form-control"" id=""username"" required>
                                    </div>
                                </div>
                                <div class=""mb-3"">
                                    <label for=""password"" class=""form-label"">Password</label>
                                    <div class=""input-group"">
                                        <span class=""input-group-text""><i class=""bi bi-lock""></i></span>
                                        <input type=""password"" class=""form-control"" id=""password"" required>
                                    </div>
                                </div>
                                <div class=""mb-3 form-check"">
                                    <input type=""checkbox"" class=""form-check-input"" id=""remember-me"">
                                    <label class=""form-check-label"" for=""remember-me"">Remember me</label>
                                </div>
                                <div class=""d-grid"">
                                    <button type=""submit"" class=""btn btn-primary"" id=""login-btn"">
                                        <span class=""spinner-border spinner-border-sm hidden"" role=""status""></span>
                                        Login
                                    </button>
                                </div>
                            </form>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    </div>

    <!-- Main Application -->
    <div id=""app-container"" class=""hidden"">
        <!-- Navigation -->
        <nav class=""navbar navbar-expand-lg navbar-dark bg-dark"">
            <div class=""container-fluid"">
                <a class=""navbar-brand"" href=""#"">
                    <i class=""bi bi-server""></i> HyperServer Panel
                </a>
                <button class=""navbar-toggler"" type=""button"" data-bs-toggle=""collapse"" data-bs-target=""#navbarNav"">
                    <span class=""navbar-toggler-icon""></span>
                </button>
                <div class=""collapse navbar-collapse"" id=""navbarNav"">
                    <ul class=""navbar-nav me-auto"">
                        <li class=""nav-item"">
                            <a class=""nav-link active"" href=""#"" data-page=""dashboard"">
                                <i class=""bi bi-speedometer2""></i> Dashboard
                            </a>
                        </li>
                        <li class=""nav-item"">
                            <a class=""nav-link"" href=""#"" data-page=""servers"">
                                <i class=""bi bi-hdd-stack""></i> Servers
                            </a>
                        </li>
                    </ul>
                    <ul class=""navbar-nav"">
                        <li class=""nav-item dropdown"">
                            <a class=""nav-link dropdown-toggle"" href=""#"" id=""navbarDropdown"" role=""button"" data-bs-toggle=""dropdown"">
                                <i class=""bi bi-person-circle""></i> <span id=""username-display"">Loading...</span>
                            </a>
                            <ul class=""dropdown-menu"">
                                <li><a class=""dropdown-item"" href=""#"" id=""logout-link"">
                                    <i class=""bi bi-box-arrow-right""></i> Logout
                                </a></li>
                            </ul>
                        </li>
                    </ul>
                </div>
            </div>
        </nav>

        <!-- Content Area -->
        <div class=""container-fluid mt-4"">
            <!-- Dashboard Page -->
            <div id=""dashboard-page"" class=""page-content"">
                <div class=""row mb-4"">
                    <div class=""col-md-4"">
                        <div class=""card text-white bg-primary mb-3"">
                            <div class=""card-body"">
                                <div class=""d-flex justify-content-between"">
                                    <div>
                                        <h4 class=""card-title"">Total Servers</h4>
                                        <h2 id=""total-servers"">0</h2>
                                    </div>
                                    <div class=""align-self-center"">
                                        <i class=""bi bi-hdd-stack fs-1""></i>
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>
                    <div class=""col-md-4"">
                        <div class=""card text-white bg-success mb-3"">
                            <div class=""card-body"">
                                <div class=""d-flex justify-content-between"">
                                    <div>
                                        <h4 class=""card-title"">Running Servers</h4>
                                        <h2 id=""running-servers"">0</h2>
                                    </div>
                                    <div class=""align-self-center"">
                                        <i class=""bi bi-play-circle fs-1""></i>
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>
                    <div class=""col-md-4"">
                        <div class=""card text-white bg-danger mb-3"">
                            <div class=""card-body"">
                                <div class=""d-flex justify-content-between"">
                                    <div>
                                        <h4 class=""card-title"">Stopped Servers</h4>
                                        <h2 id=""stopped-servers"">0</h2>
                                    </div>
                                    <div class=""align-self-center"">
                                        <i class=""bi bi-stop-circle fs-1""></i>
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
                
                <div class=""row"">
                    <div class=""col-12"">
                        <h3>Recent Servers</h3>
                        <div id=""recent-servers"" class=""row"">
                            <!-- Server cards will be added here dynamically -->
                        </div>
                    </div>
                </div>
            </div>

            <!-- Servers Page -->
            <div id=""servers-page"" class=""page-content hidden"">
                <div class=""d-flex justify-content-between align-items-center mb-4"">
                    <h2>Servers</h2>
                    <button class=""btn btn-primary"" id=""create-server-btn"">
                        <i class=""bi bi-plus-circle""></i> Create Server
                    </button>
                </div>
                
                <div id=""servers-container"" class=""row"">
                    <!-- Server cards will be added here dynamically -->
                </div>
            </div>
        </div>
    </div>

    <!-- Create Server Modal -->
    <div class=""modal fade"" id=""createServerModal"" tabindex=""-1"">
        <div class=""modal-dialog"">
            <div class=""modal-content"">
                <div class=""modal-header"">
                    <h5 class=""modal-title"">Create New Server</h5>
                    <button type=""button"" class=""btn-close"" data-bs-dismiss=""modal""></button>
                </div>
                <div class=""modal-body"">
                    <form id=""create-server-form"">
                        <div class=""mb-3"">
                            <label for=""server-name"" class=""form-label"">Server Name</label>
                            <input type=""text"" class=""form-control"" id=""server-name"" required>
                        </div>
                        <div class=""mb-3"">
                            <label for=""server-ram"" class=""form-label"">Max RAM (MB)</label>
                            <input type=""number"" class=""form-control"" id=""server-ram"" required>
                        </div>
                        <div class=""mb-3"">
                            <label for=""server-ports"" class=""form-label"">Allowed Ports (comma-separated)</label>
                            <input type=""text"" class=""form-control"" id=""server-ports"" placeholder=""25565,25566"">
                        </div>
                        <div class=""mb-3"">
                            <label for=""server-template"" class=""form-label"">Server Template</label>
                            <select class=""form-select"" id=""server-template"" required>
                                <option value="""">Select a template...</option>
                                <!-- Options will be populated dynamically -->
                            </select>
                        </div>
                    </form>
                </div>
                <div class=""modal-footer"">
                    <button type=""button"" class=""btn btn-secondary"" data-bs-dismiss=""modal"">Cancel</button>
                    <button type=""button"" class=""btn btn-primary"" id=""save-server-btn"">
                        <span class=""spinner-border spinner-border-sm hidden"" role=""status""></span>
                        Create Server
                    </button>
                </div>
            </div>
        </div>
    </div>

    <!-- Toast Container -->
    <div class=""toast-container"">
        <div id=""toast"" class=""toast"" role=""alert"">
            <div class=""toast-header"">
                <strong class=""me-auto"" id=""toast-title"">Notification</strong>
                <button type=""button"" class=""btn-close"" data-bs-dismiss=""toast""></button>
            </div>
            <div class=""toast-body"" id=""toast-message"">
                <!-- Toast message will be displayed here -->
            </div>
        </div>
    </div>

    <script src=""https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/js/bootstrap.bundle.min.js""></script>
    <script>
        // Global variables
        let authToken = localStorage.getItem('authToken');
        let currentUser = null;
        let servers = [];
        let templates = [];

        // DOM elements
        const loginPage = document.getElementById('login-page');
        const appContainer = document.getElementById('app-container');
        const usernameDisplay = document.getElementById('username-display');
        const pageContents = document.querySelectorAll('.page-content');
        const navLinks = document.querySelectorAll('.nav-link[data-page]');

        // Check if user is logged in
        async function checkAuth() {
            if (!authToken) {
                showLogin();
                return;
            }

            try {
                // We'll just validate the token by making a simple API call
                const response = await apiRequest('/api/HyperGames/servers', 'GET');
                
                if (response && response.success) {
                    // Token is valid
                    const username = localStorage.getItem('username') || 'User';
                    usernameDisplay.textContent = username;
                    showApp();
                    await loadDashboard();
                } else {
                    localStorage.removeItem('authToken');
                    localStorage.removeItem('username');
                    authToken = null;
                    showLogin();
                }
            } catch (error) {
                console.error('Error checking auth:', error);
                localStorage.removeItem('authToken');
                localStorage.removeItem('username');
                authToken = null;
                showLogin();
            }
        }

        // Show login page
        function showLogin() {
            loginPage.classList.remove('hidden');
            appContainer.classList.add('hidden');
        }

        // Show main application
        function showApp() {
            loginPage.classList.add('hidden');
            appContainer.classList.remove('hidden');
        }

        // Show a specific page
        function showPage(pageName) {
            // Hide all pages
            pageContents.forEach(page => page.classList.add('hidden'));
            
            // Remove active class from all nav links
            navLinks.forEach(link => link.classList.remove('active'));
            
            // Show the requested page
            document.getElementById(`${pageName}-page`).classList.remove('hidden');
            
            // Add active class to the corresponding nav link
            document.querySelector(`.nav-link[data-page=""${pageName}""]`).classList.add('active');
            
            // Load page-specific data
            if (pageName === 'servers') {
                loadServers();
            }
        }

        // Make API request
        async function apiRequest(endpoint, method = 'GET', data = null) {
            const options = {
                method,
                headers: {
                    'Content-Type': 'application/json'
                }
            };
            
            // Add authorization header for all requests except login
            if (endpoint !== '/api/HyperGames/login') {
                options.headers['Authorization'] = `Bearer ${authToken}`;
            }
            
            if (data) {
                options.body = JSON.stringify(data);
            }
            
            try {
                const response = await fetch(endpoint, options);
                
                // Check if response is OK
                if (!response.ok) {
                    // If unauthorized, clear token and redirect to login
                    if (response.status === 401) {
                        localStorage.removeItem('authToken');
                        localStorage.removeItem('username');
                        authToken = null;
                        showLogin();
                        throw new Error('Authentication failed');
                    }
                    
                    // Try to get error message from response
                    let errorMessage = 'Request failed';
                    try {
                        const responseText = await response.text();
                        
                        // Check if response is empty
                        if (!responseText || responseText.trim() === '') {
                            errorMessage = 'Server returned empty response';
                        } else {
                            try {
                                const errorData = JSON.parse(responseText);
                                errorMessage = errorData.message || errorMessage;
                            } catch (jsonError) {
                                // If we can't parse the JSON, use the raw text
                                errorMessage = responseText;
                            }
                        }
                    } catch (e) {
                        // If we can't get the response text, use the status text
                        errorMessage = response.statusText || errorMessage;
                    }
                    
                    throw new Error(errorMessage);
                }
                
                const responseText = await response.text();
                
                // Check if response is empty
                if (!responseText || responseText.trim() === '') {
                    throw new Error('Server returned empty response');
                }
                
                try {
                    return JSON.parse(responseText);
                } catch (jsonError) {
                    console.error('JSON parse error:', jsonError);
                    console.error('Response text:', responseText);
                    throw new Error('Invalid JSON response from server');
                }
            } catch (error) {
                console.error('API request error:', error);
                throw error;
            }
        }

        // Show toast notification
        function showToast(title, message, type = 'info') {
            const toastElement = document.getElementById('toast');
            const toastTitle = document.getElementById('toast-title');
            const toastMessage = document.getElementById('toast-message');
            
            toastTitle.textContent = title;
            toastMessage.textContent = message;
            
            // Set toast color based on type
            toastElement.className = 'toast';
            if (type === 'success') {
                toastElement.classList.add('bg-success', 'text-white');
            } else if (type === 'error') {
                toastElement.classList.add('bg-danger', 'text-white');
            } else if (type === 'warning') {
                toastElement.classList.add('bg-warning', 'text-dark');
            }
            
            const toast = new bootstrap.Toast(toastElement);
            toast.show();
        }

        // Toggle loading state
        function toggleLoading(buttonId, loading = true) {
            const button = document.getElementById(buttonId);
            const spinner = button.querySelector('.spinner-border');
            
            if (loading) {
                button.disabled = true;
                spinner.classList.remove('hidden');
            } else {
                button.disabled = false;
                spinner.classList.add('hidden');
            }
        }

        // Login form submission
        document.getElementById('login-form').addEventListener('submit', async (e) => {
            e.preventDefault();
            
            const username = document.getElementById('username').value;
            const password = document.getElementById('password').value;
            const rememberMe = document.getElementById('remember-me').checked;
            
            toggleLoading('login-btn', true);
            
            try {
                const response = await apiRequest('/api/HyperGames/login', 'POST', {
                    username: username,
                    password: password,
                    rememberMe: rememberMe
                });
                
                if (response.success) {
                    // Store token and user immediately
                    authToken = response.token;
                    currentUser = response.user;
                    
                    // Always store token
                    localStorage.setItem('authToken', authToken);
                    localStorage.setItem('username', response.user.username || username);
                    
                    // Update UI immediately
                    usernameDisplay.textContent = response.user.username || username;
                    
                    // Force UI update
                    showApp();
                    
                    // Load dashboard
                    await loadDashboard();
                    
                    showToast('Success', 'Login successful', 'success');
                } else {
                    showToast('Error', response.message, 'error');
                }
            } catch (error) {
                console.error('Login error:', error);
                showToast('Error', 'Login failed. Please try again.', 'error');
            } finally {
                toggleLoading('login-btn', false);
            }
        });

        // Logout
        document.getElementById('logout-link').addEventListener('click', async (e) => {
            e.preventDefault();
            
            try {
                await apiRequest('/api/HyperGames/logout', 'POST');
            } catch (error) {
                console.error('Logout error:', error);
            }
            
            localStorage.removeItem('authToken');
            localStorage.removeItem('username');
            authToken = null;
            currentUser = null;
            showLogin();
            showToast('Success', 'Logged out successfully', 'success');
        });

        // Navigation
        document.querySelectorAll('[data-page]').forEach(link => {
            link.addEventListener('click', (e) => {
                e.preventDefault();
                const page = link.getAttribute('data-page');
                showPage(page);
            });
        });

        // Load dashboard data
        async function loadDashboard() {
            try {
                const response = await apiRequest('/api/HyperGames/servers');
                if (response.success) {
                    servers = response.servers;
                    
                    // Update stats
                    const totalServers = servers.length;
                    const runningServers = servers.filter(s => s.Status === 'running').length;
                    const stoppedServers = totalServers - runningServers;
                    
                    document.getElementById('total-servers').textContent = totalServers;
                    document.getElementById('running-servers').textContent = runningServers;
                    document.getElementById('stopped-servers').textContent = stoppedServers;
                    
                    // Show recent servers (up to 6)
                    const recentServersContainer = document.getElementById('recent-servers');
                    recentServersContainer.innerHTML = '';
                    
                    servers.slice(0, 6).forEach(server => {
                        const serverCard = createServerCard(server);
                        recentServersContainer.appendChild(serverCard);
                    });
                }
            } catch (error) {
                console.error('Error loading dashboard:', error);
                showToast('Error', 'Failed to load dashboard data', 'error');
            }
        }

        // Load servers
        async function loadServers() {
            try {
                const response = await apiRequest('/api/HyperGames/servers');
                if (response.success) {
                    servers = response.servers;
                    
                    const serversContainer = document.getElementById('servers-container');
                    serversContainer.innerHTML = '';
                    
                    servers.forEach(server => {
                        const serverCard = createServerCard(server);
                        serversContainer.appendChild(serverCard);
                    });
                }
            } catch (error) {
                console.error('Error loading servers:', error);
                showToast('Error', 'Failed to load servers', 'error');
            }
        }

        // Create server card element
        function createServerCard(server) {
            const col = document.createElement('div');
            col.className = 'col-md-6 col-lg-4 mb-4';
            
            // Determine server status
            const status = server.Status || 'stopped';
            const isInstalling = status === 'installing';
            const isError = status === 'error';
            
            col.innerHTML = `
                <div class=""card server-card ${status}"">
                    <div class=""server-status ${status}""></div>
                    <div class=""card-body"">
                        <h5 class=""card-title"">${server.Name}</h5>
                        <p class=""card-text"">
                            <small class=""text-muted"">Version: ${server.Version}</small><br>
                            <small class=""text-muted"">Path: ${server.Path}</small><br>
                            <small class=""text-muted"">RAM: ${server.MaxRam}MB</small>
                        </p>
                        <div class=""server-actions"">
                            ${isInstalling || isError ? `
                                <span class=""badge bg-warning text-dark"">${isInstalling ? 'Installing...' : 'Error'}</span>
                            ` : status === 'stopped' ? 
                                `<button class=""btn btn-success btn-sm start-server"" data-server=""${server.Name}"">
                                    <i class=""bi bi-play-fill""></i> Start
                                </button>` :
                                `<button class=""btn btn-warning btn-sm stop-server"" data-server=""${server.Name}"">
                                    <i class=""bi bi-stop-fill""></i> Stop
                                </button>`
                            }
                            <button class=""btn btn-danger btn-sm uninstall-server"" data-server=""${server.Name}"" ${isInstalling ? 'disabled' : ''}>
                                <i class=""bi bi-trash""></i> Uninstall
                            </button>
                        </div>
                    </div>
                </div>
            `;
            
            // Add event listeners
            const startBtn = col.querySelector('.start-server');
            const stopBtn = col.querySelector('.stop-server');
            const uninstallBtn = col.querySelector('.uninstall-server');
            
            if (startBtn) {
                startBtn.addEventListener('click', () => startServer(server.Name));
            }
            
            if (stopBtn) {
                stopBtn.addEventListener('click', () => stopServer(server.Name));
            }
            
            uninstallBtn.addEventListener('click', () => uninstallServer(server.Name));
            
            return col;
        }

        // Start server
        async function startServer(serverName) {
            try {
                const response = await apiRequest('/api/HyperGames/servers/start', 'POST', { serverName });
                if (response.success) {
                    showToast('Success', `Server ${serverName} started successfully`, 'success');
                    await loadServers();
                    await loadDashboard();
                } else {
                    showToast('Error', response.message, 'error');
                }
            } catch (error) {
                console.error('Error starting server:', error);
                showToast('Error', 'Failed to start server', 'error');
            }
        }

        // Stop server
        async function stopServer(serverName) {
            try {
                const response = await apiRequest('/api/HyperGames/servers/stop', 'POST', { serverName });
                if (response.success) {
                    showToast('Success', `Server ${serverName} stopped successfully`, 'success');
                    await loadServers();
                    await loadDashboard();
                } else {
                    showToast('Error', response.message, 'error');
                }
            } catch (error) {
                console.error('Error stopping server:', error);
                showToast('Error', 'Failed to stop server', 'error');
            }
        }

        // Uninstall server
        async function uninstallServer(serverName) {
            if (!confirm(`Are you sure you want to uninstall server ${serverName}? This action cannot be undone.`)) {
                return;
            }
            
            try {
                const response = await apiRequest('/api/HyperGames/servers/uninstall', 'POST', { serverName });
                if (response.success) {
                    showToast('Success', `Server ${serverName} uninstalled successfully`, 'success');
                    await loadServers();
                    await loadDashboard();
                } else {
                    showToast('Error', response.message, 'error');
                }
            } catch (error) {
                console.error('Error uninstalling server:', error);
                showToast('Error', 'Failed to uninstall server', 'error');
            }
        }

        // Load templates for create server modal
        async function loadTemplates() {
            try {
                const response = await apiRequest('/api/HyperGames/templates');
                if (response.success) {
                    templates = response.templates;
                    
                    const templateSelect = document.getElementById('server-template');
                    templateSelect.innerHTML = '<option value="""">Select a template...</option>';
                    
                    templates.forEach(template => {
                        const option = document.createElement('option');
                        option.value = template.Name; // Use the Name property
                        option.textContent = `${template.Name} (${template.Version})`; // More descriptive
                        templateSelect.appendChild(option);
                    });
                }
            } catch (error) {
                console.error('Error loading templates:', error);
                showToast('Error', 'Failed to load templates', 'error');
            }
        }

        // Create server modal
        const createServerModal = new bootstrap.Modal(document.getElementById('createServerModal'));
        
        document.getElementById('create-server-btn').addEventListener('click', async () => {
            await loadTemplates();
            createServerModal.show();
        });

        // Create server form submission
        document.getElementById('save-server-btn').addEventListener('click', async () => {
            const serverName = document.getElementById('server-name').value;
            const serverRam = parseInt(document.getElementById('server-ram').value);
            const serverPorts = document.getElementById('server-ports').value
                .split(',')
                .map(port => parseInt(port.trim()))
                .filter(port => !isNaN(port));
            const serverTemplate = document.getElementById('server-template').value;
            
            if (!serverName || !serverRam || !serverTemplate) {
                showToast('Error', 'Please fill in all required fields', 'error');
                return;
            }
            
            toggleLoading('save-server-btn', true);
            
            try {
                const response = await apiRequest('/api/HyperGames/servers', 'POST', {
                    serverName,
                    maxRamMB: serverRam,
                    allowedPorts: serverPorts,
                    templateName: serverTemplate // Send the template name
                });
                
                if (response.success) {
                    showToast('Success', 'Server creation started. It will appear in the list shortly.', 'success');
                    createServerModal.hide();
                    document.getElementById('create-server-form').reset();
                    // Don't reload immediately, wait for the installation to complete
                    // The user will see the ""installing"" status
                } else {
                    showToast('Error', response.message, 'error');
                }
            } catch (error) {
                console.error('Error creating server:', error);
                showToast('Error', 'Failed to create server', 'error');
            } finally {
                toggleLoading('save-server-btn', false);
            }
        });

        // Add a periodic refresh for the dashboard to see installation progress
        setInterval(async () => {
            // Only refresh if the dashboard page is visible
            if (!document.getElementById('dashboard-page').classList.contains('hidden')) {
                await loadDashboard();
            }
        }, 5000); // Refresh every 5 seconds

        // Initialize the application
        checkAuth();
    </script>
</body>
</html>";

        public async Task OnLoadAsync(IPluginContext context)
        {
            _context = context;
            _authService = context.ServiceProvider.GetRequiredService<AuthenticationService>();
            _userService = context.ServiceProvider.GetRequiredService<UserService>();

            Directory.CreateDirectory(Path.GetDirectoryName(serversFile));
            if (File.Exists(serversFile))
            {
                var json = await File.ReadAllTextAsync(serversFile);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    var loaded = JsonConvert.DeserializeObject<List<GameServer>>(json);
                    if (loaded != null)
                    {
                        _servers = loaded;
                        RebuildTemplates();
                    }
                }
            }

            // register templates
            _templates.Add(new MinecraftServerTemplate("/unused", 2048, new[] { 25565 }, context));

            _jobTask = Task.Run(ProcessJobsAsync);

            context.RegisterApiRoute("/HyperGames", ServeDefaultPageAsync);
            context.RegisterApiRoute("/api/HyperGames/login", HandleLoginAsync);
            context.RegisterApiRoute("/api/HyperGames/logout", WithAuth(HandleLogoutAsync));
            context.RegisterApiRoute("/api/HyperGames/servers", WithAuth(HandleServersAsync));
            context.RegisterApiRoute("/api/HyperGames/servers/start", WithAuth(HandleStartAsync));
            context.RegisterApiRoute("/api/HyperGames/servers/stop", WithAuth(HandleStopAsync));
            context.RegisterApiRoute("/api/HyperGames/servers/uninstall", WithAuth(HandleRemoveAsync));
            context.RegisterApiRoute("/api/HyperGames/templates", WithAuth(HandleTemplatesAsync));

            _context.Logger.Log("HyperGames plugin loaded.");
        }

        private void RebuildTemplates()
        {
            foreach (var s in _servers)
            {
                if (s.TemplateType == "MinecraftServerTemplate")
                {
                    s.Template = new MinecraftServerTemplate(
                        s.Path,
                        s.MaxRam,
                        new[] { 25565 },
                        _context
                    );
                }
            }
        }

        private bool IsAuthenticated(HttpListenerRequest req)
        {
            string auth = req.Headers["Authorization"];
            if (auth == null || !auth.StartsWith("Bearer "))
                return false;

            string token = auth.Substring("Bearer ".Length);
            return _authService.ValidateJwtToken(token);
        }

        private Func<HttpListenerRequest, Task> WithAuth(Func<HttpListenerRequest, Task> next)
        {
            return async req =>
            {
                if (!IsAuthenticated(req))
                {
                    var res = HttpContextHolder.CurrentResponse;
                    res.StatusCode = 401;
                    res.ContentType = "application/json";
                    var payload = Encoding.UTF8.GetBytes("{\"success\":false,\"message\":\"Authentication required.\"}");
                    await res.OutputStream.WriteAsync(payload, 0, payload.Length);
                    return;
                }

                await next(req);
            };
        }

        private async Task ServeDefaultPageAsync(HttpListenerRequest request)
        {
            var res = HttpContextHolder.CurrentResponse;
            string htmlPath = Path.Combine(AppContext.BaseDirectory, "plugins", Name, "hyperpanel.html");

            Directory.CreateDirectory(Path.GetDirectoryName(htmlPath));
            if (!File.Exists(htmlPath))
                await File.AppendAllTextAsync(htmlPath, frontend);

            var html = await File.ReadAllTextAsync(htmlPath);
            var buf = Encoding.UTF8.GetBytes(html);
            res.ContentType = "text/html";
            res.ContentLength64 = buf.Length;
            await res.OutputStream.WriteAsync(buf, 0, buf.Length);
        }

        private async Task HandleLoginAsync(HttpListenerRequest request)
        {
            var response = HttpContextHolder.CurrentResponse;

            try
            {
                string requestBody;
                using (var reader = new StreamReader(request.InputStream))
                {
                    requestBody = await reader.ReadToEndAsync();
                }

                var loginData = JsonConvert.DeserializeObject<LoginRequest>(requestBody);
                var auth = await _userService.AuthenticateUserAsync(loginData);
                var user = auth.user;

                if (user != null)
                {
                    // Generate a JWT token using the authentication service
                    string token = _authService.GenerateJwtToken(user);

                    var responseObj = new { success = true, token, user };
                    string jsonResponse = JsonConvert.SerializeObject(responseObj);

                    response.StatusCode = 200;
                    response.ContentType = "application/json";
                    byte[] buffer = Encoding.UTF8.GetBytes(jsonResponse);
                    response.ContentLength64 = buffer.Length;
                    await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                }
                else
                {
                    var responseObj = new { success = false, message = "Invalid username or password" };
                    string jsonResponse = JsonConvert.SerializeObject(responseObj);

                    response.StatusCode = 401;
                    response.ContentType = "application/json";
                    byte[] buffer = Encoding.UTF8.GetBytes(jsonResponse);
                    response.ContentLength64 = buffer.Length;
                    await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                }
            }
            catch { }
        }

        private async Task HandleLogoutAsync(HttpListenerRequest request)
        {
            var response = HttpContextHolder.CurrentResponse;

            try
            {
                // In a real implementation, you might want to invalidate the token
                var responseObj = new { success = true };
                string jsonResponse = JsonConvert.SerializeObject(responseObj);

                response.StatusCode = 200;
                response.ContentType = "application/json";
                byte[] buffer = Encoding.UTF8.GetBytes(jsonResponse);
                response.ContentLength64 = buffer.Length;
                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            }
            catch { }
        }

        private async Task HandleServersAsync(HttpListenerRequest request)
        {
            var res = HttpContextHolder.CurrentResponse;

            if (request.HttpMethod == "GET")
            {
                var obj = new { success = true, servers = _servers };
                var json = JsonConvert.SerializeObject(obj);
                var buf = Encoding.UTF8.GetBytes(json);
                res.ContentType = "application/json";
                res.ContentLength64 = buf.Length;
                await res.OutputStream.WriteAsync(buf, 0, buf.Length);
                return;
            }

            if (request.HttpMethod == "POST")
            {
                string body;
                using (var r = new StreamReader(request.InputStream)) body = await r.ReadToEndAsync();

                var reqObj = JsonConvert.DeserializeObject<CreateServerRequest>(body);

                string serverPath = Path.Combine(AppContext.BaseDirectory, "servers", reqObj.ServerName.ToLower().Replace(" ", "_"));

                var template = new MinecraftServerTemplate(serverPath, reqObj.MaxRamMB, reqObj.AllowedPorts?.ToArray(), _context);

                var server = new GameServer
                {
                    Name = reqObj.ServerName,
                    MaxRam = reqObj.MaxRamMB,
                    Path = serverPath,
                    Version = template.Version,
                    Status = "installing",
                    TemplateType = "MinecraftServerTemplate",
                    Template = template
                };

                _servers.Add(server);
                await SaveServersAsync();

                _jobQueue.Enqueue(server);
                _jobSignal.Release();

                var resp = new { success = true, server };
                var jsonResp = JsonConvert.SerializeObject(resp);
                var buf = Encoding.UTF8.GetBytes(jsonResp);
                res.ContentType = "application/json";
                res.ContentLength64 = buf.Length;
                await res.OutputStream.WriteAsync(buf, 0, buf.Length);
            }
        }

        private async Task HandleStartAsync(HttpListenerRequest request)
        {
            var res = HttpContextHolder.CurrentResponse;

            string body;
            using (var r = new StreamReader(request.InputStream))
                body = await r.ReadToEndAsync();

            var data = JsonConvert.DeserializeObject<Dictionary<string, string>>(body);

            if (!data.TryGetValue("serverName", out string name))
            {
                res.StatusCode = 400;
                return;
            }

            var server = _servers.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (server == null)
            {
                res.StatusCode = 404;
                return;
            }

            await server.Template.RunServer();
            server.Status = "running";

            var buf = Encoding.UTF8.GetBytes("{\"success\":true}");
            res.ContentType = "application/json";
            await res.OutputStream.WriteAsync(buf, 0, buf.Length);
        }

        private async Task HandleStopAsync(HttpListenerRequest request)
        {
            var res = HttpContextHolder.CurrentResponse;

            string body;
            using (var r = new StreamReader(request.InputStream))
                body = await r.ReadToEndAsync();

            var data = JsonConvert.DeserializeObject<Dictionary<string, string>>(body);

            if (!data.TryGetValue("serverName", out string name))
            {
                res.StatusCode = 400;
                return;
            }

            var server = _servers.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (server == null)
            {
                res.StatusCode = 404;
                return;
            }

            await server.Template.StopServer();
            server.Status = "stopped";

            var buf = Encoding.UTF8.GetBytes("{\"success\":true}");
            res.ContentType = "application/json";
            await res.OutputStream.WriteAsync(buf, 0, buf.Length);
        }

        private async Task HandleRemoveAsync(HttpListenerRequest request)
        {
            var res = HttpContextHolder.CurrentResponse;

            string body;
            using (var r = new StreamReader(request.InputStream))
                body = await r.ReadToEndAsync();

            var data = JsonConvert.DeserializeObject<Dictionary<string, string>>(body);

            if (!data.TryGetValue("serverName", out string name))
            {
                res.StatusCode = 400;
                return;
            }

            var server = _servers.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (server == null)
            {
                res.StatusCode = 404;
                return;
            }

            await server.Template.UninstallServer();
            _servers.Remove(server);
            await SaveServersAsync();

            var buf = Encoding.UTF8.GetBytes("{\"success\":true}");
            res.ContentType = "application/json";
            await res.OutputStream.WriteAsync(buf, 0, buf.Length);
        }

        private async Task HandleTemplatesAsync(HttpListenerRequest request)
        {
            var res = HttpContextHolder.CurrentResponse;

            var list = _templates.Select(t => new { t.Name, t.Version }).ToList();
            var json = JsonConvert.SerializeObject(new { success = true, templates = list });
            var buf = Encoding.UTF8.GetBytes(json);

            res.ContentType = "application/json";
            res.ContentLength64 = buf.Length;
            await res.OutputStream.WriteAsync(buf, 0, buf.Length);
        }

        private async Task ProcessJobsAsync()
        {
            while (!_cancel.IsCancellationRequested)
            {
                await _jobSignal.WaitAsync(_cancel.Token);
                if (_jobQueue.TryDequeue(out var server))
                {
                    try
                    {
                        await server.Template.InstallServerFiles();
                        server.Status = "stopped";
                        await SaveServersAsync();
                    }
                    catch (Exception ex)
                    {
                        _context.Logger.LogError($"Install error: {ex.Message}");
                        server.Status = "error";
                    }
                }
            }
        }

        public async Task OnUnloadAsync()
        {
            foreach (var s in _servers)
            {
                if (s.Template != null && s.Status == "running")
                    await s.Template.StopServer();
            }

            _cancel.Cancel();
            if (_jobTask != null) await _jobTask;

            var json = JsonConvert.SerializeObject(_servers, Formatting.Indented);
            await File.WriteAllTextAsync(serversFile, json);
        }

        public Task OnUpdateAsync(IPluginContext context)
        {
            foreach (var s in _servers)
            {
                if (s.Template is MinecraftServerTemplate mc)
                {
                    bool running = mc.IsRunning();
                    if (running && s.Status != "running") s.Status = "running";
                    if (!running && s.Status == "running") s.Status = "stopped";
                }
            }

            return Task.CompletedTask;
        }

        private async Task SaveServersAsync()
        {
            var json = JsonConvert.SerializeObject(_servers, Formatting.Indented);
            Directory.CreateDirectory(Path.GetDirectoryName(serversFile));
            await File.WriteAllTextAsync(serversFile, json);
        }

    }

    public class GameServer
    {
        public string Name { get; set; }
        public string Version { get; set; }
        public string Path { get; set; }
        public int MaxRam { get; set; }
        public string Status { get; set; }

        // Persisted type identifier
        public string TemplateType { get; set; }

        // runtime only
        [JsonIgnore]
        public IServerTemplate Template { get; set; }
    }

    public class CreateServerRequest
    {
        public string ServerName { get; set; }
        public int MaxRamMB { get; set; }
        public List<int> AllowedPorts { get; set; }
        public string TemplateName { get; set; }
    }


    // ---------------------------
    // MINECRAFT SERVER TEMPLATE
    // ---------------------------
    public class MinecraftServerTemplate : IServerTemplate
    {
        public string Name { get; set; } = "Minecraft Paper";
        public string Version { get; set; } = "1.20.4";
        public int[] AllowedPorts { get; set; }
        public int MaxRamMB { get; set; } = 2048;
        public string ServerPath { get; set; }

        [JsonIgnore]
        public Process Process { get; set; }
        public string ServerFilesDownloadLink { get; set; }

        private readonly Logger _logger;
        private readonly ServerConfig _config;

        public MinecraftServerTemplate(string serverPath, int maxRam, int[] allowedPorts, IPluginContext ctx)
        {
            ServerPath = serverPath;
            MaxRamMB = maxRam;
            AllowedPorts = allowedPorts ?? new[] { 25565 };
            _logger = ctx.ServiceProvider.GetRequiredService<Logger>();
            _config = ctx.ServiceProvider.GetRequiredService<ConfigManager>().Config;
        }

        public async Task InstallServerFiles()
        {
            Directory.CreateDirectory(ServerPath);

            await DownloadServerFiles();

            await File.WriteAllTextAsync(Path.Combine(ServerPath, "eula.txt"), "eula=true");

            string props =
                $"server-port={AllowedPorts[0]}\n" +
                $"online-mode=false\n";

            await File.WriteAllTextAsync(Path.Combine(ServerPath, "server.properties"), props);

            _logger.Log($"Server {Name} ({Version}) has been installed!");
        }

        public async Task DownloadServerFiles()
        {
            string url = "https://fill-data.papermc.io/v1/objects/cabed3ae77cf55deba7c7d8722bc9cfd5e991201c211665f9265616d9fe5c77b/paper-1.20.4-499.jar";
            string jar = Path.Combine(ServerPath, "server.jar");

            using HttpClient cli = new HttpClient();
            var resp = await cli.GetAsync(url);
            resp.EnsureSuccessStatusCode();

            using var fs = new FileStream(jar, FileMode.Create);
            await resp.Content.CopyToAsync(fs);
        }

        public Task<string> GetConsoleOutput() => Task.FromResult("");

        public bool IsRunning() => Process != null && !Process.HasExited;

        public async Task RunServer()
        {
            if (IsRunning()) return;

            string jar = Path.Combine(ServerPath, "server.jar");

            Process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "java",
                    Arguments = $"-Xmx{MaxRamMB}M -Xms128M -jar \"{jar}\" nogui",
                    WorkingDirectory = ServerPath,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            Process.Start();
            _logger.Log($"Server '{Name}' with PID '{Process.Id}' have been started!");
        }

        public async Task StopServer()
        {
            if (!IsRunning()) return;
            var id = Process.Id;

            try
            {
                await Process.StandardInput.WriteLineAsync("stop");
                if (!Process.WaitForExit(15000))
                    Process.Kill();
            }
            catch
            {
                if (!Process.HasExited)
                    Process.Kill();
            }
            finally
            {
                Process.Dispose();
                Process = null;
            }
            _logger.Log($"Server '{Name}' with PID '{id}' have been stopped!");
        }

        public async Task UninstallServer()
        {
            await StopServer();
            if (Directory.Exists(ServerPath))
                Directory.Delete(ServerPath, true);
            _logger.Log($"Server '{Name}' have been uninstalled!");
        }
    }
}
