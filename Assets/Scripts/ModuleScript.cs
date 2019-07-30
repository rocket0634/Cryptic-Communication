using KModkit;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class ModuleScript : MonoBehaviour {

    private static int _moduleIDCounter = 1;
    private int _moduleID;
    public KMBombInfo Info;
    private BombInfo clientInfo = new BombInfo();
    public KMBombModule Module;
    public KMAudio Audio;
    public KMSelectable SendButton, ReceiveButton;
    private bool active, connected, available;
    private string currentSend = "", error = "No information is available from your connection.\nDid you accidentally receive instead of send?", response = string.Empty;
    private List<string> console = new List<string>();

    private static IPHostEntry ipHostInfo = Dns.GetHostEntry("localhost");
    private static IPAddress ipAddress = ipHostInfo.AddressList[0];
    private static IPEndPoint remoteEP = new IPEndPoint(ipAddress, 8080);
    private Socket client;

    ManualResetEvent connectDone = new ManualResetEvent(false), sendDone = new ManualResetEvent(false), receiveDone = new ManualResetEvent(false);
    

    void Start ()
    {
        _moduleID = _moduleIDCounter++;
        Module.OnActivate += delegate () { Activate(); };
    }

    void Activate()
    {
        SendButton.OnInteract = delegate() { StartClient(true); return false; };
        ReceiveButton.OnInteract = delegate () { StartClient(false); return false; };
        active = true;
        available = true;
    }

    /*IEnumerator Counter()
    {
        while (true)
        {
            yield return new WaitUntil(() => startTimer);
            DebugLog("Blah");
            timer = false;
            yield return new WaitForSeconds(6);
            DebugLog("Blah2");
            timer = true;
            startTimer = false;
        }
    }*/

    void StartClient(bool send)
    {
        if (!available || !active) return;
        available = false;
        try
        {
            // Create a TCP/IP socket.  
            client = new Socket(ipAddress.AddressFamily,
                SocketType.Stream, ProtocolType.Tcp);
            // Connect to the remote endpoint.  
            client.BeginConnect(remoteEP,
                new AsyncCallback(ConnectCallback), client);
            connectDone.WaitOne(6000);
            if (!connected) return;

            if (send)
            {
                string info;
                if (clientInfo != null && clientInfo.Serial == Info.GetSerialNumber() && clientInfo.Stage < 4)
                {
                    clientInfo.Stage++;
                }
                else
                {
                    clientInfo = new BombInfo
                    {
                        ID = client.Handle.ToInt32(),
                        Serial = Info.GetSerialNumber(),
                        Stage = 1
                    };
                }
                info = JsonConvert.SerializeObject(clientInfo, Formatting.Indented);
                currentSend = info;
                Send(client, info + "<EOF>");
                sendDone.WaitOne(6000);
            }
            else
            {
                if (clientInfo == null)
                {
                    DebugLog(true, "There was an error in the connection. Sending again may fix this issue.");
                    ResetConnection();
                    return;
                }
                currentSend = string.Empty;
                Send(client, clientInfo.ID.ToString());
                sendDone.WaitOne(6000);
                Receive(client);
                receiveDone.WaitOne(6000);
                DebugLog(true, "Response received: {0}", response);
                if (response != error)
                {
                    clientInfo = JsonConvert.DeserializeObject<BombInfo>(response);
                    if (clientInfo.Stage == 4) Module.HandlePass();
                }
            }
            
            response = string.Empty;
            ResetConnection();
            DebugLog(false, "Socket closed.");
        }
        catch (Exception e)
        {
            if (e is SocketException) { DebugLog(true, "Connection lost."); DebugLog(false, e.Message); }
            else if (e is NullReferenceException) { DebugLog(true, "NullReferenceException detected - it is likely the server did not respond with a proper message."); DebugLog(false, e.Message); }
            else DebugLog(true, e.Message);
            ResetConnection();
        }
    }

    void ResetConnection()
    {
        connectDone.Reset();
        sendDone.Reset();
        receiveDone.Reset();
        if (connected)
        {
            client.Shutdown(SocketShutdown.Both);
            client.Close();
            connected = false;
        }
        available = true;
    }

    void ConnectCallback(IAsyncResult ar)
    {
        try
        {
            Socket client = (Socket)ar.AsyncState;

            client.EndConnect(ar);

            DebugLog(false, "Socket connected to {0}",
                client.RemoteEndPoint.ToString());

            connectDone.Set();
            connected = true;
        }
        catch (Exception e)
        {
            ResetConnection();
            if (e is SocketException)
            {
                DebugLog(true, "Connection failed.");
                DebugLog(false, e.Message);
                return;
            }
            DebugLog(true, e.Message);
        }
    }

    void Send(Socket handler, string data)
    {
        byte[] byteData = Encoding.ASCII.GetBytes(data);
        handler.BeginSend(byteData, 0, byteData.Length, 0,
            new AsyncCallback(SendCallback), handler);
    }

    void Receive(Socket client)
    {
        try
        {
            StateObject state = new StateObject
            {
                workSocket = client
            };

            client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                new AsyncCallback(ReceiveCallback), state);
        }
        catch (Exception e)
        {
            ResetConnection();
            DebugLog(true, e.Message);
        }
    }

    void SendCallback(IAsyncResult ar)
    {
        try
        {
            Socket handler = (Socket)ar.AsyncState;

            int bytesSent = handler.EndSend(ar);
            DebugLog(false, "Sent {0} bytes to the server.", bytesSent);
            if (currentSend != string.Empty) DebugLog(false, "Sent following information to server: {0}", currentSend);
            sendDone.Set();
        }
        catch (Exception e)
        {
            ResetConnection();
            DebugLog(true, e.Message);
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
            ResetConnection();
            if (e is ObjectDisposedException) { DebugLog(false, e.Message); return; }
            DebugLog(true, e.Message);
        }
    }

    void DebugLog(bool show, string log, params object[] args)
    {
        var logData = string.Format(log, args);
        var modName = string.Format(show ? "[{0}]" : "<{0}>", "Cryptic Communication #" + _moduleID);
        Debug.LogFormat("{0} {1}", modName, logData);
    }

    class BombInfo
    {
        public int ID;
        public string Serial;
        public int Stage;
    }

    class StateObject
    {
        public Socket workSocket = null;
        public const int BufferSize = 256;
        public byte[] buffer = new byte[BufferSize];
        public StringBuilder sb = new StringBuilder();
    }
}