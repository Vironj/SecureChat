using Lab6;
using MagmaCBC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Lab7
{
    //------------------------------------------------------------------------------------------------
    class Program
    {
        static ServerObject server; // сервер
        static Thread listenThread; // поток для прослушивания
        static void Main(string[] args)
        {
            try
            {
                server = new ServerObject();
                listenThread = new Thread(new ThreadStart(server.Listen));
                listenThread.Start(); //старт потока
            }
            catch (Exception ex)
            {
                server.Disconnect();
                Console.WriteLine(ex.Message);
            }
        }
    }
    //------------------------------------------------------------------------------------------------
    public class ServerObject
    {
        private static byte[] KEY = new byte[32]
        {
            113, 119, 101, 114, 116, 121, 117, 105,
            111, 112, 91, 93, 97, 115, 100, 102,
            103, 104, 106, 107, 108, 59, 39, 122,
            120, 99, 118, 98, 110, 109, 44, 49
        };
        private static Magma magma = new Magma(KEY);
        static TcpListener tcpListener; // сервер для прослушивания
        List<ClientObject> clients = new List<ClientObject>(); // все подключенные клиенты

        protected internal void AddConnection(ClientObject clientObject)
        {
            clients.Add(clientObject);
        }
        protected internal void RemoveConnection(string id)
        {
            // получаем по id закрытое подключение
            ClientObject client = clients.FirstOrDefault(c => c.Login == id);
            // и удаляем его из списка подключений
            if (client != null)
                clients.Remove(client);
        }
        // прослушивание входящих подключений
        protected internal void Listen()
        {
            try
            {
                tcpListener = new TcpListener(IPAddress.Any, 8888);
                tcpListener.Start();
                Console.WriteLine("Сервер запущен. Ожидание подключений...");

                while (true)
                {
                    TcpClient tcpClient = tcpListener.AcceptTcpClient();
                    ClientObject clientObject = new ClientObject(tcpClient, this);
                    Thread clientThread = new Thread(new ThreadStart(clientObject.Process));
                    clientThread.Start();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Disconnect();
            }
        }

        // трансляция сообщения подключенным клиентам
        protected internal void BroadcastMessage(byte[] message, string id)
        {
            for (int i = 0; i < clients.Count; i++)
            {
                if (clients[i].Login != id) // если id клиента не равно id отправляющего
                {
                    clients[i].Stream.Write(message, 0, message.Length); //передача данных
                }
            }
        }

        protected internal void BroadcastEncMessage(string message, string id)
        {
            byte[] data = magma.ECBenc(Encoding.Unicode.GetBytes(message));
            for (int i = 0; i < clients.Count; i++)
            {
                if (clients[i].Login != id) // если id клиента не равно id отправляющего
                {
                    clients[i].Stream.Write(data, 0, data.Length); //передача данных
                }
            }
        }

        protected internal void UnicastMessage(string message, string id)
        {
            byte[] odata = Encoding.Unicode.GetBytes(message);
            byte[] data = magma.ECBenc(Encoding.Unicode.GetBytes(message));
            for (int i = 0; i < clients.Count; i++)
            {
                if (clients[i].Login == id) // если id клиента равно id отправляющего
                {
                    clients[i].Stream.Write(data, 0, data.Length); //передача данных
                    break;
                }
            }
        }
        // отключение всех клиентов
        protected internal void Disconnect()
        {
            tcpListener.Stop(); //остановка сервера

            for (int i = 0; i < clients.Count; i++)
            {
                clients[i].Close(); //отключение клиента
            }
            Environment.Exit(0); //завершение процесса
        }
        protected internal void Disconnect(string id)
        {
            for (int i = 0; i < clients.Count; i++)
            {
                if (clients[i].Login == id)
                {
                    clients[i].Close(); //отключение клиента
                }
            }
        }
    }
    //------------------------------------------------------------------------------------------------
    public class ClientObject
    {
        private static byte[] KEY = new byte[32]
        {
            113, 119, 101, 114, 116, 121, 117, 105,
            111, 112, 91, 93, 97, 115, 100, 102,
            103, 104, 106, 107, 108, 59, 39, 122,
            120, 99, 118, 98, 110, 109, 44, 49
        };
        private static Magma magma = new Magma(KEY);
        protected internal string Login { get; private set; }
        protected internal NetworkStream Stream { get; private set; }
        string userName;
        byte[] passwordHash = new byte[32];
        TcpClient client;
        ServerObject server; // объект сервера
        Authenticator authenticator = new Authenticator();



        public ClientObject(TcpClient tcpClient, ServerObject serverObject)
        {
            Login = Guid.NewGuid().ToString();
            client = tcpClient;
            server = serverObject;
            serverObject.AddConnection(this);
        }

        public void Process()
        {
            try
            {
                Stream = client.GetStream();// получаем имя пользователя
                string message = GetMessageInString();
                userName = message;

                byte[] byteMessage = GetMessageInBytes();//получаем хэш от пароля пользователя
                Array.Copy(byteMessage, passwordHash, passwordHash.Length);

                string result = authenticator.LogIn(userName, passwordHash);
                //TODO: условия по результату аутентификации
                switch (result)
                {
                    case "Вы вошли в систему":
                        server.UnicastMessage(result + "!", this.Login);
                        message = userName + " вошел в чат";
                        Console.WriteLine("{0} подключился к серверу.", userName);
                        // посылаем сообщение о входе в чат всем подключенным пользователям
                        server.BroadcastEncMessage(message, this.Login);
                        break;
                    case "Неверный пароль":
                        server.UnicastMessage(result + "!", this.Login);
                        server.RemoveConnection(this.Login);
                        Close();
                        break;
                    case "Пользователя с таким логином не существует":
                        server.UnicastMessage(result + "!", this.Login);
                        server.RemoveConnection(this.Login);
                        Close();
                        break;
                };

                // в бесконечном цикле получаем сообщения от клиента
                while (true)
                {
                    try
                    {
                        byte[] ByteMessage = GetMessageInBytes();
                        //message = String.Format("{0}: {1}", userName, message);
                        //Console.WriteLine(message);
                        server.BroadcastEncMessage(userName+":", this.Login);
                        server.BroadcastMessage(ByteMessage, this.Login);
                    }
                    catch
                    {
                        message = String.Format("{0} покинул чат", userName);
                        Console.WriteLine("{0} отключился от сервера.", userName);
                        server.BroadcastEncMessage(message, this.Login);
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            finally
            {
                // в случае выхода из цикла закрываем ресурсы
                server.RemoveConnection(this.Login);
                Close();
            }
        }

        // чтение входящего сообщения и преобразование в строку
        private string GetMessageInString()
        {
            byte[] data = new byte[280]; // буфер для получаемых данных
            int bytes = 0;
            do
            {
                bytes = Stream.Read(data, 0, data.Length);
            }
            while (Stream.DataAvailable);
            byte[] Data = new byte[bytes];
            Array.Copy(data, Data, bytes);
            return Encoding.Unicode.GetString(Data);
        }

        private byte[] GetMessageInBytes()
        {
            byte[] data = new byte[280]; // буфер для получаемых данных
            int bytes = 0;
            do
            {
                bytes = Stream.Read(data, 0, data.Length);
            }
            while (Stream.DataAvailable);
            byte[] cutted = new byte[bytes];
            Array.Copy(data, cutted, cutted.Length);
            return cutted;
        }

        // закрытие подключения
        protected internal void Close()
        {
            if (Stream != null)
                Stream.Close();
            if (client != null)
                client.Close();
        }
    }
}