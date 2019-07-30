using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace CryptServerBeta
{
    class Server
    {
        public ManualResetEvent allDone = new ManualResetEvent(false);
        public ManualResetEvent connectDone = new ManualResetEvent(false);
        public ManualResetEvent sendDone = new ManualResetEvent(false);
        public ManualResetEvent receiveDone = new ManualResetEvent(false);
        bool searching = false;
        BombInfo clientinfo = new BombInfo{ Serial = "" };
        const string error = "No information is available from your connection.\nDid you accidentally receive instead of send?";
        static string exFile;
        static Thread ClientCheck;
        static Thread ServCheck;
        static Dictionary<int, BombInfo> Database = new Dictionary<int, BombInfo>();

        string response = string.Empty;

        static void Main(string[] args)
        {
            var curDir = Directory.GetCurrentDirectory();
            var exDir = Path.Combine(curDir, "Data");
            if (!Directory.Exists(exDir)) Directory.CreateDirectory(exDir);
            exFile = Path.Combine(exDir, "Data.Json");
            if (!File.Exists(exFile)) File.WriteAllText(exFile, JsonConvert.SerializeObject(Database));
            var deserialized = JsonConvert.DeserializeObject<Dictionary<int, BombInfo>>(File.ReadAllText(exFile), new JsonSerializerSettings { Error = (object sender, Newtonsoft.Json.Serialization.ErrorEventArgs args2) => args2.ErrorContext.Handled = true });
            if (deserialized != null) Database = deserialized;

            // The code provided will print ‘Hello World’ to the console.
            // Press Ctrl+F5 (or go to Debug > Start Without Debugging) to run your app.
            Console.WriteLine("Server: Hello World!");
            var serv = new Server();
            ServCheck = new Thread(serv.StartListening);
            ClientCheck = new Thread(serv.ClientThread);
            ServCheck.Start();
            ClientCheck.Start();

            // Go to http://aka.ms/dotnet-get-started-console to continue learning how to build a console app! 
        }

        void StartListening()
        {
            IPHostEntry ipHostInfo = Dns.GetHostEntry("localhost");
            IPEndPoint localEP = new IPEndPoint(ipHostInfo.AddressList[0], 8080);
            //IPEndPoint localEP = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8080);
            Console.WriteLine("Server: Local address and port: {0}", localEP.ToString());
            Socket listener = new Socket(localEP.Address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                listener.Bind(localEP);
                listener.Listen(10);

                while(true)
                {
                    allDone.Reset();
                    Console.WriteLine("Server: Waiting for a connection...");
                    listener.BeginAccept(new AsyncCallback(AcceptCallback), listener);
                    searching = true;
                    allDone.WaitOne();
                }
            } catch (Exception e)
            {
                Console.WriteLine("Server: " + e.ToString());
            }
            searching = false;
            Console.WriteLine("Server: Closing the Listener...");
        }

        void ClientThread()
        {
            while (true)
            {
                var input = Console.ReadLine();
                if (input == "send") StartClient(true);
                else if (input == "receive") StartClient(false);
                else if (input == "clear")
                {
                    Database.Clear();
                    Console.WriteLine("Database and JSON were cleared.");
                    File.WriteAllText(exFile, JsonConvert.SerializeObject(Database, Formatting.Indented));
                }
                else if (input == "read")
                {
                    var text = JsonConvert.SerializeObject(Database, Formatting.Indented);
                    Console.WriteLine(text == "{}" ? "Empty" : text);
                }
            }
        }

        void StartClient(bool send)
        {
            while (!searching) { }
            try
            {
                IPHostEntry ipHostInfo = Dns.GetHostEntry("localhost");
                IPAddress ipAddress = ipHostInfo.AddressList[0];
                IPEndPoint remoteEP = new IPEndPoint(ipAddress, 8080);

                // Create a TCP/IP socket.  
                Socket client = new Socket(ipAddress.AddressFamily,
                    SocketType.Stream, ProtocolType.Tcp);

                // Connect to the remote endpoint.  
                client.BeginConnect(remoteEP,
                    new AsyncCallback(ConnectCallback), client);
                connectDone.WaitOne();

                if (send)
                {
                    var serial = Console.ReadLine();
                    string info;
                    if (clientinfo.Serial == serial && clientinfo.Stage < 4)
                    {
                        clientinfo.Stage++;
                    }
                    else
                    {
                        clientinfo = new BombInfo
                        {
                            ID = client.Handle.ToInt32(),
                            Serial = serial,
                            Stage = 1
                        };
                    }
                    info = JsonConvert.SerializeObject(clientinfo, Formatting.Indented);
                    Send(client, info + "<EOF>", false);
                    sendDone.WaitOne();
                }
                else {
                    Send(client, clientinfo.ID.ToString(), false);
                    sendDone.WaitOne();
                    Receive(client);
                    receiveDone.WaitOne();
                    Console.WriteLine("Client: Response received: {0}", response);
                    if (response != error)
                    {
                        clientinfo = JsonConvert.DeserializeObject<BombInfo>(response);
                        if (clientinfo.Stage == 4) clientinfo = new BombInfo { ID = -1, Serial = "PD82E7", Stage = 1 };
                    }
                }
 
                client.Shutdown(SocketShutdown.Both);
                client.Close();
                response = string.Empty;
                connectDone.Reset();
                sendDone.Reset();
                receiveDone.Reset();
                Console.WriteLine("Client: Socket closed.");
            }
            catch (Exception e)
            {
                Console.WriteLine("Client: " + e.ToString());
            }
        }

        void ConnectCallback(IAsyncResult ar)
        {
            try
            { 
                Socket client = (Socket)ar.AsyncState;
                
                client.EndConnect(ar);

                Console.WriteLine("Client: Socket connected to {0}",
                    client.RemoteEndPoint.ToString());
 
                connectDone.Set();
            }
            catch (Exception e)
            {
                Console.WriteLine("Client: " + e.ToString());
            }
        }

        void AcceptCallback(IAsyncResult ar)
        {
            allDone.Set();

            Socket listener = (Socket)ar.AsyncState;
            Socket handler = listener.EndAccept(ar);

            StateObject state = new StateObject
            {
                workSocket = handler
            };
            handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReadCallback), state);
        }

        void Receive(Socket client)
        {
            try
            {
                StateObject state = new StateObject();
                state.workSocket = client;
 
                client.BeginReceive(state.buffer, 0, 256, 0,
                    new AsyncCallback(ReceiveCallback), state);
            }
            catch (Exception e)
            {
                Console.WriteLine("Client: " + e.ToString());
            }
        }

        void ReceiveCallback(IAsyncResult ar)
        {
            try
            {
                StateObject state = (StateObject)ar.AsyncState;
                Socket client = state.workSocket;
 
                int bytesRead = client.EndReceive(ar);

                if (bytesRead > 0)
                {
                    state.sb.Append(Encoding.ASCII.GetString(state.buffer, 0, bytesRead));
                    
                    client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                        new AsyncCallback(ReceiveCallback), state);
                }
                else
                { 
                    if (state.sb.Length > 1)
                    {
                        response = state.sb.ToString();
                    }
                    receiveDone.Set();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Client: " + e.ToString());
            }
        }

        void ReadCallback(IAsyncResult ar)
        {
            string content = string.Empty;
            StateObject state = (StateObject)ar.AsyncState;
            Socket handler = state.workSocket;

            int read = handler.EndReceive(ar);

            if (read > 0)
            {
                state.sb.Append(Encoding.ASCII.GetString(state.buffer, 0, read));

                content = state.sb.ToString();
                if (content.StartsWith("GET"))
                {
                    Console.WriteLine(content);
                    var response = "<!DOCTYPE html><html><head><meta http-equiv=\"content-type\" content=\"text/html; charset=UTF-8\">" +
                        "<meta charset=\"utf-8\">" +
                        "<title>Cryptic Communication Request</title>" +
                        "</head>" +
                        "<body>" +
                        "Test</body>" +
                        "</html>";
                    Console.WriteLine(response);
                    Send(handler, response, true);
                    return;
                }
                if (!content.Contains("<EOF>"))
                {
                    if (Database.ContainsKey(int.Parse(content)))
                    {
                        content = JsonConvert.SerializeObject(Database[int.Parse(content)], Formatting.Indented);
                        Send(handler, content, true);
                    }
                    else if (content == "alive")
                    {
                        Send(handler, "yes", true);
                    } 
                    else
                    {
                        Send(handler, error, true);
                    }
                    return;
                }
                if (content.IndexOf("<EOF>") > -1)
                {
                    content = content.Replace("<EOF>", "");
                    Console.WriteLine("Server: Read {0} bytes from socket. \n Data : {1}",
                        content.Length, content);
                    var info = JsonConvert.DeserializeObject<BombInfo>(content);
                    if (info.Stage == 4)
                    {
                        if (Database.ContainsKey(info.ID)) Database.Remove(info.ID);
                    }
                    else
                    {
                        if (!Database.ContainsKey(info.ID))
                            Database.Add(info.ID, info);
                        Database[info.ID] = info;
                    }
                    File.WriteAllText(exFile, JsonConvert.SerializeObject(Database, Formatting.Indented));
                    handler.Shutdown(SocketShutdown.Both);
                    handler.Close();
                    Console.WriteLine("Server: Socket closed");
                }
                else
                { 
                    handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                    new AsyncCallback(ReadCallback), state);
                }

            }
        }

        void Send(Socket handler, string data, bool server)
        {
            byte[] byteData = Encoding.ASCII.GetBytes(data);
            handler.BeginSend(byteData, 0, byteData.Length, 0,
                new AsyncCallback(x => SendCallback(x, server)), handler);
        }

        void SendCallback(IAsyncResult ar, bool server)
        {
            try
            { 
                Socket handler = (Socket)ar.AsyncState;
                
                int bytesSent = handler.EndSend(ar);
                Console.WriteLine("{1}: Sent {0} bytes to {2}.", bytesSent, server ? "Server" : "Client", server ? "client" : "server");

                if (server)
                {
                    handler.Shutdown(SocketShutdown.Both);
                    handler.Close();
                    Console.WriteLine("Server: Socket closed");
                }  
                else sendDone.Set();
            }
            catch (Exception e)
            {
                Console.WriteLine((server ? "Server: " : "Client: ") + e.ToString());
            }
        }
        
        class StateObject
        {
            public Socket workSocket = null;
            public const int BufferSize = 1024;
            public byte[] buffer = new byte[BufferSize];
            public StringBuilder sb = new StringBuilder();
        }

        class BombInfo
        {
            public int ID;
            public string Serial;
            public int Stage;
        }
    }
}
