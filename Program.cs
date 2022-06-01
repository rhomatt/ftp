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

        private void user(string username) {
            string sendString = "USER " + username + '\n';
            this.stream.Write(Encoding.ASCII.GetBytes(sendString));
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
        private bool ParseCmd(string line) {
            if(line.Length <= 0)
                return true;

            string[] args = line.Split(' ');
            string cmd = args[0];

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
                case "logout":
                    return false;
                case "user":
                    if(args.Length < 2) {
                        Console.WriteLine("usage: user USERNAME");
                        break;
                    }

                    this.user(args[1]);
                    break;
                default:
                    Console.Error.WriteLine("Invalid command");
                    break;
            }

            return true;
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
