using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Windows.Networking;
using Windows.Networking.Sockets;
using WPCordovaClassLib.Cordova;
using WPCordovaClassLib.Cordova.Commands;
using WPCordovaClassLib.Cordova.JSON;
using System.IO;

// Magically adds useful methods to IBuffer, byte[] and string.
using System.Runtime.InteropServices.WindowsRuntime;


namespace WPCordovaClassLib.Cordova.Commands
{
	public class ChromeSocket : BaseCommand
	{
		class Socket
		{
			private ChromeSocket cs;
			public DatagramSocket s;
			public string recvFromCallbackId;
			public Queue<string> recvFromPackets;

			public Socket(ChromeSocket _cs)
			{
				cs = _cs;
			}

			public void MessageReceived(DatagramSocket socket,
				DatagramSocketMessageReceivedEventArgs a)
			{
				var data = a.GetDataReader().DetachBuffer().ToArray();
				//string ds = System.Text.Encoding.UTF8.GetString(data, 0, data.Length);
				string result = "{\"data\":\"" + Convert.ToBase64String(data) +
					"\", \"address\":\"" + a.RemoteAddress.RawName +
					"\", \"port\":\"" + a.RemotePort + "\"}";
				if (recvFromCallbackId != null)
				{
					string cid = recvFromCallbackId;
					recvFromCallbackId = null;
					cs.DispatchCommandResult(
						new PluginResult(PluginResult.Status.OK, result),
						cid);
				}
				else
				{
					recvFromPackets.Enqueue(result);
				}
			}
		};

		int mNextSocketId = 1;
		Dictionary<int, Socket> mConnections = new Dictionary<int, Socket>();

		public void create(string options)
		{
			string[] o = JsonHelper.Deserialize<string[]>(options);
			String socketMode = o[0];
			if(socketMode == "tcp") {
				throw new Exception("Not yet supported");
			}
			if(socketMode == "udp") {
				Socket s = new Socket(this);
				s.s = new DatagramSocket();
				s.recvFromPackets = new Queue<string>();

				// Must do this before bind().
				s.s.MessageReceived += s.MessageReceived;

				mConnections.Add(mNextSocketId, s);
				DispatchCommandResult(new PluginResult(PluginResult.Status.OK,
					mNextSocketId));
				mNextSocketId++;
				return;
			}
			throw new Exception("Invalid socketMode");
		}
		public void destroy(string options)
		{
			string[] o = JsonHelper.Deserialize<string[]>(options);
			int socketId = Convert.ToInt32(o[0]);
			mConnections[socketId].s.Dispose();
			mConnections.Remove(socketId);
		}
		public async void bind(string options)
		{
			string[] o = JsonHelper.Deserialize<string[]>(options);
			int socketId = Convert.ToInt32(o[0]);
			string address = o[1];
			string port = o[2];
			DatagramSocket s = mConnections[socketId].s;
			HostName hostname = null;
			if (address.Length != 0)
				hostname = new HostName(address);
			await s.BindEndpointAsync(hostname, port);

			DispatchCommandResult(new PluginResult(PluginResult.Status.OK));
		}
		public void recvFrom(string options)
		{
			string[] o = JsonHelper.Deserialize<string[]>(options);
			int socketId = Convert.ToInt32(o[0]);
			int bufferSize = Convert.ToInt32(o[1]);
			Socket s = mConnections[socketId];
			// If we have packets waiting, pick the oldest one of those.
			if (s.recvFromPackets.Count > 0)
			{
				DispatchCommandResult(new PluginResult(
					PluginResult.Status.OK, s.recvFromPackets.Dequeue()));
			}
			else
			{
				// Otherwise, wait for another packet.
				// How will MessageReceived know which callback to call?
				// Use CurrentCommandCallbackId.
				s.recvFromCallbackId = this.CurrentCommandCallbackId;
			}
		}
		public async void sendTo(string options)
		{
			string[] o = JsonHelper.Deserialize<string[]>(options);
			int socketId = Convert.ToInt32(o[0]);
			string address = o[1];
			string port = o[2];
			string data = o[3];
			DatagramSocket s = mConnections[socketId].s;
			//var bw = new BinaryWriter(s.OutputStream.AsStreamForWrite());
			//bw.Write(Convert.FromBase64String(data));
			var buf = Convert.FromBase64String(data).AsBuffer();
			var stream = await s.GetOutputStreamAsync(new HostName(address), port);
			await stream.WriteAsync(buf);
			DispatchCommandResult(new PluginResult(PluginResult.Status.OK, buf.Length));
		}
	}
}
