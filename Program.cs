using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace FtpClient {
	class Client {

		TcpClient connection;
		NetworkStream stream;
		bool isPassive = false; // false: active, true: passive
		IPAddress address;
		string prompt = "ftp> ";

		public Client(string address, int port) {
			IPHostEntry hostEntries = Dns.GetHostEntry(address);
			foreach(IPAddress curraddr in hostEntries.AddressList)
				if(curraddr.AddressFamily.ToString() == ProtocolFamily.InterNetwork.ToString())
					this.address = curraddr;

			this.connection = new TcpClient();
			this.connection.Connect(this.address, port);
			this.stream = this.connection.GetStream();

			this.Read(this.stream); // read the initial help message
		}

		private int FTPCmd(string cmd, params string[] args) {
			StringBuilder sb = new StringBuilder();

			sb.Append(cmd);
			foreach(string arg in args) {
				if(arg.Trim() == "")
					continue;
				sb.Append(' ');
				sb.Append(arg);
			}
			sb.Append("\r\n");

			this.stream.Write(Encoding.ASCII.GetBytes(sb.ToString()));
			return this.Read(this.stream);
		}

		private void list(string target) {
			TcpListener listener = null;
			TcpClient dataConnection = null;

			//TODO different logic for active and passive mode
			try {
				if(!this.isPassive)
					listener = port();
				else {
					dataConnection = new TcpClient();
					dataConnection.Connect(passive());
					Console.WriteLine("client recieved");
				}

				FTPCmd("LIST", target);
				if(!this.isPassive)
					dataConnection = listener.AcceptTcpClient();
				Console.WriteLine("client recieved");
				this.Read(dataConnection.GetStream());
				this.Read(this.stream);
			} catch (Exception) {
			} finally {
				if(listener != null)
					listener.Stop();
				if(dataConnection != null)
					dataConnection.Dispose();
			}
		}

		private void get(string target) {
			TcpListener listener = null;
			TcpClient dataConnection = null;

			//TODO different logic for active and passive mode
			try {
				listener = port();

				FTPCmd("RETR", target);
				dataConnection = listener.AcceptTcpClient();
				NetworkStream stream = dataConnection.GetStream();

				FileStream file = File.Open(target, System.IO.FileMode.Create);

				do {
					byte[] buffer = new byte[256];
					int read = stream.Read(buffer, 0, buffer.Length);

					file.Write(buffer, 0, read);
				} while (stream.DataAvailable);

				file.Flush();
				file.Dispose();
				this.Read(this.stream);
			} catch (Exception) {
				Console.Error.WriteLine("An error occured trying to execute get");
			} finally {
				if(listener != null)
					listener.Stop();
				if(dataConnection != null)
					dataConnection.Dispose();
			}
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

		// return listener if port command succeeds, null otherwise
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

			if(FTPCmd("PORT", arg) >= 400) {
				listener.Stop();
				throw new Exception("Port command failed");
			}

			return listener;
		}

		private IPEndPoint passive() {
			this.stream.Write(Encoding.ASCII.GetBytes("PASV\r\n"));
			byte[] buffer = new byte[256];
			this.stream.Read(buffer);
			string target = Encoding.ASCII.GetString(buffer);
			Console.WriteLine(target);
			Regex ipPattern = new Regex(@"\d+,\d+,\d+,\d+,\d+,\d+");
			target = ipPattern.Match(target).Value;

			string[] parts = target.Split(',');
			int p1 = Int32.Parse(parts[4]);
			int p2 = Int32.Parse(parts[5]);


			IPAddress ip = IPAddress.Parse(parts[0] + '.' + parts[1] + '.' + parts[2] + '.' + parts[3]);
			return new IPEndPoint(ip, p1*256 + p2);
		}

		// return reply code, -1 if no reply code
		private int Read(NetworkStream stream) {
			int code = -1;

			do {
				byte[] buffer = new byte[256];
				stream.Read(buffer, 0, buffer.Length);

				string line = Encoding.ASCII.GetString(buffer);
				Int32.TryParse(line.Split(' ')[0], out code);

				Console.Write(line);
			} while (stream.DataAvailable);

			Console.WriteLine("done");

			return code;
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
			string target;

			try {
				switch(cmd) {
					case "a":
					case "ascii":
						FTPCmd("TYPE", "A");
						break;
					case "b":
					case "binary":
						FTPCmd("TYPE", "I");
						break;
					case "cd":
						target = args[1];
						FTPCmd("CWD", target);
						break;
					case "cdup":
						FTPCmd("CDUP");
						break;
					case "debug":
						break;
					case "ls":
					case "dir":
						target = args.Length == 1 ? "" : line.Split(' ', 2)[1];
						list(target);
						break;
					case "get":
						target = args[1];
						get(target);
						break;
					case "?":
					case "h":
					case "help":
						PrintHelp();
						return true;
					case "passive":
						this.isPassive = !this.isPassive;
						Console.WriteLine("Passive mode is {0}", this.isPassive ? "on" : "off");
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
						return true;
				}
			} catch (Exception e) {
				Console.Error.WriteLine(e.Message);
				Console.Error.WriteLine("Invalid use of {0}", cmd);
			}

			return true;
		}

		public void Run() {
			bool running = true;
			while(running) {
				Console.Write(this.prompt);
				running = ParseCmd(Console.ReadLine());
			}

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
