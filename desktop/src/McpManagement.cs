using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Negotchi
{
    internal sealed class McpConfiguration
    {
        internal Dictionary<string, object> Servers = Json.Map();
        internal string Version, File;
        internal static async Task<McpConfiguration> Read(CodexClient client)
        {
            var response = await client.RequestAsync("config/read", Json.Map("includeLayers", true));
            var layer = Json.Items(response, "layers").OfType<Dictionary<string, object>>().FirstOrDefault(item => Json.Text(Json.Object(item, "name"), "type") == "user" && string.IsNullOrEmpty(Json.Text(Json.Object(item, "name"), "profile")));
            return new McpConfiguration { Servers = layer == null ? Json.Map() : new Dictionary<string, object>(Json.Object(Json.Object(layer, "config"), "mcp_servers")), Version = layer == null ? null : Json.Text(layer, "version"), File = layer == null ? null : Json.Text(Json.Object(layer, "name"), "file") };
        }
        internal async Task Write(CodexClient client)
        {
            var parameters = Json.Map("edits", new[] { Json.Map("keyPath", "mcp_servers", "value", Servers, "mergeStrategy", "replace") }, "reloadUserConfig", false);
            if (!string.IsNullOrEmpty(Version)) parameters["expectedVersion"] = Version;
            if (!string.IsNullOrEmpty(File)) parameters["filePath"] = File;
            var response = await client.RequestAsync("config/batchWrite", parameters); Version = Json.Text(response, "version");
            await client.RequestAsync("config/mcpServer/reload", Json.Map());
        }
        internal static void ValidateName(string name) { if (string.IsNullOrWhiteSpace(name) || !Regex.IsMatch(name, "^[a-zA-Z0-9_-]+$")) throw new ArgumentException("Use letras, números, hífen ou sublinhado no nome."); }
        internal static Dictionary<string, object> Edit(Dictionary<string, object> original, bool http, string endpoint, string arguments, string tokenEnvironment)
        {
            var result = original == null ? Json.Map() : new Dictionary<string, object>(original);
            if (!string.IsNullOrWhiteSpace(endpoint))
            {
                if (http)
                {
                    Uri uri; if (!Uri.TryCreate(endpoint.Trim(), UriKind.Absolute, out uri) || (uri.Scheme != "http" && uri.Scheme != "https") || uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0) throw new ArgumentException("Informe uma URL HTTP(S) sem credenciais ou parâmetros. Use OAuth ou variável de ambiente para autenticação.");
                    result["url"] = endpoint.Trim(); result.Remove("command"); result.Remove("args");
                }
                else { result["command"] = endpoint.Trim(); result.Remove("url"); }
            }
            if (!result.ContainsKey(http ? "url" : "command")) throw new ArgumentException("Informe a origem do servidor para o transporte escolhido.");
            if (!http && !string.IsNullOrWhiteSpace(arguments))
            {
                var values = Json.Items(Json.Decode("{\"args\":" + arguments + "}"), "args");
                if (values.Any(value => !(value is string))) throw new ArgumentException("Argumentos devem ser uma lista JSON de textos.");
                result["args"] = values;
            }
            if (!string.IsNullOrWhiteSpace(tokenEnvironment))
            {
                if (!Regex.IsMatch(tokenEnvironment.Trim(), "^[a-zA-Z_][a-zA-Z0-9_]*$")) throw new ArgumentException("Informe somente o nome da variável, não o token.");
                result["bearer_token_env_var"] = tokenEnvironment.Trim();
            }
            return result;
        }
    }

    internal sealed class McpManagement : Window
    {
        private readonly CodexClient client;
        private readonly ListBox list;
        private readonly TextBox details;
        private readonly WrapPanel actions;
        private readonly TextBlock status;
        private McpConfiguration config;
        private readonly Dictionary<string, Dictionary<string, object>> runtime = new Dictionary<string, Dictionary<string, object>>();
        private bool active, closed;
        public McpManagement(Window owner, CodexClient client)
        {
            this.client = client; Owner = owner; Title = "Servidores MCP"; Width = 760; Height = 560; MinWidth = 560; MinHeight = 400; WindowStartupLocation = WindowStartupLocation.CenterOwner; ShowInTaskbar = false; Background = Theme.Background; Foreground = Theme.Text;
            var root = new DockPanel { Margin = new Thickness(20) };
            status = new TextBlock { Text = "Carregando…", Foreground = Theme.Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10) }; DockPanel.SetDock(status, Dock.Top); root.Children.Add(status);
            actions = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) }; DockPanel.SetDock(actions, Dock.Bottom); root.Children.Add(actions);
            actions.Children.Add(Theme.Button("Atualizar", async delegate { await Refresh(); }, false));
            actions.Children.Add(Theme.Button("Adicionar", async delegate { await Change("add"); }, false));
            actions.Children.Add(Theme.Button("Editar", async delegate { await Change("edit"); }, false));
            actions.Children.Add(Theme.Button("Ativar / desativar", async delegate { await Change("toggle"); }, false));
            actions.Children.Add(Theme.Button("Remover", async delegate { await Change("remove"); }, false));
            actions.Children.Add(Theme.Button("Autenticar", async delegate { await Login(); }, false));
            actions.Children.Add(Theme.Button("Fechar", delegate { Close(); }, false));
            var grid = new Grid(); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) }); grid.ColumnDefinitions.Add(new ColumnDefinition());
            list = new ListBox { Background = Theme.Card, Foreground = Theme.Text, BorderBrush = Theme.Line }; grid.Children.Add(list);
            details = new TextBox { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = Theme.Card, Foreground = Theme.Text, BorderBrush = Theme.Line, Margin = new Thickness(10, 0, 0, 0), Padding = new Thickness(10) }; Grid.SetColumn(details, 1); grid.Children.Add(details); root.Children.Add(grid);
            list.SelectionChanged += delegate { ShowDetails(); }; Content = root; Theme.BindTypography(this);
            Loaded += async delegate { await Refresh(); }; Closed += delegate { closed = true; };
        }
        private async Task Refresh()
        {
            actions.IsEnabled = false;
            try
            {
                config = await McpConfiguration.Read(client); runtime.Clear(); string cursor = null; var visited = new HashSet<string>();
                do
                {
                    var page = await client.RequestAsync("mcpServerStatus/list", Json.Map("cursor", cursor, "limit", 100));
                    foreach (var server in Json.Items(page, "data").OfType<Dictionary<string, object>>()) runtime[Json.Text(server, "name")] = server;
                    cursor = Json.Text(page, "nextCursor"); if (cursor.Length > 0 && !visited.Add(cursor)) throw new InvalidOperationException("Página de MCP repetida.");
                } while (cursor.Length > 0 && !closed);
                string selected = list.SelectedItem as string; list.ItemsSource = config.Servers.Keys.Concat(runtime.Keys).Distinct().OrderBy(name => name).ToArray(); list.SelectedItem = selected; if (list.SelectedItem == null && list.Items.Count > 0) list.SelectedIndex = 0;
                status.Text = "Configuração global do Codex. Revise a origem antes de autorizar; processos MCP podem acessar arquivos e rede com as permissões do usuário.";
            }
            catch (Exception error) { status.Text = "Não foi possível consultar MCP: " + error.Message; }
            finally { actions.IsEnabled = !closed; }
        }
        private void ShowDetails()
        {
            string name = list.SelectedItem as string; if (name == null || config == null) { details.Clear(); return; }
            var configured = Json.Get(config.Servers, name) as Dictionary<string, object>;
            Dictionary<string, object> server; runtime.TryGetValue(name, out server);
            details.Text = name + "\n\n" + (configured == null ? "Origem herdada/gerenciada: edição global indisponível." : "Configuração global: " + (Equals(Json.Get(configured, "enabled"), false) ? "desativada" : "ativada") + "\nTransporte: " + (configured.ContainsKey("url") ? "HTTP" : "stdio")) + "\n\nAutenticação: " + (Json.Text(server, "authStatus").Length > 0 ? Json.Text(server, "authStatus") : "Não informada") + "\n\nFerramentas: " + (Json.Object(server, "tools").Count == 0 ? "Nenhuma reportada" : string.Join(", ", Json.Object(server, "tools").Keys)) + "\n\nCredenciais e argumentos existentes permanecem ocultos.";
        }
        private async Task Change(string operation)
        {
            if (active || config == null) return; string name = list.SelectedItem as string;
            if (operation != "add" && (name == null || !config.Servers.ContainsKey(name))) { status.Text = "Selecione um servidor configurado no arquivo global do usuário."; return; }
            string target = name; Dictionary<string, object> value = name == null ? null : Json.Get(config.Servers, name) as Dictionary<string, object>;
            if (operation == "add" || operation == "edit")
            {
                var edited = EditServer(operation == "add" ? null : name, operation == "add" ? null : value); if (edited == null) return; target = edited.Item1; value = edited.Item2;
                if (operation == "add" && config.Servers.ContainsKey(target)) { status.Text = "Já existe um servidor com esse nome."; return; }
            }
            if (!DesktopActions.Review(this, "Revisar servidor MCP", operation + ": " + target + "\n\n" + (operation == "add" || operation == "edit" ? "Origem: " + GitWindow.RedactRemote(Json.Text(value, "url")) + Json.Text(value, "command") + "\nAutenticação: valores secretos não serão exibidos.\nO servidor pode expor ferramentas com acesso a arquivos, processos e rede. Confira a origem e os argumentos informados." : "A alteração afeta a configuração global compartilhada do Codex."))) return;
            active = true; actions.IsEnabled = false;
            var before = new Dictionary<string, object>(config.Servers);
            try
            {
                if (operation == "remove") config.Servers.Remove(target);
                else if (operation == "toggle") { var updated = new Dictionary<string, object>(value); updated["enabled"] = Equals(Json.Get(value, "enabled"), false); config.Servers[target] = updated; }
                else config.Servers[target] = value;
                await config.Write(client); await Refresh(); status.Text = "Configuração salva e recarregada. Consulte as ferramentas e o estado antes de usar o servidor.";
            }
            catch (Exception error) { config.Servers = before; status.Text = "Falha ao aplicar; atualize para conferir o estado: " + error.Message; }
            finally { active = false; actions.IsEnabled = !closed; ShowDetails(); }
        }
        private Tuple<string, Dictionary<string, object>> EditServer(string existing, Dictionary<string, object> original)
        {
            var window = new Window { Owner = this, Title = existing == null ? "Adicionar MCP" : "Editar MCP", Width = 570, Height = 520, MinHeight = 450, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = Theme.Background, Foreground = Theme.Text, ShowInTaskbar = false };
            var root = new DockPanel { Margin = new Thickness(20) }; var buttons = new StackPanel { Orientation = Orientation.Horizontal }; DockPanel.SetDock(buttons, Dock.Bottom); root.Children.Add(buttons);
            var fields = new StackPanel(); root.Children.Add(fields);
            Func<string, string, TextBox> field = delegate(string label, string text) { fields.Children.Add(new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 4) }); var input = new TextBox { Text = text ?? "", Background = Theme.Card, Foreground = Theme.Text, BorderBrush = Theme.Line, Padding = new Thickness(7), Margin = new Thickness(0, 0, 0, 10) }; fields.Children.Add(input); return input; };
            var name = field("Nome", existing); name.IsReadOnly = existing != null;
            var transport = new ComboBox { ItemsSource = new[] { "stdio: programa local", "HTTP: URL" }, SelectedIndex = original != null && original.ContainsKey("url") ? 1 : 0, Margin = new Thickness(0, 0, 0, 10) }; fields.Children.Add(transport);
            var endpoint = field("Programa ou URL (vazio preserva o existente)", "");
            var args = field("Argumentos JSON para stdio, ex.: [\"--flag\"] (vazio preserva)", "");
            var environment = field("Nome da variável do token HTTP (vazio preserva)", "");
            var error = new TextBlock { Foreground = Theme.Accent, TextWrapping = TextWrapping.Wrap }; fields.Children.Add(error); Tuple<string, Dictionary<string, object>> result = null;
            buttons.Children.Add(Theme.Button("Cancelar", delegate { window.Close(); }, false)); buttons.Children.Add(Theme.Button("Revisar", delegate { try { McpConfiguration.ValidateName(name.Text); result = Tuple.Create(name.Text.Trim(), McpConfiguration.Edit(original, transport.SelectedIndex == 1, endpoint.Text, args.Text, environment.Text)); window.Close(); } catch (Exception failure) { error.Text = failure.Message; } }, true));
            window.Content = root; Theme.BindTypography(window); window.ShowDialog(); return result;
        }
        private async Task Login()
        {
            string name = list.SelectedItem as string; if (name == null || active) return;
            if (!DesktopActions.Review(this, "Autenticar MCP", "Abrir a autorização do servidor " + name + " no navegador?")) return;
            active = true; actions.IsEnabled = false;
            try { var result = await client.RequestAsync("mcpServer/oauth/login", Json.Map("name", name)); Uri url; if (!Uri.TryCreate(Json.Text(result, "authorizationUrl"), UriKind.Absolute, out url) || url.Scheme != "https") throw new InvalidOperationException("O servidor não retornou uma URL HTTPS de autorização."); DesktopActions.Launch(url.ToString()); status.Text = "Conclua no navegador e clique Atualizar."; }
            catch (Exception error) { status.Text = "Autenticação indisponível: " + error.Message; }
            finally { active = false; actions.IsEnabled = !closed; }
        }
    }
}
