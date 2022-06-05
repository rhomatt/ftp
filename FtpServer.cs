using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace FtpServer {
	class Server {
		public static TcpListener server = new TcpListener(IPAddress.Any, 2121);
		private TcpClient client;
		private StreamReader fromClient;
		private StreamWriter toClient;
		private bool passive = false;

		public Server() {
			this.client = server.AcceptTcpClient();
			this.fromClient = new StreamReader(this.client.GetStream());
			this.toClient = new StreamWriter(this.client.GetStream());
		}

		private void WriteToClient(int code, string message) {
			StringBuilder sb = new StringBuilder();
			sb.Append(code);
			sb.Append(message);
			sb.Append("\r\n");

			this.toClient.Write(sb);
			this.toClient.Flush();
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
		// returns an unstarted TcpListener to prepare for the incoming TCP connection
		private TcpListener SendIPEndpoint() {
			IPAddress clientAddress = ((IPEndPoint) this.client.Client.LocalEndPoint).Address;
			TcpListener dataListener = new TcpListener(clientAddress, 0);
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

			return dataListener;
		}

		private void SendClientData(byte[] data) {
			TcpClient dataConnection = null;
			TcpListener dataListener = null;

			try {
				if(this.passive) {
					IPAddress clientAddress = ((IPEndPoint) this.client.Client.LocalEndPoint).Address;
					dataListener = new TcpListener(clientAddress, 0);
					dataListener.Start();
					// why the TcpClient capitalizes point and the TcpListener doesn't, I will never understand...
					int port = ((IPEndPoint) dataListener.LocalEndpoint).Port;
				} else {

				}
			} catch (Exception) {
			} finally {
				if(dataConnection != null)
					dataConnection.Close();
				if(dataListener != null)
					dataListener.Stop();
			}
		}

		private bool ParseCmd(string line) {
			if(line.Length <= 0)
				return true;

			string[] args = line.Trim().Split(' ', 2);
			string cmd = args[0];
			string target;

			try {
				switch(cmd) {
					case "TYPE":
						if(args[1] == "A") {
						}
						else if(args[1] == "I") {
						}
						else {
						}
						break;
					case "CWD":
						target = args[1];
						break;
					case "CDUP":
						break;
					case "LIST":
						target = args[1];
						break;
					case "RETR":
						target = args[1];
						break;
					case "PASV":
						break;
					case "PWD":
						break;
					case "QUIT":
						return false;
					case "USER":
						break;
					case "PASS":
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

		public void Run(){
			while(ParseCmd(this.fromClient.ReadLine()));

			this.client.Close();
		}

		public static void Main(string[] args) {
			Console.WriteLine("Starting server...");
			Server.server.Start();
			Console.WriteLine("Server started");

			TcpClient client = server.AcceptTcpClient();

			while(true) {
				Server server = new Server();
				ThreadPool.QueueUserWorkItem((object _) => server.Run());
			}
		}
	}
}
