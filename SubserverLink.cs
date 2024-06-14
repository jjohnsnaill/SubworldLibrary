using System;
using System.IO.Pipes;
using Terraria;

namespace SubworldLibrary
{
	internal class SubserverLink
	{
		private NamedPipeServerStream pipeOut;
		private NamedPipeServerStream pipeIn;
		private byte[] queue;

		public SubserverLink(string name, byte[] queue)
		{
			pipeOut = new NamedPipeServerStream(name + ".OUT", PipeDirection.In);
			pipeIn = new NamedPipeServerStream(name + ".IN", PipeDirection.Out);
			this.queue = queue;
		}

		public bool Connected => queue == null;

		public void Close()
		{
			pipeOut.Close();
			pipeIn.Close();
		}

		public void Send(byte[] data)
		{
			if (Connected)
			{
				pipeIn.Write(data);
			}
		}

		public void ConnectAndSend()
		{
			pipeIn.WaitForConnection();

			pipeIn.Write(queue);

			queue = null;
		}

		public void ConnectAndRead(object id)
		{
			try
			{
				ConnectAndRead();
			}
			finally
			{
				SubworldSystem.StopSubserver((int)id);
			}
		}

		private void ConnectAndRead()
		{
			pipeOut.WaitForConnection();

			// prompt clients to connect to the subserver
			for (int i = 0; i < 256; i++)
			{
				if (Netplay.Clients[i].IsConnected())
				{
					Netplay.Clients[i].Socket.AsyncSend(new byte[] { 5, 0, 3, (byte)i, 0 }, 0, 5, (state) => { });
				}
			}

			while (pipeOut.IsConnected)
			{
				byte[] packetInfo = new byte[3];
				if (pipeOut.Read(packetInfo) < 3)
				{
					break;
				}

				byte low = packetInfo[1];
				byte high = packetInfo[2];
				int length = (high << 8) | low;

				byte[] data = new byte[length];
				pipeOut.Read(data, 2, length - 2);
				data[0] = low;
				data[1] = high;

				if (packetInfo[0] == 255 && data[2] == 255)
				{
					// this packet actually came from a subserver, put it in message buffer 256 for reading on the main thread
					lock (NetMessage.buffer[256])
					{
						Buffer.BlockCopy(data, 0, NetMessage.buffer[256].readBuffer, NetMessage.buffer[256].totalData, length);
						NetMessage.buffer[256].totalData += length;
						NetMessage.buffer[256].checkBytes = true;
					}
					continue;
				}

				Netplay.Clients[packetInfo[0]].Socket.AsyncSend(data, 0, length, (state) => { });
			}
		}
	}
}