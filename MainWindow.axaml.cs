using System;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using System.Text;

namespace RPSduel
{
    public partial class MainWindow : Window
    {
        private TextBlock _gameText;
        private TcpClient _client;
        private StreamReader _reader;
        private StreamWriter _writer;

        // Add the game area size
        private const int GameWidth = 80; // Use smaller values for better readability
        private const int GameHeight = 60;

        public MainWindow()
        {
            InitializeComponent();
            _gameText = this.FindControl<TextBlock>("GameText");
            _ = InitializeConnection(); // Discard the task to avoid a warning
            Opened += async (sender, e) => await SendInputToServer("move:0,0");

            // Register the event handlers
            KeyDown += OnKeyDown;
            KeyUp += OnKeyUp;
        }

        private async void OnKeyDown(object sender, KeyEventArgs e)
        {
            float x = 0;
            float y = 0;
            string projectileType = null;

            switch (e.Key)
            {
                case Key.W:
                    y = -1;
                    break;
                case Key.A:
                    x = -1;
                    break;
                case Key.S:
                    y = 1;
                    break;
                case Key.D:
                    x = 1;
                    break;
                case Key.J:
                    projectileType = "Rock";
                    break;
                case Key.K:
                    projectileType = "Paper";
                    break;
                case Key.L:
                    projectileType = "Scissors";
                    break;
            }

            if (x != 0 || y != 0)
            {
                await SendInputToServer($"move:{x},{y}");
            }
            else if (projectileType != null)
            {
                await SendInputToServer($"shoot:{projectileType},0,0"); // Replace 0,0 with the desired projectile velocity
            }
        }

        private async void OnKeyUp(object sender, KeyEventArgs e)
        {
            await SendInputToServer("move:0,0");
        }


        private async Task InitializeConnection()
        {
            await ConnectToServer();
        }

        private async Task ConnectToServer()
        {
            try
            {
                _client = new TcpClient();
                await _client.ConnectAsync("localhost", 8080);
                NetworkStream stream = _client.GetStream();
                _writer = new StreamWriter(stream) { AutoFlush = true };
                _reader = new StreamReader(stream);

                await DisplayText("Connected to the server!");

                _ = Task.Run(ReceiveData); // Discard the task to avoid a warning
            }
            catch (Exception ex)
            {
                await DisplayText($"Error connecting to the server: {ex.Message}");
            }
        }

        private async Task DisplayText(string text)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _gameText.Text = text;
            });

        }

        private async Task ReceiveData()
        {
            try
            {
                while (_client.Connected)
                {
                    string data = await _reader.ReadLineAsync();
                    if (data != null)
                    {
                        await RenderGameState(data);
                    }
                }
            }
            catch (Exception ex)
            {
                await DisplayText($"Error receiving data from the server: {ex.Message}");
            }
        }

        private async Task SendInputToServer(string input)
        {
            try
            {
                if (_client.Connected)
                {
                    await _writer.WriteLineAsync(input);
                }
            }
            catch (Exception ex)
            {
                await DisplayText($"Error sending input to server: {ex.Message}");
            }
        }

        private async Task RenderGameState(string gameState)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                char[][] gameArea = new char[GameHeight][];
                for (int i = 0; i < GameHeight; i++)
                {
                    gameArea[i] = new string(' ', GameWidth).ToCharArray();
                }

                string[] sections = gameState.Split('|');
                string[] players = sections[0].Split(';', StringSplitOptions.RemoveEmptyEntries);
                string[] projectiles = sections[1].Split(';', StringSplitOptions.RemoveEmptyEntries);

                foreach (string player in players)
                {
                    string[] parts = player.Split(',');
                    char displayCharacter = parts[0][0];
                    int x = (int)float.Parse(parts[1]);
                    int y = (int)float.Parse(parts[2]);

                    x = Math.Clamp(x, 0, GameWidth - 1);
                    y = Math.Clamp(y, 0, GameHeight - 1);

                    gameArea[y][x] = displayCharacter;
                }

                foreach (string projectile in projectiles)
                {
                    string[] parts = projectile.Split(',');
                    char displayCharacter = parts[0][0];
                    int x = (int)float.Parse(parts[1]);
                    int y = (int)float.Parse(parts[2]);

                    x = Math.Clamp(x, 0, GameWidth - 1);
                    y = Math.Clamp(y, 0, GameHeight - 1);

                    gameArea[y][x] = displayCharacter;
                }

                StringBuilder displayText = new StringBuilder();
                for (int i = 0; i < GameHeight; i++)
                {
                    displayText.AppendLine(new string(gameArea[i]));
                }

                _gameText.Text = displayText.ToString();
            });
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}