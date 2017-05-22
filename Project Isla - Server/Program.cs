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
    class Program
    {
        static void Main(string[] args)
        {
            Server.StartServer();
        }
    }

    class Server
    {
        public Server() { }

        private static Socket listener;
        public static ManualResetEvent mre = new ManualResetEvent(false);
        const int BufferSize = 1024;
        const int port = 50000;
        bool isRunning = true;
        public const string beginningDelim = "<!--STARTMESSAGE-->";
        public const string endingDelim = "<!--ENDMESSAGE-->";

        class StateObject
        {
            public Socket workSocket = null;
            public byte[] buffer = new byte[BufferSize];
            public StringBuilder sb = new StringBuilder();
        }

        static bool isConnected(Socket s)
        {
            return !((s.Poll(1000, SelectMode.SelectRead) && (s.Available == 0) || !s.Connected));
        }

        static string between(string message)
        {
            return Regex.Match(message, @"^<!--STARTMESSAGE-->(.*?)<!--ENDMESSAGE-->$").Groups[1].Value;
        }

        public static void StartServer()
        {
            byte[] buffer = new byte[1024];
            IPEndPoint localPoint = new IPEndPoint(IPAddress.Any, port);
            listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            try
            {
                listener.Bind(localPoint);

                while (true)
                {
                    mre.Reset();
                    listener.Listen(10);
                    listener.BeginAccept(new AsyncCallback(AcceptCallback), listener);
                }
            }
            catch (SocketException se)
            {
                Console.WriteLine(string.Format("Is something else running on port {0}?", port));
                Console.WriteLine(string.Format("\n{0}", se.InnerException));
            }
            catch (Exception e)
            {
                Console.WriteLine(e.InnerException);
            }
        }

        public static void Send(Socket handler, string message)
        {
            StateObject so = new StateObject();

            so.sb.Append(beginningDelim);
            so.sb.Append(message);
            so.sb.Append(endingDelim);
            string send = so.sb.ToString();

            byte[] byteData = Encoding.UTF8.GetBytes(send);

            handler.BeginSend(byteData, 0, byteData.Length, 0, new AsyncCallback(sendCallback), handler);
        }

        static void AcceptCallback(IAsyncResult ar)
        {
            Socket listener = (Socket)ar.AsyncState;

            if (listener != null)
            {
                Socket handler = listener.EndAccept(ar);
                mre.Set();

                StateObject so = new StateObject();
                so.workSocket = handler;
                handler.BeginReceive(so.buffer, 0, BufferSize, 0, new AsyncCallback(readCallback), so);
            }
        }

        static void readCallback(IAsyncResult ar)
        {
            StateObject so = (StateObject)ar.AsyncState;
            Socket handler = so.workSocket;

            if (!isConnected(handler))
            {
                handler.Close();
                return;
            }

            int read = handler.EndReceive(ar);

            if (read > 0)
            {
                so.sb.Append(Encoding.UTF8.GetString(so.buffer, 0, read));

                if (so.sb.ToString().Contains(endingDelim))
                {
                    string send = string.Empty;
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

                    send = string.Format("{0}{1}{2}", beginningDelim, send, endingDelim);

                    byte[] bytesToSend = Encoding.UTF8.GetBytes(send);

                    handler.BeginSend(bytesToSend, 0, bytesToSend.Length, SocketFlags.None, new AsyncCallback(sendCallback), so);

                }
                else
                {
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
            StateObject so = (StateObject)ar.AsyncState;
            Socket handler = so.workSocket;

            handler.EndSend(ar);

            StateObject newSo = new StateObject();
            newSo.workSocket = handler;
            handler.BeginReceive(newSo.buffer, 0, BufferSize, 0, new AsyncCallback(readCallback), newSo);
        }
    }
}
