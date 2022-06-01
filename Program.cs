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

        private void FTPCmd(string cmd, params string[] args) {
            StringBuilder sb = new();

            sb.Append(cmd);
            foreach(string arg in args) {
                sb.Append(' ');
                sb.Append(arg);
            }
            sb.Append('\n');

            this.stream.Write(Encoding.ASCII.GetBytes(sb.ToString()));
        }

        private void PrintHelp() {
            string[] help_lines = {
                "Help:",
                "?              display this message",
                "a(scii)        set ASCII transfer type",
                "b(inary)       set binary transfer type",
                "cd             change working directory",
                "cdup           same as cd ..",
                "debug          toggle debug mode",
                "dir            list contents of directory. If no arg given, assume cwd",
                "get <path>     get a remote file",
                "h(elp)         same as ?",
                "login <user>   logs in as <user>. prompts for a password",
                "logout         closes this client gracefully",
                "ls             same as dir",
                "passive        toggle active/passive mode",
                "pwd            prints working directory",
                "q(uit)         same as logout",
                "user <user>    same as login"
            };

            foreach(string line in help_lines)
                Console.WriteLine(line);
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

            string[] args = line.Split(' ', StringSplitOptions.TrimEntries);
            string cmd = args[0];

            try {
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
                        string target = args.Length == 1 ? "" : line.Split(' ', 2)[1];

                        FTPCmd("LIST", target);
                        break;
                    case "get":
                        break;
                    case "?":
                    case "h":
                    case "help":
                        PrintHelp();
                        break;
                    case "passive":
                        break;
                    case "pwd":
                        FTPCmd("PWD");
                        break;
                    case "q":
                    case "quit":
                    case "logout":
                        FTPCmd("QUIT");
                        return false;
                    case "user":
                    case "login":
                        FTPCmd("USER", args[1]);

                        string password = Console.ReadLine();
                        FTPCmd("PASS", password);
                        break;
                    default:
                        Console.Error.WriteLine("Invalid command");
                        break;
                }
            } catch (Exception e) {
                Console.Error.WriteLine("Invalid use of {0}", cmd);
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
