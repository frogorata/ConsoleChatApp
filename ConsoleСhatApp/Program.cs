using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

class TerminalChatApp
{
    static bool debugMode = false;
    static string nickname = "User";
    static List<TcpClient> clients = new List<TcpClient>();

    static void Main()
    {
        try
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;
            Console.Title = "🎮 Терминальный Чат v2.4 (Многопользовательский)";

            while (true)
            {
                Console.WriteLine("=== Добро пожаловать в Терминальный Чат v2.4 ===\n");
                Console.WriteLine("Выберите режим:");
                Console.WriteLine("1. Создать сервер (многопользовательский)");
                Console.WriteLine("2. Подключиться к серверу");
                Console.WriteLine($"3. {(debugMode ? "Выключить" : "Включить")} debug-режим");
                Console.WriteLine("4. Выход");
                Console.Write("Введите номер: ");
                string choice = Console.ReadLine();
                switch (choice)
                {
                    case "1": StartServer(); return;
                    case "2": StartClient(); return;
                    case "3": debugMode = !debugMode; Console.Clear(); break;
                    case "4": return;
                    default: Console.WriteLine("Неверный выбор. Повторите.\n"); break;
                }
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("\nПроизошла ошибка: " + ex.Message);
            Console.ResetColor();
        }
        finally
        {
            Console.WriteLine("\nНажмите Enter для выхода...");
            Console.ReadLine();
        }
    }

    static void StartServer()
    {
        Console.Clear();
        Console.Write("Ваш ник (сервер): ");
        nickname = Console.ReadLine();

        Console.Write("Порт (например, 8888): ");
        int port = int.TryParse(Console.ReadLine(), out var p) ? p : 8888;

        TcpListener server = new TcpListener(IPAddress.Any, port);
        server.Start();

        string ipAddress = GetLocalIPAddress();
        Console.Clear();
        Console.WriteLine($"\nСервер создан!\nИмя: {nickname}\nIP: {ipAddress}\nПорт: {port}\n");

        new Thread(() =>
        {
            while (true)
            {
                TcpClient client = server.AcceptTcpClient();
                clients.Add(client);
                PrintDebug("Новый клиент подключился.");
                Console.WriteLine($"> Подключено: {clients.Count} человек(а)");
                BroadcastMessage($"[{Time()}] Сервер: Пользователь подключился.", client);
                new Thread(() => HandleClient(client)).Start();
            }
        }).Start();

        while (true)
        {
            Console.Write("> ");
            string msg = Console.ReadLine();
            if (HandleCommand(msg)) continue;
            BroadcastMessage($"[{Time()}] [{nickname}]: {msg}", null);
        }
    }

    static void HandleClient(TcpClient client)
    {
        var stream = client.GetStream();
        var reader = new StreamReader(stream, Encoding.UTF8);
        var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

        writer.WriteLine($"[{Time()}] Сервер: Добро пожаловать в чат!");

        while (true)
        {
            try
            {
                string message = reader.ReadLine();
                if (message == null) break;
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("\n" + message);
                Console.ResetColor();
                BroadcastMessage(message, client);
            }
            catch { break; }
        }
        clients.Remove(client);
        client.Close();
    }

    static void BroadcastMessage(string message, TcpClient sender)
    {
        foreach (var client in clients)
        {
            if (client == sender) continue;
            var writer = new StreamWriter(client.GetStream(), Encoding.UTF8) { AutoFlush = true };
            writer.WriteLine(message);
        }
    }

    static void StartClient()
    {
        Console.Clear();
        Console.Write("Ваш ник: ");
        nickname = Console.ReadLine();

        Console.Write("IP сервера: ");
        string ip = Console.ReadLine();

        Console.Write("Порт: ");
        int port = int.TryParse(Console.ReadLine(), out var p) ? p : 8888;

        bool stopLoading = false;
        Thread loadingThread = new Thread(() =>
        {
            string[] dots = { ".", "..", "..." };
            int i = 0;
            while (!stopLoading)
            {
                Console.Write($"\rПодключение к серверу{dots[i]}   ");
                Thread.Sleep(500);
                i = (i + 1) % dots.Length;
            }
        });
        loadingThread.Start();

        try
        {
            TcpClient client = new TcpClient(ip, port);
            stopLoading = true;
            loadingThread.Join();

            var stream = client.GetStream();
            var reader = new StreamReader(stream, Encoding.UTF8);
            var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

            Console.Clear();
            Console.WriteLine("Подключение успешно! Вы можете общаться.\n");

            new Thread(() =>
            {
                while (true)
                {
                    try
                    {
                        string message = reader.ReadLine();
                        if (message == null) break;
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine("\n" + message);
                        Console.ResetColor();
                        Console.Write("> ");
                    }
                    catch { break; }
                }
            }).Start();

            while (true)
            {
                Console.Write("> ");
                string msg = Console.ReadLine();
                if (HandleCommand(msg, writer)) continue;
                string fullMsg = $"[{Time()}] [{nickname}]: {msg}";
                writer.WriteLine(fullMsg);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\n" + fullMsg);
                Console.ResetColor();
            }
        }
        catch (Exception ex)
        {
            stopLoading = true;
            loadingThread.Join();
            Console.WriteLine("Ошибка подключения: " + ex.Message);
            Console.ReadLine();
        }
    }

    static bool HandleCommand(string input, StreamWriter writer = null)
    {
        if (input.StartsWith("/exit"))
        {
            Console.WriteLine("Вы вышли из чата.");
            Environment.Exit(0);
        }
        else if (input.StartsWith("/clear"))
        {
            Console.Clear();
            return true;
        }
        else if (input.StartsWith("/nick "))
        {
            nickname = input.Substring(6).Trim();
            Console.WriteLine($"Ник изменён на: {nickname}");
            return true;
        }
        else if (input.StartsWith("/help"))
        {
            Console.WriteLine("\n/exit — выйти из чата\n/nick <имя> — сменить ник\n/clear — очистить экран\n/help — список команд\n");
            return true;
        }
        return false;
    }

    static void PrintDebug(string msg)
    {
        if (debugMode)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("[DEBUG] " + msg);
            Console.ResetColor();
        }
    }

    static string Time() => DateTime.Now.ToString("HH:mm");

    static string GetLocalIPAddress()
    {
        foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
                return ip.ToString();
        }
        return "Не найден";
    }
}