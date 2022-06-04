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

		public static void PerClientServer(TcpClient client){
			bool fromDone = false;

			Task FromClient = Task.Run(async () => {
				NetworkStream stream = client.GetStream();

				while(true) {
					// do nothing until we read a line in
					byte[] buffer = new byte[256];
					int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
				}
			});

			Task ToClient = Task.Run(async () => {
				StreamWriter stream = new StreamWriter(client.GetStream());
				while(!fromDone) {
					await stream.WriteLineAsync("temp");
				}
				stream.Close();
			});

			FromClient.Wait();
			ToClient.Wait();
		}

		public static void Main(string[] args) {
			Console.WriteLine("Starting server...");
			Server.server.Start();
			Console.WriteLine("Server started");

			while(true) {
				TcpClient client = server.AcceptTcpClient();
				ThreadPool.QueueUserWorkItem((object _) => Server.PerClientServer(client));
			}
		}
	}
}
