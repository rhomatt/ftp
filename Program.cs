using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace FtpClient {
	class Client {

		TcpClient connection;
		NetworkStream stream;
		bool running = true;
		bool passive = false; // start in active mode
		IPAddress address;

		public Client(string address, int port) {
			IPHostEntry hostEntries = Dns.GetHostEntry(address);
			foreach(IPAddress curraddr in hostEntries.AddressList)
				if(curraddr.AddressFamily.ToString() == ProtocolFamily.InterNetwork.ToString())
					this.address = curraddr;

			this.connection = new TcpClient();
			this.connection.Connect(this.address, port);
			this.stream = this.connection.GetStream();

			this.Read(); // read the initial help message
		}

		private void FTPCmd(string cmd, bool read, params string[] args) {
			StringBuilder sb = new StringBuilder();

			sb.Append(cmd);
			foreach(string arg in args) {
				sb.Append(' ');
				sb.Append(arg);
			}
			sb.Append("\r\n");

			this.stream.Write(Encoding.ASCII.GetBytes(sb.ToString()));
			if(read)
				this.Read();
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

		// returns local ipv4 address if usev6 is false. else returns local ipv6 address
		private IPAddress getLocalIP(bool usev6) {
			IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
			foreach (IPAddress address in host.AddressList) {
				ProtocolFamily ipType = usev6 ? 
					ProtocolFamily.InterNetworkV6 : ProtocolFamily.InterNetwork;

				if(address.AddressFamily.ToString() == ipType.ToString())
					return address;
			}

			return null;
		}

		private TcpListener port() {
			IPAddress localIP = this.getLocalIP(false);
			Console.WriteLine(localIP.ToString());

			string arg = localIP.ToString().Replace('.', ',');
			TcpListener listener = new TcpListener(IPAddress.Any, 0);
			listener.Start();
			int port = ((IPEndPoint)listener.LocalEndpoint).Port;
			Console.WriteLine("port: {0}", port);
			int p1 = port / 256;
			int p2 = port - p1 * 256;

			arg += ',' + p1.ToString();
			arg += ',' + p2.ToString();

			Console.WriteLine("sending PORT {0}", arg);

			FTPCmd("PORT", true, arg);
			return listener;
		}

		private void Read() {
			do {
				byte[] buffer = new byte[256];
				this.stream.Read(buffer, 0, buffer.Length);

				string line = Encoding.ASCII.GetString(buffer);
				Console.Write(line);
			} while (this.stream.DataAvailable);
		}

		/*
		 * Parses a command and performs some logic.
		 *
		 * return false if quitting, true otherwise */
		private bool ParseCmd(string line) {
			if(line.Length <= 0)
				return true;

			string[] args = line.Split(' ', 2);
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
						TcpListener listener = port();
						string target = args.Length == 1 ? "" : line.Split(' ', 2)[1];

						Console.WriteLine("listing...");
						FTPCmd("LIST", true, target);
						TcpClient dataConncetion = listener.AcceptTcpClient();
						NetworkStream stream = dataConncetion.GetStream();

						do {
							byte[] buffer = new byte[256];
							stream.Read(buffer, 0, buffer.Length);

							string aline = Encoding.ASCII.GetString(buffer);
							Console.Write(aline);
						} while (stream.DataAvailable);

						break;
					case "get":
						break;
					case "?":
					case "h":
					case "help":
						PrintHelp();
						return true;
					case "passive":
						break;
					case "pwd":
						FTPCmd("PWD", true);
						break;
					case "q":
					case "quit":
					case "logout":
						FTPCmd("QUIT", true);
						return false;
					case "user":
					case "login":
						FTPCmd("USER", true, args[1]);
						string password = Console.ReadLine();
						FTPCmd("PASS", true, password);
						break;
					default:
						Console.Error.WriteLine("Invalid command");
						return true;
				}
			} catch (Exception e) {
				Console.Error.WriteLine("Invalid use of {0}", cmd);
			}

			return true;
		}

		public void Run() {
			while(ParseCmd(Console.ReadLine()));

			this.running = false;
			this.connection.Close();
		}

		public static void Main(string[] args) {
			if(args.Length < 1) {
				Console.WriteLine("usage: ftp target");
				return;
			}

			string address = args[0];
			int port = args.Length > 1 ? Int32.Parse(args[1]) : 21;

			Client client = new Client(address, port);

			client.Run();
		}
	}
}
