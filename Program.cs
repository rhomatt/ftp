using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace FtpClient {
    class Client {

        TcpClient connection;
        NetworkStream stream;
        bool running = true;

        public Client(string address) {
            this.connection = new TcpClient(address, 21);
            this.stream = this.connection.GetStream();
        }

        public void pwd() {
            this.stream.Write(Encoding.ASCII.GetBytes("PWD\n"));
        }

        private async Task flusher() {
            while(true) {
                byte[] buffer = new byte[256];
                await this.stream.ReadAsync(buffer);

                string line = Encoding.ASCII.GetString(buffer);
                Console.Write(line);
            }
        }

        public async Task run() {
            await Task.Run(flusher);
        }

        /*
         * Parses a command and performs some logic.
         *
         * sets running to false if quitting
         */
        public void ParseCmd(string cmd) {
            switch(cmd) {
                case "a":
                case "ascii":
                    break;
                case "b":
                case "binary":
                    break;
                case "cd":
                    break;
                case "cdup":
                    break;
                case "debug":
                    break;
                case "ls":
                case "dir":
                    break;
                case "get":
                    break;
                case "?":
                case "h":
                case "help":
                    break;
                case "passive":
                    break;
                case "pwd":
                    this.pwd();
                    break;
                case "q":
                case "quit":
                    this.running = false;
                    break;
            }
        }

        public static void Main(string[] args) {
            if(args.Length < 1) {
                Console.WriteLine("usage: ftp target");
                return;
            }

            string address = args[0];

            Client client = new Client(address);
            client.run();

            while(client.running) {
                client.ParseCmd(Console.ReadLine());
            }
        }
    }
}
