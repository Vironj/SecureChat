using Lab2;
using MagmaCBC;
using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Lab7Client
{
    class Program
    {
        static string userName;
        private const string host = "127.0.0.1";
        private const int port = 8888;
        static TcpClient client;
        static NetworkStream stream;
        private static byte[] KEY = new byte[32] 
        {
            113, 119, 101, 114, 116, 121, 117, 105,
            111, 112, 91, 93, 97, 115, 100, 102,
            103, 104, 106, 107, 108, 59, 39, 122,
            120, 99, 118, 98, 110, 109, 44, 49
        };
        private static Magma magma = new Magma(KEY);
        static void Main(string[] args)
        {
            
            client = new TcpClient();
            try
            {
                Console.Write("Введите свое имя: ");
                userName = Console.ReadLine();
                client.Connect(host, port); //подключение клиента
                stream = client.GetStream(); // получаем поток

                string message = userName;
                byte[] data = Encoding.Unicode.GetBytes(message);
                stream.Write(data, 0, data.Length);//отправка логина серверу

                Console.Write("Введите ваш пароль: ");
                string password = Console.ReadLine();
                message = password;
                data = Encoding.Unicode.GetBytes(message);
                data = SHA256.getHash(data);
                stream.Write(data, 0, data.Length);//отправка пароля серверу

                // запускаем новый поток для получения данных
                Thread receiveThread = new Thread(new ThreadStart(ReceiveMessage));
                receiveThread.Start(); //старт потока
                //Console.WriteLine("Добро пожаловать, {0}", userName);
                Thread.Sleep(1000);
                SendMessage();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                Disconnect();
            }
        }
        // отправка сообщений
        static void SendMessage()
        {

            Console.WriteLine("Введите сообщение: ");

            while (true)
            {
                string message = Console.ReadLine();
                byte[] data = magma.ECBenc(Encoding.Unicode.GetBytes(message));
                stream.Write(data, 0, data.Length);
            }
        }
        // получение сообщений
        static void ReceiveMessage()
        {
            while (true)
            {
                try
                {
                    byte[] data = new byte[280]; // буфер для получаемых данных
                    StringBuilder builder = new StringBuilder();
                    int bytes = 0;
                    do
                    {
                        bytes = stream.Read(data, 0, data.Length);
                    }
                    while (stream.DataAvailable);
                        byte[] DecData = new byte[bytes];
                        Array.Copy(data,DecData,bytes);
                        DecData = magma.ECBdec(DecData);

                    if (bytes ==0)
                    {
                        Console.WriteLine("Сервер вас отключил!");
                        Disconnect();
                    }
                    string message = Encoding.Unicode.GetString(DecData);
                    Console.WriteLine(message);//вывод сообщения

                }
                catch
                {
                    Console.WriteLine("Подключение прервано!"); //соединение было прервано
                    Console.ReadLine();
                    Disconnect();
                }
            }
        }

        static void Disconnect()
        {
            if (stream != null)
                stream.Close();//отключение потока
            if (client != null)
                client.Close();//отключение клиента
            Environment.Exit(0); //завершение процесса
        }
    }
}