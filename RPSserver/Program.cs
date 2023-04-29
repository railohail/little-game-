using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

public class Vector2
{
    public float X { get; set; }
    public float Y { get; set; }

    public Vector2(float x = 0, float y = 0)
    {
        X = x;
        Y = y;
    }

    public Vector2 Add(Vector2 other)
    {
        return new Vector2(X + other.X, Y + other.Y);
    }

    public Vector2 Multiply(float scalar)
    {
        return new Vector2(X * scalar, Y * scalar);
    }
    public static float Distance(Vector2 v1, Vector2 v2)
    {
        float deltaX = v1.X - v2.X;
        float deltaY = v1.Y - v2.Y;
        return (float)Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
    }
}

public class Player
{
    public TcpClient Client { get; set; }
    public string Choice { get; set; }
    public int Number { get; set; }
    public Vector2 Position { get; set; }
    public Vector2 Velocity { get; set; }
    public char DisplayCharacter { get; set; }
    public int AdditionalNumber { get; set; }
    public Vector2 LastInputDirection { get; set; }

    public Player(TcpClient client, string choice = null, int num = 0)
    {
        Client = client;
        Choice = choice;
        Number = num;
        Position = new Vector2();
        Velocity = new Vector2();
        DisplayCharacter = 'c';
        AdditionalNumber = num;
    }
}

public class Projectile
{
    public string Type { get; set; }
    public Vector2 Position { get; set; }
    public Vector2 Velocity { get; set; }
    public Player Owner { get; set; }

    public Projectile(string type, Vector2 position, Vector2 velocity, Player owner)
    {
        Type = type;
        Position = position;
        Velocity = velocity;
        Owner = owner;
    }
}

class Program
{
    private static TcpListener Listener;
    private static List<Player> clients = new List<Player>();
    private static object clientsLock = new object();
    private static List<Projectile> projectiles = new List<Projectile>();
    private static void DestroyProjectile(int index)
    {
        lock (clientsLock)
        {
            projectiles.RemoveAt(index);
        }
    }

    private static void CheckCollisions(List<Projectile> projectiles)
    {
        // Iterate through all projectiles and players to check for collisions
        lock (clientsLock)
        {
            for (int i = projectiles.Count - 1; i >= 0; i--)
            {
                Projectile projectile = projectiles[i];
                foreach (Player player in clients)
                {
                    if (player != projectile.Owner && IsColliding(projectile.Position, player.Position))
                    {
                        Console.WriteLine($"Player {player.Number} collided with a projectile");
                        clients.Remove(player);
                        player.Client.Close();
                        DestroyProjectile(i);
                        break;
                    }
                }

                // Check for projectile collisions
                for (int j = projectiles.Count - 1; j >= 0; j--)
                {
                    if (i >= projectiles.Count || j >= projectiles.Count || i == j || projectiles[i].Owner == projectiles[j].Owner) continue;

                    Projectile otherProjectile = projectiles[j];
                    if (IsColliding(projectile.Position, otherProjectile.Position))
                    {
                        if ((projectile.Type == "Rock" && otherProjectile.Type == "Scissors") ||
                            (projectile.Type == "Scissors" && otherProjectile.Type == "Paper") ||
                            (projectile.Type == "Paper" && otherProjectile.Type == "Rock"))
                        {
                            // Winner projectile passes through the loser
                            DestroyProjectile(j);
                        }
                        else
                        {
                            // Projectiles of the same type or Rock-Rock, Paper-Paper, Scissors-Scissors collisions
                            DestroyProjectile(i);
                            break;
                        }
                    }
                }
            }
        }
    }





    private static bool IsColliding(Vector2 pos1, Vector2 pos2)
    {
        // Use simple distance check for collision
        float distance = Vector2.Distance(pos1, pos2);
        float collisionDistance = 1.0f; // Adjust this value based on the size of your game objects

        return distance < collisionDistance;
    }

    private static void SendGameStateToClients()
    {
        StringBuilder gameState = new StringBuilder();

        lock (clientsLock)
        {
            foreach (Player player in clients)
            {
                gameState.Append($"{player.DisplayCharacter}{player.AdditionalNumber},{player.Position.X},{player.Position.Y};");
            }

            gameState.Append("|"); // Separator between players and projectiles

            foreach (Projectile proj in projectiles)
            {
                char projDisplayCharacter = ' ';
                switch (proj.Type)
                {
                    case "Rock":
                        projDisplayCharacter = 'R';
                        break;
                    case "Paper":
                        projDisplayCharacter = 'P';
                        break;
                    case "Scissors":
                        projDisplayCharacter = 'S';
                        break;
                }

                gameState.Append($"{projDisplayCharacter},{proj.Position.X},{proj.Position.Y};");
            }
        }

        string gameStateString = gameState.ToString();
        foreach (Player player in clients)
        {
            // Check if the client is still connected before getting the stream
            if (player.Client.Connected)
            {
                StreamWriter writer = new StreamWriter(player.Client.GetStream()) { AutoFlush = true };
                writer.WriteLine(gameStateString);
            }
        }
    }


