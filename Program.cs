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

        private void pwd() {
            this.stream.Write(Encoding.ASCII.GetBytes("PWD\n"));
        }

        private async Task Read() {
            while(running) {
                try {
                    byte[] buffer = new byte[256];
                    await this.stream.ReadAsync(buffer, 0, buffer.Length);

                    string line = Encoding.ASCII.GetString(buffer);
                    Console.Write(line);
                } catch (IOException e) {
                    // network stream was closed. we are done
                    return;
                } catch(Exception e) {
                    Console.Error.WriteLine("An error occured while trying to read data: {0}", e.Message);
                }
            }
        }

        /*
         * Parses a command and performs some logic.
         *
         * return false if quitting, true otherwise */
        private bool ParseCmd(string cmd) {
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
                    this.pwd();
                    return true;
                case "":
                case "q":
                case "quit":
                case "logout":
                    return false;
                default:
                    Console.Error.WriteLine("Invalid command");
                    return true;
            }
        }

        public async Task Run() {
            Task readTask = Task.Run(this.Read);
            while(ParseCmd(Console.ReadLine()));

            this.running = false;
            this.connection.Close();

            await readTask;
        }

        public static async Task Main(string[] args) {
            if(args.Length < 1) {
                Console.WriteLine("usage: ftp target");
                return;
            }

            string address = args[0];

            Client client = new Client(address);

            await client.Run();
        }
    }
}
