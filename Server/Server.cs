using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PIAPI;

namespace Project_Isla___Server
{
    public class Server
    {
        private static Socket listener;
        public static Socket Listener
        {
            get => listener;
            set
            {
                if (listener == value)
                    return;
                listener = value;
            }
        }

        //The server is running
        private static bool isrunning;
        public static bool isRunning
        {
            get => isrunning;
            set
            {
                if (isrunning == value)
                    return;
                isrunning = value;
            }
        }

        //Size of data buffer
        private static int bufferSize;
        public static int BufferSize
        {
            get => bufferSize;
            set
            {
                if (bufferSize == value)
                    return;
                bufferSize = value;
            }
        }

        //Run server on port 50000
        private static int port;
        public static int Port
        {
            get => port;
            set
            {
                if (port == value)
                    return;
                port = value;
            }
        }

        //Reset event for accept callback
        private static ManualResetEvent acceptConnectionReset = new ManualResetEvent(false);
        private static ManualResetEvent disconnectResetEvent = new ManualResetEvent(false);

        public Server()
        {
            isRunning = true;
            Port = 50000;
            BufferSize = 1024;
        }

        public Server(int port, int bufferSize = 1024)
        {
            isRunning = true;
            Port = port;
            BufferSize = bufferSize;
        }

        class StateObject
        {
            //Start object class
            public Socket workSocket;
            public byte[] buffer = new byte[BufferSize];
            public StringBuilder sb = new StringBuilder();
        }

        public void StartServer()
        {
            //Buffer for data
            var buffer = new byte[BufferSize];

            //Start the server on port 50000
            var localPoint = new IPEndPoint(IPAddress.Any, port);

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

                    Console.WriteLine("Waiting for a connection...");

                    //Begin to accept the connection
                    listener.BeginAccept(new AsyncCallback(AcceptCallback), listener);

                    //Wait for the connection to finish connecting
                    acceptConnectionReset.WaitOne();
                }

                Console.WriteLine("Socket closed");
                Console.Read();
            }
            catch (SocketException se)
            {
                //Is there something else running on port 50000?
                Console.WriteLine(string.Format("Is something else running on port {0}?", port));
                Console.WriteLine(string.Format("\n{0}", se.InnerException));

                //Stop the server
                Stop(listener);
            }
            catch (Exception e)
            {
                //Misc exception
                Console.WriteLine(e.InnerException);

                //Stop the server
                Stop(listener);
            }

            //Stop the server when the while loop breaks
            Stop(listener);
        }

        void Stop(Socket listener)
        {
            //Check to see if the socket is connected
            if (Networking.isConnected(listener))
            {
                try
                {
                    //Begin the socket disconnect
                    listener.BeginDisconnect(false, new AsyncCallback(DisconnectCallback), listener);
                    disconnectResetEvent.WaitOne();

                }
                catch (Exception e)
                {
                    Console.WriteLine(e.InnerException);
                }
            }

            //Set isRunning to false
            isRunning = false;

            listener.Close();

            //Allow the thread to continue and shutdown the server
            acceptConnectionReset.Set();
        }

        public void Stop()
        {
            //Set isRunning to false
            isRunning = false;

            //Allow the thread to continue and shutdown the server
            acceptConnectionReset.Set();
        }

        void Send(Socket handler, string message)
        {
            var so = new StateObject();

            //Create the message with beginning delimiter, message, and ending delimiter
            so.sb.Append(PIAPI.String.BeginningDelimiter);
            so.sb.Append(message);
            so.sb.Append(PIAPI.String.EndingDelimiter);

            //Store the built message in a string
            var send = so.sb.ToString();

            //Convert string to byte array
            var byteData = Encoding.UTF8.GetBytes(send);

            //Begin to send the message
            handler.BeginSend(byteData, 0, byteData.Length, 0, new AsyncCallback(sendCallback), handler);
        }

        void AcceptCallback(IAsyncResult ar)
        {
            var listener = (Socket)ar.AsyncState;

            if (listener != null)
            {
                try
                {
                    var handler = listener.EndAccept(ar);
                    Console.WriteLine("Connection Established");

                    //Reset the accept event
                    acceptConnectionReset.Set();

                    var so = new StateObject();
                    so.workSocket = handler;

                    //Begin to receive data
                    handler.BeginReceive(so.buffer, 0, BufferSize, 0, new AsyncCallback(readCallback), so);
                }
                catch (ObjectDisposedException ode)
                {
                    Console.WriteLine("Cannot access disposed socket object");
                    Console.WriteLine(ode.InnerException);
                }
            }
        }

        void readCallback(IAsyncResult ar)
        {
            var so = (StateObject)ar.AsyncState;
            var handler = so.workSocket;

            //Check to see if the socket is connected or not
            if (!Networking.isConnected(handler))
            {
                handler.Close();
                return;
            }

            //Read the data
            var read = handler.EndReceive(ar);

            if (read > 0)
            {
                //Store received data
                so.sb.Append(Encoding.UTF8.GetString(so.buffer, 0, read));

                if (so.sb.ToString().Contains(PIAPI.String.EndingDelimiter)) //Check to see if the message contains the ending delimiter
                {
                    var send = string.Empty;

                    //Get the message between the beginning delimiter and ending delimiter
                    var message = PIAPI.String.Between(so.sb.ToString());

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
                    send = string.Format("{0}{1}{2}", PIAPI.String.BeginningDelimiter, send, PIAPI.String.EndingDelimiter);

                    //Convert send message to bytes
                    var bytesToSend = Encoding.UTF8.GetBytes(send);

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

        void sendCallback(IAsyncResult ar)
        {
            //End the send message task
            var so = (StateObject)ar.AsyncState;
            var handler = so.workSocket;

            handler.EndSend(ar);

            //Create object to begin to receive more data
            var newSo = new StateObject();
            newSo.workSocket = handler;

            //Begin to receive data
            handler.BeginReceive(newSo.buffer, 0, BufferSize, 0, new AsyncCallback(readCallback), newSo);
        }

        void DisconnectCallback(IAsyncResult ar)
        {
            //Disconnect
            var so = (Socket)ar.AsyncState;
            so.EndDisconnect(ar);

            disconnectResetEvent.Set();
        }
    }
}