    private static void UpdatePositions(Player currentPlayer, List<Projectile> projectiles)
    {
        float deltaTime = 1.0f / 60; // Assuming 60 FPS
        float playerSpeed = 5.0f;
        float projectileSpeed = 10.0f;
        float minX = 0, maxX = 800; // Assuming game area width of 800 units
        float minY = 0, maxY = 600; // Assuming game area height of 600 units

        // Update player position
        currentPlayer.Position = currentPlayer.Position.Add(currentPlayer.Velocity.Multiply(playerSpeed * deltaTime));

        // Bound player position within game area
        currentPlayer.Position.X = Math.Clamp(currentPlayer.Position.X, minX, maxX);
        currentPlayer.Position.Y = Math.Clamp(currentPlayer.Position.Y, minY, maxY);

        // Update projectile positions
        lock (clientsLock)
        {
            for (int i = 0; i < projectiles.Count; i++)
            {
                Projectile proj = projectiles[i];
                proj.Position = proj.Position.Add(proj.Velocity.Multiply(projectileSpeed * deltaTime));

                // Remove projectiles that go out of bounds
                if (proj.Position.X < minX || proj.Position.X > maxX || proj.Position.Y < minY || proj.Position.Y > maxY)
                {
                    DestroyProjectile(i);
                    i--;
                }
            }
        }
    }

    private static void SendGameStateToClient(Player player)
    {
        StringBuilder gameState = new StringBuilder();
        if (!player.Client.Connected)
        {
            return;
        }
        lock (clientsLock)
        {
            foreach (Player otherPlayer in clients)
            {
                gameState.Append($"{otherPlayer.DisplayCharacter}{otherPlayer.AdditionalNumber},{otherPlayer.Position.X},{otherPlayer.Position.Y};");
            }

            gameState.Append("|"); // Separator between players and projectiles

            foreach (Projectile proj in projectiles)
            {
                char projDisplayCharacter = ' ';
                switch (proj.Type)
                {
                    case "Rock":
                        projDisplayCharacter = 'R';
                        break;
                    case "Paper":
                        projDisplayCharacter = 'P';
                        break;
                    case "Scissors":
                        projDisplayCharacter = 'S';
                        break;
                }

                gameState.Append($"{projDisplayCharacter},{proj.Position.X},{proj.Position.Y};");
            }
        }

        StreamWriter writer = new StreamWriter(player.Client.GetStream()) { AutoFlush = true };
        writer.WriteLine(gameState.ToString());
    }
    static void Main(string[] args)
    {
        Listener = new TcpListener(IPAddress.Any, 8080);
        Listener.Start();
        Console.WriteLine("Hello, the server has started");

        Thread gameLoopThread = new Thread(GameLoop);
        gameLoopThread.Start();

        while (true)
        {
            TcpClient client = Listener.AcceptTcpClient();
            lock (clientsLock)
            {
                int playerNumber = clients.Count + 1;
                Player newPlayer = new Player(client, null, playerNumber);
                clients.Add(newPlayer);
                Console.WriteLine($"Client {playerNumber} connected");
                Console.WriteLine($"There are a total of {clients.Count} clients");

                // Send the current game state to the newly connected client
                SendGameStateToClient(newPlayer);
            }

            Thread clientThread = new Thread(() => HandleClient(client));
            clientThread.Start();
        }
    }
    private static void GameLoop()
    {
        System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
        stopwatch.Start();

        while (true)
        {
            long elapsedMilliseconds = stopwatch.ElapsedMilliseconds;
            if (elapsedMilliseconds >= 1000 / 60)
            {
                lock (clientsLock)
                {
                    foreach (Player player in clients)
                    {
                        // Update player and projectile positions
                        UpdatePositions(player, projectiles);
                    }
                }

                // Check for collisions between projectiles and players
                CheckCollisions(projectiles);

                // Send game state to all clients
                SendGameStateToClients();

                stopwatch.Restart();
            }
        }
    }




    private static void ProcessGame(Player currentPlayer)
    {
        // Update player and projectile positions
        UpdatePositions(currentPlayer, projectiles);

        // Check for collisions between projectiles and players
        CheckCollisions(projectiles);

        // Send game state to all clients
        SendGameStateToClients();
    }



    private static void HandleClient(TcpClient client)
    {
        try
        {
            NetworkStream stream = client.GetStream();
            StreamReader reader = new StreamReader(stream);
            StreamWriter writer = new StreamWriter(stream) { AutoFlush = true };

            Player currentPlayer;
            lock (clientsLock)
            {
                currentPlayer = clients.Find(p => p.Client == client);
            }

            while (client.Connected)
            {
                if (!client.Connected) // Check if the client is still connected
                {
                    break; // Exit the loop if the client is no longer connected
                }

                string input = reader.ReadLine();

                // Process the input commands
                HandleInput(currentPlayer, input, projectiles);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling client: {ex.Message}");
        }
        finally
        {
            lock (clientsLock)
            {
                clients.RemoveAll(p => p.Client == client);
                Console.WriteLine("Client disconnected");
                Console.WriteLine($"There are now {clients.Count} clients");
            }
            client.Close();
        }
    }



    private static void HandleInput(Player currentPlayer, string input, List<Projectile> projectiles)
    {
        string[] inputs = input.Split(' ');
        foreach (string command in inputs)
        {
            if (command.StartsWith("move:"))
            {
                string[] moveParams = command.Substring(5).Split(',');
                float x = float.Parse(moveParams[0]);
                float y = float.Parse(moveParams[1]);
                currentPlayer.Velocity = new Vector2(x, y);
                currentPlayer.LastInputDirection = new Vector2(x, y); // Store the last input direction
            }

            else if (command.StartsWith("shoot:"))
            {
                string[] shootParams = command.Substring(6).Split(',');
                string projectileType = shootParams[0];
                Vector2 projectileVelocity = currentPlayer.LastInputDirection;
                projectiles.Add(new Projectile(projectileType, currentPlayer.Position, projectileVelocity, currentPlayer));
            }

        }
    }
}
