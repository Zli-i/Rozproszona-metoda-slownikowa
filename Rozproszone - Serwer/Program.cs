using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Rozproszone___Serwer
{
    class Program
    {
        public enum result_codes
        {
            AT_SUCCESS = -1,
            AT_FAIL    = -2
        }

        public const string LOAD = "AT+LOAD="; //AT+LOAD=..\\..\\..\\Haslo\\wordlist_pl.txt 
        public const string LIST = "AT+LIST"; 
        public const string START = "AT+START";
        public const string PASSWORD = "+START=";
        public const string STOP = "AT+STOP";
        public const string DISCONNECT = "AT+DSC=";
        //public const string LOAD = "AT+LOAD="; //AT+LOAD=..\\..\\..\\Haslo\\wordlist_pl.txt 

        private readonly static Dictionary<int, Socket> connectedClients = new Dictionary<int, Socket>();
        private readonly object lockObject = new object();
        private static ConcurrentDictionary<string, int> checkedWords = new ConcurrentDictionary<string, int>();
        static bool accessAllowed = true;
        static object lockSend = new object();

        //public static ConcurrentDictionary<string, int> CheckedWords { get => checkedWords; set => checkedWords = value; }

        static void Main(string[] args)
        {
            int port = 13000;
            String ip = "127.0.0.1";
            Socket listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Parse(ip), port);
            listener.Bind(endPoint);
            listener.Listen(8);
            Console.WriteLine("Server nasłuchuje...");
            Socket klient = default(Socket);
            int polaczeni = 0;
            Program p = new Program();
            new Thread(new ThreadStart(() => p.SendMessageToThread())).Start();
            while (true)
            {
                klient = listener.Accept();
                polaczeni++;
                Console.WriteLine("Klient nr" + polaczeni + " polaczony!");
                Thread KlientThread = new Thread(new ThreadStart(() => p.KlientHandler(klient, polaczeni)));
                KlientThread.Start();
            }
        }

        public static int CollectData(string url)
        {
            if (!File.Exists(url))
            {
                Console.WriteLine("Plik podany nie istnieje!");
                return ((int)result_codes.AT_FAIL);
            }

            string[] lines = File.ReadAllLines(url);

            Console.WriteLine(lines.Length);

            foreach (string word in lines)
            {
                checkedWords.TryAdd(word, 0);
            }
            Console.WriteLine($"Załadowano {checkedWords.Count} słów");
            accessAllowed = true;
            return ((int)result_codes.AT_SUCCESS);
        }

        private static bool IsSocketConnected(Socket socket)
        {
            try
            {
                if (socket.Connected)
                    return true;
                else
                    return false;
            }
            catch (SocketException)
            {
                return false;
            }
        }

        public static void ListAllClients()
        {
            foreach (var kvp in connectedClients)
            {
                int clientId = kvp.Key;
                Socket clientSocket = kvp.Value;

                if (IsSocketConnected(clientSocket))
                {
                    // The client with clientId is connected
                    Console.WriteLine($"Klient {clientId} - connected");
                }
                else
                {
                    // The client with clientId is not connected
                    Console.WriteLine($"Klient {clientId} - disconnected");
                }
            }
        }
        private static List<int> GetConnectedThreadsId()
        {
            List<int> result = new List<int>();
            foreach (var kvp in connectedClients)
            {
                int clientId = kvp.Key;
                Socket clientSocket = kvp.Value;

                if (IsSocketConnected(clientSocket))
                {
                    // The client with clientId is connected
                    result.Add(kvp.Key);
                }
            }
            return result;
        }
        private static bool ShouldPrintValue(int currentValue, int maxRecords)
        {
            double percentage = (double)currentValue / maxRecords * 100;

            return percentage % 10 == 0;
        }

        private static void clear()
        {
            List<int> idList = GetConnectedThreadsId();
            foreach (var thread in idList)
            {
                Console.WriteLine($"Wysylam do {thread}, {STOP}");
                SendToSpecificThread(thread, STOP);
                if (connectedClients.TryGetValue(thread, out Socket targetSocket))
                {
                    if (targetSocket.Connected)
                    {
                        targetSocket.Close();
                    }
                    else
                    {
                        Console.WriteLine($"Klient {thread} jest nie podłączony!");
                    }
                }
                else
                {
                    Console.WriteLine("Thread with ID " + thread + " not found.");
                }
            }
        }

        private static void Start()
        {
            List<int> idList = GetConnectedThreadsId();
            if (idList.Count>0)
            {
                int numberOfClients = idList.Count;
                int i = 0;
                int maxRecords = checkedWords.Count;
                if (maxRecords == 0)
                {
                    Console.WriteLine("Brak wczytanych rekordów do przesłania");
                    return;
                }

                foreach (var pair in checkedWords)
                {
                    lock (lockSend)
                    {
                        if (accessAllowed)
                        {
                            ++i;
                            SendToSpecificThread(idList[i%numberOfClients], pair.Key);
                            if (ShouldPrintValue(i, maxRecords))
                            {
                                Console.WriteLine($"{i} / {maxRecords} checked!");
                            }
                        }
                        else
                        {
                            clear();
                            checkedWords.Clear();
                            return;
                        }
                    }
                }
                Console.WriteLine("Nie udalo sie znalezc hasla!");
            }
            else
            {
                Console.WriteLine("Nie można wystartować, gdy żaden klient nie jest podpięty");
            }
        }

        public void SendMessageToThread()
        {
            string sndMsg;
            while (true)
            {
                sndMsg = Console.ReadLine();
                Console.WriteLine(sndMsg);

                switch(sndMsg)
                {
                    case "?":
                    case "help":
                        Console.WriteLine("help menu");
                        break;
                    case string s when s.StartsWith("AT+"):
                        ProceedRequest(s);
                        break;
                    default:
                        Console.WriteLine("Nie znana komenda, wpisz \"help\" lub \"?\", aby wyswietlić dostępne komendy");
                        break;
                }

                // Modify the following line to specify the target thread ID
                int targetThreadId = 1;

                // Use the specified thread ID to send the message
                //SendToSpecificThread(targetThreadId, sndMsg);
            }
        }

        public static void ProceedRequest(string msg)
        {
            Tuple<int, string> result;
            switch (msg)
            {
                case string s when s.StartsWith(LOAD):
                    CollectData(s.Replace(LOAD, ""));
                    Tuple.Create(result_codes.AT_SUCCESS, result_codes.AT_SUCCESS.ToString());
                    break;
                case string s when s.StartsWith(LIST):
                    ListAllClients();
                    break;
                case string s when s.StartsWith(START):
                    Start();
                    break;
                case string s when s.StartsWith(STOP):
                    clear();
                    break;
                case string s when s.StartsWith(DISCONNECT):
                    break;
                default:
                    break;
            }
        }

        public void KlientHandler(Socket socket, int id)
        {
            lock (lockObject)
            {
                connectedClients[id] = socket;
            }

            byte[] msg = new byte[1024];
            int size = 0;
            try
            {
                while (true)
                {
                    if (IsSocketConnected(socket))
                    {
                        size = socket.Receive(msg);
                        string rcvMsg = Encoding.UTF8.GetString(msg);
                        if (rcvMsg == "")
                            return;
                        if (rcvMsg.StartsWith(PASSWORD))
                        {
                            lock (lockSend)
                            {
                                string password = rcvMsg.Replace(PASSWORD, "");
                                Console.WriteLine($"Znaleziono haslo {password}!");
                                accessAllowed = false;
                                msg = new byte[1024];
                            }
                        }
                        lock (lockSend)
                            if (accessAllowed)
                                socket.Send(msg, 0, size, SocketFlags.None);
                    }
                    else
                    {
                        return;
                    }
                }
            }
            catch(SocketException ex)
            {
                if (ex.SocketErrorCode == SocketError.ConnectionReset)
                {
                    Console.WriteLine("Klient " + id + " został zamknięty siłą.");
                }
                if (ex.SocketErrorCode == SocketError.ConnectionAborted)
                {
                    Console.WriteLine("Klient " + id + " został zamknięty siłą.");
                    //Console.WriteLine("Klient " + id + " został zamknięty siłą.");
                }
                else
                {
                    //Console.WriteLine("Błąd obsługi klienta " + id + ": " + ex.Message + " asa " + ex.SocketErrorCode.ToString());
                }
            }
            finally
            {
                Console.WriteLine("Zamykanie watku dla klienta " + id);
                socket.Close();
            }
        }

        private static void SendToSpecificThread(int targetThreadId, string message)
        {
            if (connectedClients.TryGetValue(targetThreadId, out Socket targetSocket))
            {
                if (IsSocketConnected(targetSocket))
                { 
                    byte[] msg = Encoding.UTF8.GetBytes(message);
                    targetSocket.Send(msg);
                }
                else
                {
                    Console.WriteLine($"Klient {targetThreadId} jest nie podłączony!");
                }
            }
            else
            {
                Console.WriteLine("Thread with ID " + targetThreadId + " not found.");
            }
        }
    }
}
