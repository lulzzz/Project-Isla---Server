using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using PIAPI;

namespace Project_Isla___Server
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Starting server...");

            var server = new Server();

            var t = new Thread(() => server.StartServer());
            t.Start();

            var s = string.Empty;

            while (Server.isRunning)
            {
                //Commands go here
                s = Console.ReadLine();

                switch (s)
                {
                    case "stop":
                        server.Stop();
                        break;
                }

            }

        }
    }
}

