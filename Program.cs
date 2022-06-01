using System;
using System.Net.Sockets;

namespace FtpClient {
    class Client {

        TcpClient connection;

        public Client(string address) {
            this.connection = new TcpClient(address, 21);
        }

        /*
         * Parses a command and performs some logic.
         *
         * return false if quitting, true otherwise
         */
        public bool ParseCmd(string cmd) {
            switch(cmd) {
                case "a":
                case "ascii":
                    return true;
                case "b":
                case "binary":
                    return true;
                case "cd":
                    return true;
                case "cdup":
                    return true;
                case "debug":
                    return true;
                case "ls":
                case "dir":
                    return true;
                case "get":
                    return true;
                case "?":
                case "h":
                case "help":
                    return true;
                case "passive":
                    return true;
                case "pwd":
                    return true;
                case "q":
                case "quit":
                    return false;
            }

            return true;
        }

        public static void Main(string[] args) {
            if(args.Length < 1) {
                Console.WriteLine("usage: ftp target");
                return;
            }

            string address = args[0];
            bool cont = true;

            Client client = new Client(address);

            while(cont) {
                cont = client.ParseCmd(Console.ReadLine());
            }
        }
    }
}
