using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Project_Isla___Server
{
    class Pi
    {
        //The server is running
        private static bool isrunning = true;
        public static bool isRunning { get { return isrunning; } set { isrunning = value; } }

        class Program
        {
            static void Main(string[] args)
            {
                Thread t = new Thread(() => Server.StartServer());
                t.Start();

                Server myServer = new Server();

                string s = string.Empty;

                while (isRunning)
                {
                    //Commands go here
                    s = Console.ReadLine();

                    switch (s)
                    {
                        case "stop":
                            Server.Stop();
                            break;
                    }
                    
                }

            }
        }

        class Server
        {
            public Server() { }

            private static Socket listener;

            //Reset event for accept callback
            public static ManualResetEvent acceptConnectionReset = new ManualResetEvent(false);

            //Size of data buffer
            const int BufferSize = 1024;

            //Run server on port 50000
            const int port = 50000;

            //Start and end of message delimiters
            public const string beginningDelim = "<!--STARTMESSAGE-->";
            public const string endingDelim = "<!--ENDMESSAGE-->";

            class StateObject
            {
                //Start object class
                public Socket workSocket = null;
                public byte[] buffer = new byte[BufferSize];
                public StringBuilder sb = new StringBuilder();
            }

            static bool isConnected(Socket s)
            {
                //Code for polling the socket to see if we are connected or not
                return !((s.Poll(1000, SelectMode.SelectRead) && (s.Available == 0) || !s.Connected));
            }

            static string between(string message)
            {
                //Extract the message between the start and end delimiters
                return Regex.Match(message, @"^<!--STARTMESSAGE-->(.*?)<!--ENDMESSAGE-->$").Groups[1].Value;
            }

            public static void StartServer()
            {
                //Buffer for data
                byte[] buffer = new byte[1024];

                //Start the server on port 50000
                IPEndPoint localPoint = new IPEndPoint(IPAddress.Any, port);

                //TCP Socket server
                listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                try
                {
                    listener.Bind(localPoint);

                    while (isRunning)
                    {
                        //Reset the accept connection event, locking threads

                        acceptConnectionReset.Reset();
                        //10 Connections in queue 
                        listener.Listen(10);

                        //Begin to accept the connection
                        listener.BeginAccept(new AsyncCallback(AcceptCallback), listener);

                        //Wait for the conection to finish connecting
                        acceptConnectionReset.WaitOne();
                    }

                    Console.WriteLine("Socket closed");
                    Console.Read();
                }
                catch (SocketException se)
                {
                    //Is there soemthing else running on port 50000?
                    Console.WriteLine(string.Format("Is something else running on port {0}?", port));
                    Console.WriteLine(string.Format("\n{0}", se.InnerException));
                }
                catch (Exception e)
                {
                    //Misc exception
                    Console.WriteLine(e.InnerException);
                }
                finally
                {
                    //Set isRunning = false
                    isRunning = false;

                    //Start the disconnect
                    listener.BeginDisconnect(false, new AsyncCallback(DisconnectCallback), listener);

                    //Stop the server
                    Stop(listener);
                }
            }

            public static void Stop(Socket listener)
            {
                //Check to see if the socket is connected
                if (isConnected(listener))
                {
                    //Begin the socket disconnect
                    listener.BeginDisconnect(false, new AsyncCallback(DisconnectCallback), listener);
                }

                //Set isRunning to false
                isRunning = false;

                //Close the listener
                listener.Close();
            }

            public static void Stop()
            {
                //Set isRunning to false
                isRunning = false;

                //Close the listener
                listener.Close();
            }

            public static void Send(Socket handler, string message)
            {
                StateObject so = new StateObject();

                //Create the message with beginning delim, message, and ending delim
                so.sb.Append(beginningDelim);
                so.sb.Append(message);
                so.sb.Append(endingDelim);

                //Store the built message in a string
                string send = so.sb.ToString();

                //Convert string to byte array
                byte[] byteData = Encoding.UTF8.GetBytes(send);

                //Begin to send the message
                handler.BeginSend(byteData, 0, byteData.Length, 0, new AsyncCallback(sendCallback), handler);
            }

            static void AcceptCallback(IAsyncResult ar)
            {
                Socket listener = (Socket)ar.AsyncState;

                if (listener != null)
                {
                    //End the accept
                    Socket handler = listener.EndAccept(ar);

                    //Reset the accept event
                    acceptConnectionReset.Set();

                    StateObject so = new StateObject();
                    so.workSocket = handler;

                    //Begin to receive data
                    handler.BeginReceive(so.buffer, 0, BufferSize, 0, new AsyncCallback(readCallback), so);
                }
            }

            static void readCallback(IAsyncResult ar)
            {
                StateObject so = (StateObject)ar.AsyncState;
                Socket handler = so.workSocket;

                //Check to see if the socket is connected or not
                if (!isConnected(handler))
                {
                    handler.Close();
                    return;
                }

                //Read the data
                int read = handler.EndReceive(ar);

                if (read > 0)
                {
                    //Store received data
                    so.sb.Append(Encoding.UTF8.GetString(so.buffer, 0, read));

                    if (so.sb.ToString().Contains(endingDelim)) //Check to see if the message contains the ending delim
                    {
                        string send = string.Empty;

                        //Get the message between the beginning delim and ending delim
                        string message = between(so.sb.ToString());

                        switch (message)
                        {
                            case "Hi":
                                send = "How are you?";
                                break;
                            case "Hello":
                                send = "What's up?";
                                break;
                        }

                        //Create appropriate response message
                        send = string.Format("{0}{1}{2}", beginningDelim, send, endingDelim);

                        //Convert send message to bytes
                        byte[] bytesToSend = Encoding.UTF8.GetBytes(send);

                        //Echo data back to client
                        handler.BeginSend(bytesToSend, 0, bytesToSend.Length, SocketFlags.None, new AsyncCallback(sendCallback), so);
                    }
                    else
                    {
                        //Receive more data
                        handler.BeginReceive(so.buffer, 0, BufferSize, 0, new AsyncCallback(readCallback), so);
                    }
                }
                else
                {
                    handler.Close();
                }
            }

            static void sendCallback(IAsyncResult ar)
            {
                //End the send message task
                StateObject so = (StateObject)ar.AsyncState;
                Socket handler = so.workSocket;

                handler.EndSend(ar);

                //Create object to begin to receive more data
                StateObject newSo = new StateObject();
                newSo.workSocket = handler;

                //Begin to receive data
                handler.BeginReceive(newSo.buffer, 0, BufferSize, 0, new AsyncCallback(readCallback), newSo);
            }

            static void DisconnectCallback(IAsyncResult ar)
            {
                //Disconnect
                Socket so = (Socket)ar.AsyncState;
                so.EndDisconnect(ar);
            }
        }
    }
}
