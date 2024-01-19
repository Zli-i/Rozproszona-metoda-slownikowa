using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Rozproszone___Client
{
    class Program
    {
        public const string PASSWORD = "+START=";
        public const string STOP = "AT+STOP";
        private static string password;
        static void Main(string[] args)
        {
            crackPassword();
            string sndMsg = null;
            int port = 13000;
            String ip = "127.0.0.1";
            Socket client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Parse(ip), port);
            client.Connect(endPoint);
            Console.WriteLine("Połączono z serwerem...");
            Program p = new Program();
            Thread t = new Thread(new ThreadStart(() => p.ReciveMessages(client)));
            t.Start();
            t.Join();
        }

        private void ReciveMessages(Socket klient)
        {
            try
            {
                while (true)
                {


                    byte[] MsgFromSrv = new byte[1024];
                    int size = klient.Receive(MsgFromSrv);
                    string recived = System.Text.Encoding.ASCII.GetString(MsgFromSrv, 0, size);
                    if ((recived == "AT+CLOSE") || (recived == STOP))
                    {
                        Console.WriteLine("Zamykanie klienta...");
                        klient.Close();
                        return;
                    }
                    //Console.WriteLine("Klient otrzymał: " + recived);
                    if (password == recived)
                    {
                        Console.WriteLine("Znaleziono tajne haslo " + recived);
                        string sndMsg = PASSWORD + recived;
                        klient.Send(System.Text.Encoding.ASCII.GetBytes(sndMsg), 0, sndMsg.Length, SocketFlags.None);
                    }
                }
                
            }
            catch (SocketException ex)
            {
                if (ex.SocketErrorCode == SocketError.ConnectionReset)
                {
                    Console.WriteLine("Klient został zamknięty siłą.");
                }
                if (ex.SocketErrorCode == SocketError.ConnectionAborted)
                {
                    //Console.WriteLine("Klient " + id + " został zamknięty siłą.");
                }
                else
                {
                    Console.WriteLine("Błąd obsługi klienta " + ex.Message);
                }
            }
            finally
            {
                Console.WriteLine("Zamykanie watku dla klienta ");
                klient.Close();
            }
        }

        public async static void crackPassword()
        {
            string filePath = "C:\\Users\\OlgierdPydynski\\source\\repos\\Rozproszone\\Haslo\\password.txt";
            try
            {

                // Check if the file exists
                if (File.Exists(filePath))
                {
                    // Read all lines from the file and join them into a single string
                    string line = File.ReadAllLines(filePath)[0];
                    password = line;
                    Console.WriteLine(line);
                }
                else
                {
                    Console.WriteLine("File not found: " + filePath);
                }
            }
            catch (Exception ex)
            {
                // Handle any exceptions that may occur during file reading
                Console.WriteLine("An error occurred: " + ex.Message);
            }
        }
    }
}
