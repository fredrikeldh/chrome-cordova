using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
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
			public IDisposable disposable;

			public StreamSocket ss;

			public StreamSocketListener ssl;
			public string acceptCallbackId;
			public Queue<StreamSocket> acceptSockets;
			public int acceptQueueSize;

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

			public void ConnectionReceived(StreamSocketListener ssl,
				StreamSocketListenerConnectionReceivedEventArgs a)
			{
				if (acceptCallbackId != null)
				{
					string cid = acceptCallbackId;
					acceptCallbackId = null;
					DispatchAcceptedSocket(a.Socket, cid);
				}
				else if(acceptSockets.Count < acceptQueueSize)
				{
					acceptSockets.Enqueue(a.Socket);
				}
				// else drop the new connection.
			}

			public void DispatchAcceptedSocket(StreamSocket ss, string cid)
			{
				Socket s = new Socket(cs);
				s.disposable = s.ss = ss;
				cs.mConnections.Add(cs.mNextSocketId, s);
				var result = cs.mNextSocketId;
				cs.mNextSocketId++;
				cs.DispatchCommandResult(
					new PluginResult(PluginResult.Status.OK, result),
					cid);
			}
		};

		public async void listen(string options)
		{
			string[] o = JsonHelper.Deserialize<string[]>(options);
			int socketId = Convert.ToInt32(o[0]);
			string address = o[1];
			string port = o[2];
			int acceptQueueSize = -1;	// default: no limit to queue size.
			try
			{
				acceptQueueSize = Convert.ToInt32(o[3]);
			}
			catch (Exception)
			{
			}
			var c = mConnections[socketId];
			if (c.ssl != null)
				throw new Exception("Already listening");
			if (c.s != null)
				throw new Exception("UDP sockets cannot listen");
			c.ssl = new StreamSocketListener();
			c.acceptSockets = new Queue<StreamSocket>();
			c.acceptQueueSize = acceptQueueSize;
			c.ssl.ConnectionReceived += c.ConnectionReceived;
			if (address.Length == 0)
			{
				await c.ssl.BindServiceNameAsync(port);
			}
			else
			{
				await c.ssl.BindEndpointAsync(new HostName(address), port);
			}
			DispatchCommandResult(new PluginResult(PluginResult.Status.OK));
		}

		public void accept(string options)
		{
			string[] o = JsonHelper.Deserialize<string[]>(options);
			int socketId = Convert.ToInt32(o[0]);
			Socket s = mConnections[socketId];
			if (s.acceptSockets.Count > 0)
			{
				s.DispatchAcceptedSocket(s.acceptSockets.Dequeue(),
					this.CurrentCommandCallbackId);
			}
			else
			{
				s.acceptCallbackId = this.CurrentCommandCallbackId;
			}
		}

		int mNextSocketId = 1;
		Dictionary<int, Socket> mConnections = new Dictionary<int, Socket>();

		public void create(string options)
		{
			string[] o = JsonHelper.Deserialize<string[]>(options);
			String socketMode = o[0];
			Socket s = new Socket(this);
			if (socketMode == "tcp")
			{
			}
			else if (socketMode == "udp")
			{
				s.disposable = s.s = new DatagramSocket();
				s.recvFromPackets = new Queue<string>();

				// Must do this before bind().
				s.s.MessageReceived += s.MessageReceived;
			}
			else
			{
				throw new Exception("Invalid socketMode");
			}
			mConnections.Add(mNextSocketId, s);
			DispatchCommandResult(new PluginResult(PluginResult.Status.OK,
				mNextSocketId));
			mNextSocketId++;
			return;
		}

		public void destroy(string options)
		{
			string[] o = JsonHelper.Deserialize<string[]>(options);
			int socketId = Convert.ToInt32(o[0]);
			if(mConnections[socketId].disposable != null)
				mConnections[socketId].disposable.Dispose();
			mConnections.Remove(socketId);
		}

		public async void connect(string options)
		{
			string[] o = JsonHelper.Deserialize<string[]>(options);
			int socketId = Convert.ToInt32(o[0]);
			string address = o[1];
			string port = o[2];
			// todo: add UDP support.
			var c = mConnections[socketId];
			if (c.ss != null)
				throw new Exception("Already connected");
			if(c.s != null)
				throw new Exception("UDP not supported");
			StreamSocket s = new StreamSocket();
			c.disposable = c.ss = s;
			// await causes this function to return to the caller.
			// The rest of the function will execute asynchronously,
			// as if it were a callback.
			await s.ConnectAsync(new HostName(address), port);
			DispatchCommandResult(new PluginResult(PluginResult.Status.OK));
		}

		public void disconnect(string options)
		{
			string[] o = JsonHelper.Deserialize<string[]>(options);
			int socketId = Convert.ToInt32(o[0]);
			StreamSocket s = mConnections[socketId].ss;
			if (s != null)
			{
				s.Dispose();
			}
		}

		public async void read(string options)
		{
			string[] o = JsonHelper.Deserialize<string[]>(options);
			int socketId = Convert.ToInt32(o[0]);
			uint bufferSize = 1024;	// default not specified by reference doc.
			try {
				bufferSize = Convert.ToUInt32(o[1]);
			} catch(Exception) {
			}
			// todo: add UDP support.
			StreamSocket s = mConnections[socketId].ss;
			var readBuf = new Windows.Storage.Streams.Buffer(bufferSize);
			var res = await s.InputStream.ReadAsync(readBuf, bufferSize,
				InputStreamOptions.Partial);
			string result = Convert.ToBase64String(res.ToArray());
			DispatchCommandResult(new PluginResult(PluginResult.Status.OK,
				result));
		}

		public async void write(string options)
		{
			string[] o = JsonHelper.Deserialize<string[]>(options);
			int socketId = Convert.ToInt32(o[0]);
			var data = Convert.FromBase64String(o[1]).AsBuffer();
			// todo: add UDP support.
			StreamSocket s = mConnections[socketId].ss;
			var res = await s.OutputStream.WriteAsync(data);
			DispatchCommandResult(new PluginResult(PluginResult.Status.OK,
				res));
		}

		// This function supports only UDP.
		// For the BSD-socket TCP equivalent, see listen().
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
