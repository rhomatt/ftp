using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace FtpServer {
	enum SendData {
		NotReady, Active, Passive
	}

	class Server {
		private static TcpListener server = new TcpListener(IPAddress.Any, 2121);
		private TcpClient client;
		private StreamReader fromClient;
		private StreamWriter toClient;
		private static string anonymousUser = "ftp";
		private string user;
		private bool authenticated = false;
		private string cwd = Directory.GetCurrentDirectory();

		private string[] welcomeMessage = {
			" Welcome to Matthew's FTP server",
		};

		// This will get marked when a PORT or PASV command is sent from the client
		// 1 indicates 
		private SendData sendData = SendData.NotReady;
		// Either a IPEndPoint, or TcpListener, depending on if we are expecting active or passive mode
		private Object dataLink;

		public Server() {
			this.client = server.AcceptTcpClient();
			this.fromClient = new StreamReader(this.client.GetStream());
			this.toClient = new StreamWriter(this.client.GetStream());
		}

		private void WriteToClient(int code, string message) {
			StringBuilder sb = new StringBuilder();
			sb.Append(code);
			sb.Append(message);
			sb.Append(" \r\n");

			this.toClient.Write(sb);
			this.toClient.Flush();
		}

		private bool Authenticate(string password) {
			if(this.user == Server.anonymousUser)
				this.authenticated = true;

			//TODO maybe put logic here to support users
			return this.authenticated;
		}

		// returns local ipv4 address
		private IPAddress GetLocalIP() {
			IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
			foreach (IPAddress address in host.AddressList) {
				ProtocolFamily ipType = ProtocolFamily.InterNetwork;

				if(address.AddressFamily.ToString() == ipType.ToString())
					return address;
			}

			return null;
		}

		// only to be called in passive mode
		// sets dataLink to be a TcpListener to prepare for the incoming TCP connection
		private void SendIPEndpoint() {
			IPAddress clientAddress = ((IPEndPoint) this.client.Client.LocalEndPoint).Address;
			Console.WriteLine("Creating a listener for {0}", clientAddress.ToString());
			TcpListener dataListener = new TcpListener(clientAddress, 0);
			dataListener.Start();
			// why the TcpClient capitalizes point and the TcpListener doesn't, I will never understand...
			int port = ((IPEndPoint) dataListener.LocalEndpoint).Port;
			int p1 = port / 256;
			int p2 = port - p1 * 256;

			int code = 227;
			StringBuilder data = new StringBuilder();
			data.Append(" Entering Passive Mode (");
			data.Append(this.GetLocalIP().ToString().Replace('.', ','));
			data.AppendFormat(",{0},{1})", p1, p2);
			this.WriteToClient(code, data.ToString());

			this.sendData = SendData.Passive;
			this.dataLink = dataListener;
		}

		private void SendClientData(TcpClient dataConnection, byte[] data) {
			try {
				dataConnection.GetStream().Write(data);
			} catch (Exception) {
				Console.Error.WriteLine("There was a problem trying to send data to the client");
			}
		}

		private void List() {
			if(this.sendData == SendData.NotReady) {
				this.WriteToClient(425, " Use PORT or PASV first");
				return;
			}
			this.WriteToClient(150, " Here comes the directory listing.");
			TcpClient dataConnection;

			if(this.sendData == SendData.Passive)
				dataConnection = ((TcpListener) this.dataLink).AcceptTcpClient();
			else {
				dataConnection = new TcpClient();
				dataConnection.Connect((IPEndPoint) this.dataLink);
			}

			string[] files = Directory.GetFiles(this.cwd);
			foreach(string line in files) {
				string file = line + "\r\n";
				SendClientData(dataConnection, Encoding.ASCII.GetBytes(file));
			}
			dataConnection.Close();
			this.WriteToClient(226, " Directory send OK.");
		}

		private bool ParseCmd(string line) {
			if(line.Length <= 0)
				return true;

			Console.WriteLine(line);

			string[] args = line.Trim().Split(' ', 2);
			string cmd = args[0];
			string target;

			try {
				switch(cmd) {
					case "TYPE":
						this.WriteToClient(502, "Sorry, only binary mode is supported");
						break;
					case "LIST":
						this.List();
						this.sendData = SendData.NotReady;
						break;
					case "RETR":
						target = args[1];
						break;
					case "PASV":
						this.SendIPEndpoint();
						break;
					case "PWD":
						string cwd = String.Format(" {0}", this.cwd);
						this.WriteToClient(200, cwd);
						break;
					case "QUIT":
						WriteToClient(221, " Goodbye.");
						return false;
					case "USER":
						this.user = args[1];
						if(this.user == Server.anonymousUser)
							this.WriteToClient(331, " Please specify the password");
						else
							this.WriteToClient(530, " This FTP server is anonymous only.");
						break;
					case "PASS":
						this.WriteToClient(230, " Login successful");
						break;
					default:
						WriteToClient(200, " Unsupported command");
						return true;
				}
			} catch (Exception e) {
				Console.Error.WriteLine(e.Message);
				Console.Error.WriteLine("Invalid use of {0}", cmd);
			}

			return true;
		} 

		public void Run(){
			foreach(string line in this.welcomeMessage)
				this.WriteToClient(220, line);
			while(ParseCmd(this.fromClient.ReadLine()));

			this.client.Close();
		}

		public static void Main(string[] args) {
			Console.WriteLine("Starting server...");
			Server.server.Start();
			Console.WriteLine("Server started");

			while(true) {
				Server server = new Server();
				ThreadPool.QueueUserWorkItem((object _) => server.Run());
			}
		}
	}
}
