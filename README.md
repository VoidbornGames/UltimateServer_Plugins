<header>
<h1>UltimateServer Plugins</h1>
<p>A collection of community-created plugins to extend the functionality of UltimateServer.</p>
</header>

<nav class="toc">
<h2>Table of Contents</h2>
            <ul>
                <li><a href="#installation">🔧 Installation</a></li>
                <li><a href="#plugin-showcase">🧩 Plugin Showcase</a></li>
                <li><a href="#usage">📖 Usage</a></li>
                <li><a href="#developer-guide">👨‍💻 Developer Guide</a></li>
                <li><a href="#contributing">🤝 Contributing</a></li>
                <li><a href="#license">📄 License</a></li>
                <li><a href="#support">💬 Support</a></li>
            </ul>
        </nav>

<main>
            <section id="installation">
                <h2>🔧 Installation</h2>
                <p>Follow these steps to add a plugin to your UltimateServer instance.</p>
                <ol>
                    <li>
                        <h3>Download The Plugins</h3>
                        <p>Download the <code>.dll</code> plugin file from the collection.</p>
                    </li>
                    <li>
                        <h3>Upload To The Server</h3>
                        <p>Upload the plugin <code>.dll</code> file you downloaded trough the UltimateServer dashboard.</p>
                    </li>
                    <li>
                        <h3>Configure The Plugin</h3>
                        <p>Most of plugins have a config file like <code>config.json</code> in <code>/path/to/your/UltimateServer/Plugins/Plugin-Name/config.json</code> and 
                        By editing it you can configure the plugin using that file.</p>
                    </li>
                </ol>
            </section>

<section id="plugin-showcase">
                <h2>🧩 Plugin Showcase</h2>
                <p>This is a list of all available plugins in this repository. Click on a plugin's name to view its source code and specific documentation.</p>
                <table>
                    <thead>
                        <tr>
                            <th>Plugin Name</th>
                            <th>Description</th>
                            <th>Author</th>
                            <th>Status</th>
                        </tr>
                    </thead>
                    <tbody>
                        <!-- TODO: Add your plugins to this table. This is an example row. -->
                        <tr>
                            <td><a href="https://github.com/VoidbornGames/UltimateServer_Plugins/tree/main/SuperLib">SuperLib</a></td>
                            <td>Provides essential api routes like /api/health, /api/system/info.</td>
                            <td>VoidbornGames</td>
                            <td><span class="badge badge-active">Active</span></td>
                        </tr>
                    </tbody>
                </table>
            </section>

<section id="usage">
                <h2>📖 Usage</h2>
                <p>Each plugin may have its own configuration file (usually <code>config.yml</code>) and a set of commands. After installing a plugin, check its dedicated folder within the repository for a specific README or configuration guide.</p>
                <p>For example, to configure the <code>SuperLib</code> plugin, you would edit the file located at:</p>
                <pre><code class="language-bash">/path/to/your/UltimateServer/Plugins/SuperLib/config.json</code></pre>
            </section>

<section id="developer-guide">
                <h2>👨‍💻 Developer Guide</h2>
                <p>Want to create your own plugin? That's great! Here's the basic structure.</p>
                <p><a href="https://github.com/VoidbornGames/UltimateServer/wiki/Plugin-Development">See The Details Here</a></p>

<section id="contributing">
                <h2>🤝 Contributing</h2>
                <p>We welcome contributions from the community! Whether you're fixing a bug, adding a new feature, or creating a brand new plugin, we'd love to see it.</p>
                <ol>
                    <li><strong>Fork</strong> this repository.</li>
                    <li>Create a new branch for your feature (<code>git checkout -b feature/MyNewPlugin</code>).</li>
                    <li>Commit your changes (<code>git commit -am 'Add some awesome plugin'</code>).</li>
                    <li>Push to the branch (<code>git push origin feature/MyNewPlugin</code>).</li>
                    <li>Create a new <strong>Pull Request</strong>.</li>
                </ol>
                <p>Please ensure your code follows the project's coding standards and that you have tested your plugin thoroughly before submitting a pull request.</p>
            </section>

<section id="license">
                <h2>📄 License</h2>
                <p>This project is licensed under the MIT License. See the <a href="LICENSE">LICENSE</a> file for more details.</p>
            </section>

<section id="support">
                <h2>💬 Support</h2>
                <p>If you encounter any issues with a plugin or have a feature request, please open an issue on the <a href="https://github.com/VoidbornGames/UltimateServer_Plugins/issues">GitHub Issues page</a>.</p>
            </section>
        </main>

<footer>
            <p>Made with ❤️ by the <a href="https://github.com/VoidbornGames">Voidborn Games</a> community.</p>
        </footer>
