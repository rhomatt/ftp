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

		private void ResetSendData() {
			if(this.dataLink is TcpListener)
				((TcpListener) this.dataLink).Stop();

			this.sendData = SendData.NotReady;
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

		// a valid ipv4 address + port must be given
		private void CreateActiveConnectionEndpoint(string endpoint) {
			string[] parts = endpoint.Split(',');
			int p1 = Int32.Parse(parts[4]);
			int p2 = Int32.Parse(parts[5]);

			IPAddress clientAddress = IPAddress.Parse(parts[0] + '.' + parts[1] + '.' + parts[2] + '.' + parts[3]);
			this.sendData = SendData.Active;
			this.dataLink = new IPEndPoint(clientAddress, p1*256 + p2);
			this.WriteToClient(200, " PORT command successful.");
		}

		// sets dataLink to be a TcpListener to prepare for the incoming TCP connection
		private void SendIPEndpoint() {
			IPAddress clientAddress = ((IPEndPoint) this.client.Client.RemoteEndPoint).Address;
			Console.WriteLine("Creating a listener for {0}", clientAddress.ToString());
			TcpListener dataListener = new TcpListener(clientAddress, 0);
			dataListener.Start();
			// why the TcpClient capitalizes point and the TcpListener doesn't, I will never understand...
			int port = ((IPEndPoint) dataListener.LocalEndpoint).Port;
			int p1 = port / 256;
			int p2 = port - p1 * 256;

			StringBuilder data = new StringBuilder();
			data.Append(" Entering Passive Mode (");
			data.Append(clientAddress.ToString().Replace('.', ','));
			data.AppendFormat(",{0},{1})", p1, p2);
			this.WriteToClient(227, data.ToString());

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
			this.ResetSendData();
			this.WriteToClient(226, " Directory send OK.");
		}

		private void Retrieve(string fileName) {
			if(this.sendData == SendData.NotReady) {
				this.WriteToClient(425, " Use PORT or PASV first");
				return;
			}

			// please don't download anything that won't fit into RAM
			byte[] bytes = File.ReadAllBytes(fileName);

			string message = 
				String.Format(" Opening BINARY mode data connection for {0} ({1} bytes).", fileName, bytes.Length);
			this.WriteToClient(150, message);
			TcpClient dataConnection;

			if(this.sendData == SendData.Passive)
				dataConnection = ((TcpListener) this.dataLink).AcceptTcpClient();
			else {
				dataConnection = new TcpClient();
				dataConnection.Connect((IPEndPoint) this.dataLink);
			}

			dataConnection.GetStream().Write(bytes);
			dataConnection.Close();
			this.ResetSendData();
			this.WriteToClient(226, " Transfer complete.");
		}

		private bool ParseCmd(string line) {
			if(line.Length <= 0)
				return true;

			Console.WriteLine(line);

			string[] args = line.Trim().Split(' ', 2);
			string cmd = args[0];

			try {
				switch(cmd) {
					case "TYPE":
						this.WriteToClient(502, "Sorry, only binary mode is supported");
						break;
					case "LIST":
						this.List();
						break;
					case "RETR":
						this.Retrieve(args[1]);
						break;
					case "PASV":
						this.SendIPEndpoint();
						break;
					case "PORT":
						this.CreateActiveConnectionEndpoint(args[1]);
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
			EndPoint clientAddress = this.client.Client.RemoteEndPoint;
			try {
				foreach(string line in this.welcomeMessage)
					this.WriteToClient(220, line);
				while(ParseCmd(this.fromClient.ReadLine()));
			} catch (Exception e) {
				Console.Error.WriteLine("Something bad happened while servicing the client");
				Console.Error.WriteLine(e.Message);
			} finally {
				this.client.Close();
				Console.WriteLine("Session with {0} stopped", clientAddress.ToString());
			}
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
