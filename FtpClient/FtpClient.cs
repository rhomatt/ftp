﻿/*
 * Author: Matthew Rho
 * Class: data comm and networks
 * Professor: Jeremy Brown
 * Desc: Minimal FTP client implementation
 */
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace FtpClient {
	class Client {
		private TcpClient connection;
		private NetworkStream stream;
		private bool isPassive = false; // false: active, true: passive
		private bool debug = false;
		private string prompt = "ftp> ";

		// if at or above this, it indicates a command failed
		private static readonly int ERROR_LEVEL = 400;
		private static readonly int FTP_PORT = 21;

		// create a client on a specified address and port
		public Client(string address, int port) {
			IPHostEntry hostEntries = Dns.GetHostEntry(address);

			// there may be multiple addresses to try
			foreach(IPAddress curraddr in hostEntries.AddressList)
				// only connect via ipv4
				if(curraddr.AddressFamily.ToString() == ProtocolFamily.InterNetwork.ToString()) {
					try {
						this.connection = new TcpClient();
						Console.WriteLine("Trying to connect to {0} on port {1}", curraddr, port);
						this.connection.Connect(curraddr, port);
						Console.WriteLine("Connected successfully");
						this.stream = this.connection.GetStream();

						this.Read(this.stream); // read the initial help message
						Console.Write("Name ({0}:{1}): ", address, Environment.UserName);
						this.Login("");
						return;
					} catch (Exception e) {
						Console.Error.WriteLine("Could not connect to {0}.", curraddr.ToString());
						Console.Error.WriteLine(e.Message);
					}
				}

			throw new Exception("Could establish a connection to " + address);
		}

		/*
		 * Perform some arbitrary FTP command
		 *
		 * return the FTP code
		 */
		private int FTPCmd(string cmd, params string[] args) {
			StringBuilder sb = new StringBuilder();

			if(this.debug)
				Console.WriteLine("Executing a {0} FTP command", cmd);

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

		/*
		 * FTP LIST command. Lists the target directory
		 * give an empty string as the target to list the current working directory
		 */
		private void List(string target) {
			TcpListener listener = null;
			TcpClient dataConnection = null;

			try {
				if(!this.isPassive)
					listener = this.Port();
				else {
					dataConnection = new TcpClient();
					dataConnection.Connect(this.Passive());
				}

				int code;
				code = this.FTPCmd("LIST", target);
				if(code >= ERROR_LEVEL)
					throw new Exception("Could not list");
				if(!this.isPassive)
					dataConnection = listener.AcceptTcpClient();
				// list the contents of the current working directory
				this.Read(dataConnection.GetStream());
				// server should notify that transfer finished
				this.Read(this.stream);
			} catch (Exception) {
				Console.Error.WriteLine("An error occured trying to execute LIST");
			} finally {
				if(listener != null)
					listener.Stop();
				if(dataConnection != null)
					dataConnection.Dispose();
			}
		}

		/*
		 * FTP RETR command. Downloads a file.
		 */
		private void Get(string target) {
			TcpListener listener = null;
			TcpClient dataConnection = null;

			try {
				if(!this.isPassive)
					listener = this.Port();
				else {
					dataConnection = new TcpClient();
					dataConnection.Connect(this.Passive());
				}

				int code = this.FTPCmd("RETR", target);
				if(code >= ERROR_LEVEL)
					throw new Exception("Could not get");

				if(!this.isPassive)
					dataConnection = listener.AcceptTcpClient();
				NetworkStream stream = dataConnection.GetStream();

				string dest = "copied";
				if(!Directory.GetCurrentDirectory().EndsWith(dest)) {
					Directory.CreateDirectory(dest);
					Directory.SetCurrentDirectory(dest);
				}
				FileStream file = File.Open(target, System.IO.FileMode.Create);
				int bytesRead = 0;

				do {
					byte[] buffer = new byte[256];
					int read = stream.Read(buffer, 0, buffer.Length);
					bytesRead = read;
					if(this.debug)
						Console.WriteLine("Read {0} bytes, {1} so far", read, bytesRead);

					file.Write(buffer, 0, read);
					// the connection is closed upon completion, so we can just check if bytes read is 0
				} while (bytesRead > 0);

				if(this.debug)
					Console.WriteLine("Read {0} total bytes", bytesRead);

				file.Flush();
				file.Dispose();
				// server should notify that transfer finished
				this.Read(this.stream);
			} catch (Exception) {
				Console.Error.WriteLine("An error occured trying to execute RETR");
			} finally {
				if(listener != null)
					listener.Stop();
				if(dataConnection != null)
					dataConnection.Dispose();
			}
		}

		/*
		 * does what it says
		 */
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

		/*
		 * returns local ipv4 address or
		 * null if nothing found (which should never happen)
		 */
		private IPAddress GetLocalIP() {
			IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
			foreach (IPAddress address in host.AddressList) {
				ProtocolFamily ipType = ProtocolFamily.InterNetwork;

				if(address.AddressFamily.ToString() == ipType.ToString())
					return address;
			}

			return null;
		}

		/*
		 * Initiates an active FTP connection
		 *
		 * return the listener that will accept the conection from the FTP server
		 */
		private TcpListener Port() {
			IPAddress localIP = this.GetLocalIP();
			if(this.debug)
				Console.WriteLine(localIP.ToString());

			string arg = localIP.ToString().Replace('.', ',');
			TcpListener listener = new TcpListener(IPAddress.Any, 0);
			listener.Start();
			int port = ((IPEndPoint)listener.LocalEndpoint).Port;
			if(this.debug)
				Console.WriteLine("port: {0}", port);
			int p1 = port / 256;
			int p2 = port - p1 * 256;

			arg += ',' + p1.ToString();
			arg += ',' + p2.ToString();

			if(this.debug)
				Console.WriteLine("sending PORT {0}", arg);

			int code = FTPCmd("PORT", arg);
			if(code >= ERROR_LEVEL) {
				listener.Stop();
				throw new Exception("Port command failed");
			}

			return listener;
		}

		/*
		 * Initiates a passive FTP connection
		 *
		 * return the IPEndPoint to connect to, given by the FTP server
		 */
		private IPEndPoint Passive() {
			this.stream.Write(Encoding.ASCII.GetBytes("PASV\r\n"));
			byte[] buffer = new byte[256];
			this.stream.Read(buffer);
			string target = Encoding.ASCII.GetString(buffer);
			if(this.debug)
				Console.WriteLine(target);
			int code = Int32.Parse(target.Split(' ')[0]);
			if(code >= ERROR_LEVEL)
				throw new Exception("An error occured when trying to send the PASV command");

			// we should get an ipv4 address plus the port in this form
			Regex ipPattern = new Regex(@"\d+,\d+,\d+,\d+,\d+,\d+");
			target = ipPattern.Match(target).Value;

			string[] parts = target.Split(',');
			int p1 = Int32.Parse(parts[4]);
			int p2 = Int32.Parse(parts[5]);


			IPAddress ip = IPAddress.Parse(parts[0] + '.' + parts[1] + '.' + parts[2] + '.' + parts[3]);
			return new IPEndPoint(ip, p1*256 + p2);
		}

		/*
		 * Reads information from a given stream and prints it
		 *
		 * return FTP code, or -1 if none found
		 */
		private int Read(NetworkStream stream) {
			int code = -1;
			int bytesRead = 0;
			bool unfinished = false;

			do {
				byte[] buffer = new byte[256];
				bytesRead = stream.Read(buffer, 0, buffer.Length);

				string line = Encoding.ASCII.GetString(buffer);
				Regex grepCode = new Regex(@"^\d+");
				string match = grepCode.Match(line).Value;
				Int32.TryParse(match, out code);

				/*
				 * It looks like anything that has a dash after the code indicates that there is more
				 * data to be sent (at least this is how the GNU FTP client seems to be implemented).
				 *
				 * Thankfully, this means I don't have to do a Thread.Sleep, which is almost what I did
				 * because the client wasn't properly reading the entire debian ftp welcome message.
				 * Also I discovered this more or less by complete accident
				 */
				Regex checkUnfinished = new Regex(@"^\d+-");
				unfinished = checkUnfinished.IsMatch(line);

				Console.Write(line);
			} while (stream.DataAvailable && bytesRead > 0 || unfinished);

			return code;
		}

		/*
		 * Parses a command and performs some relevent logic.
		 *
		 * return false if quitting, true otherwise
		 */
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
						this.FTPCmd("TYPE", "A");
						break;
					case "b":
					case "binary":
						this.FTPCmd("TYPE", "I");
						break;
					case "cd":
						target = args[1];
						this.FTPCmd("CWD", target);
						break;
					case "cdup":
						this.FTPCmd("CDUP");
						break;
					case "debug":
						this.debug = !this.debug;
						Console.WriteLine("Debug mode is {0}", this.debug ? "on" : "off");
						break;
					case "ls":
					case "dir":
						target = args.Length == 1 ? "" : line.Split(' ', 2)[1];
						this.List(target);
						break;
					case "get":
						target = args[1];
						this.Get(target);
						break;
					case "?":
					case "h":
					case "help":
						this.PrintHelp();
						return true;
					case "passive":
						this.isPassive = !this.isPassive;
						Console.WriteLine("Passive mode is {0}", this.isPassive ? "on" : "off");
						break;
					case "pwd":
						this.FTPCmd("PWD");
						break;
					case "q":
					case "quit":
					case "logout":
						this.FTPCmd("QUIT");
						return false;
					case "user":
					case "login":
						this.Login(args[1]);
						break;
					default:
						Console.Error.WriteLine("Invalid command");
						return true;
				}
			} catch (Exception e) {
				Console.Error.WriteLine("Invalid use of {0}", cmd);
				Console.Error.WriteLine(e.Message);
			}

			return true;
		}

		/*
		 * login to remote server
		 * ask for the user if the given user is a blank string
		 */
		private void Login(string user) {
			if(user == "")
				user = Console.ReadLine();
			int code = this.FTPCmd("USER", user);
			if(code >= ERROR_LEVEL)
				return;

			string password = Console.ReadLine();
			this.FTPCmd("PASS", password);
		}

		/*
		 * Main loop
		 */
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
				Console.WriteLine("usage: FTPClient.exe target [ftp_port]");
				return;
			}

			string address = args[0];
			// Look for an arg from the user. Otherwise connect on the default port, 21
			int port = args.Length > 1 ? Int32.Parse(args[1]) : FTP_PORT;

			Client client = new Client(address, port);

			client.Run();
		}
	}
}
