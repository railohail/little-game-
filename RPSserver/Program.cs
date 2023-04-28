using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

public class Player
{
    public TcpClient Client { get; set; }
    public string Choice { get; set; }
    public int Number { get; set; }

    public Player(TcpClient client, string choice = null, int num = 0)
    {
        Client = client;
        Choice = choice;
        Number = num;
    }
}

class Program
{
    private static TcpListener Listener;
    private static List<Player> clients = new List<Player>();
    private static object clientsLock = new object();

    static void Main(string[] args)
    {
        Listener = new TcpListener(IPAddress.Any, 8080);
        Listener.Start();
        Console.WriteLine("Hello, the server has started");

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
            }

            Thread clientThread = new Thread(() => HandleClient(client));
            clientThread.Start();
        }
    }

    private static void HandleClient(TcpClient client)
    {
        try
        {
            NetworkStream stream = client.GetStream();
            StreamReader reader = new StreamReader(stream);
            StreamWriter writer = new StreamWriter(stream) { AutoFlush = true };

            writer.WriteLine("Welcome to the Rock-Paper-Scissors game! Please choose an option:");
            writer.WriteLine("1. Rock");
            writer.WriteLine("2. Paper");
            writer.WriteLine("3. Scissors");

            Player currentPlayer;
            lock (clientsLock)
            {
                currentPlayer = clients.Find(p => p.Client == client);
            }

            while (currentPlayer.Choice == null && client.Connected)
            {
                string input = reader.ReadLine();
                if (input == null)
                {
                    break;
                }

                switch (input)
                {
                    case "1":
                        currentPlayer.Choice = "Rock";
                        break;
                    case "2":
                        currentPlayer.Choice = "Paper";
                        break;
                    case "3":
                        currentPlayer.Choice = "Scissors";
                        break;
                    default:
                        writer.WriteLine("Invalid choice, please choose again:");
                        break;
                }
            }

            if (currentPlayer.Choice != null)
            {
                writer.WriteLine($"You chose {currentPlayer.Choice}");
            }
            else
            {
                lock (clientsLock)
                {
                    clients.Remove(currentPlayer);
                }
                client.Close();
                return;
            }

            // Wait for all clients to make a choice
            bool allClientsMadeChoice;
            do
            {
                allClientsMadeChoice = true;
                lock (clientsLock)
                {
                    foreach (Player player in clients)
                    {
                        if (player.Choice == null)
                        {
                            allClientsMadeChoice = false;
                            break;
                        }
                    }
                }
                Thread.Sleep(100);
            } while (!allClientsMadeChoice);

            // Send the result to all clients
            lock (clientsLock)
            {
                string message = GetResultMessage();
                foreach (Player player in clients)
                {
                    NetworkStream clientStream = player.Client.GetStream();
                    StreamWriter clientWriter = new StreamWriter(clientStream) { AutoFlush = true };
                    clientWriter.WriteLine(message);
                }
            }

                        // Reset choices for the next round
            lock (clientsLock)
            {
                foreach (Player player in clients)
                {
                    player.Choice = null;
                }
            }

            // Close the connection
            client.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        finally
        {
            lock (clientsLock)
            {
                Player playerToRemove = clients.Find(p => p.Client == client);
                if (playerToRemove != null)
                {
                    clients.Remove(playerToRemove);
                }
            }

            if (client.Connected)
            {
                client.Close();
            }
        }
    }

    private static string GetResultMessage()
    {
        Dictionary<string, int> choicesCount = new Dictionary<string, int>
        {
            { "Rock", 0 },
            { "Paper", 0 },
            { "Scissors", 0 }
        };

        lock (clientsLock)
        {
            foreach (Player player in clients)
            {
                choicesCount[player.Choice]++;
            }
        }

        StringBuilder messageBuilder = new StringBuilder();
        messageBuilder.AppendLine("Results:");

        foreach (KeyValuePair<string, int> entry in choicesCount)
        {
            messageBuilder.AppendLine($"{entry.Key}: {entry.Value}");
        }

        if (choicesCount["Rock"] > 0 && choicesCount["Paper"] > 0 && choicesCount["Scissors"] > 0)
        {
            messageBuilder.AppendLine("It's a draw!");
        }
        else if (choicesCount["Rock"] > 0 && choicesCount["Paper"] == 0)
        {
            messageBuilder.AppendLine("Rock wins!");
        }
        else if (choicesCount["Paper"] > 0 && choicesCount["Scissors"] == 0)
        {
            messageBuilder.AppendLine("Paper wins!");
        }
        else if (choicesCount["Scissors"] > 0 && choicesCount["Rock"] == 0)
        {
            messageBuilder.AppendLine("Scissors wins!");
        }
        else
        {
            messageBuilder.AppendLine("It's a draw!");
        }

        return messageBuilder.ToString();
    }
}

