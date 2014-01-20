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
		int mNextSocketId = 1;
		Dictionary<int, DatagramSocket> mConnections = new Dictionary<int, DatagramSocket>();

		public void create(string options)
		{
			string[] o = JsonHelper.Deserialize<string[]>(options);
			String socketMode = o[0];
			if(socketMode == "tcp") {
				throw new Exception("Not yet supported");
			}
			if(socketMode == "udp") {
				DatagramSocket sock = new DatagramSocket();

				// Must do this before bind().
				sock.MessageReceived += MessageReceived;

				mConnections.Add(mNextSocketId, sock);
				DispatchCommandResult(new PluginResult(PluginResult.Status.OK,
					mNextSocketId));
				mNextSocketId++;
				return;
			}
			throw new Exception("Invalid socketMode");
		}
		public void destroy(string options)
		{
			int[] o = JsonHelper.Deserialize<int[]>(options);
			int socketId = o[0];
			mConnections[socketId].Dispose();
			mConnections.Remove(socketId);
		}
		public async void bind(string options)
		{
			string[] o = JsonHelper.Deserialize<string[]>(options);
			int socketId = Convert.ToInt32(o[0]);
			string address = o[1];
			string port = o[2];
			DatagramSocket s = mConnections[socketId];
			HostName hostname = null;
			if (address.Length != 0)
				hostname = new HostName(address);
			await s.BindEndpointAsync(hostname, port);

			DispatchCommandResult(new PluginResult(PluginResult.Status.OK));
		}
		public void recvFrom(string options)
		{
			int[] o = JsonHelper.Deserialize<int[]>(options);
			int socketId = o[0];
			int bufferSize = o[1];
			DatagramSocket s = mConnections[socketId];
			// todo: how will MessageReceived know which callback to call?
			// use CurrentCommandCallbackId.
		}
		public async void sendTo(string options)
		{
			string[] o = JsonHelper.Deserialize<string[]>(options);
			int socketId = Convert.ToInt32(o[0]);
			string address = o[1];
			string port = o[2];
			string data = o[3];
			DatagramSocket s = mConnections[socketId];
			//var bw = new BinaryWriter(s.OutputStream.AsStreamForWrite());
			//bw.Write(Convert.FromBase64String(data));
			var buf = Convert.FromBase64String(data).AsBuffer();
			var stream = await s.GetOutputStreamAsync(new HostName(address), port);
			await stream.WriteAsync(buf);
			DispatchCommandResult(new PluginResult(PluginResult.Status.OK, buf.Length));
		}

		async void MessageReceived(DatagramSocket socket,
			DatagramSocketMessageReceivedEventArgs a)
		{
			var data = a.GetDataReader().DetachBuffer().ToArray();
			string ds = System.Text.Encoding.UTF8.GetString(data, 0, data.Length);
			string result = "{data:\"" + Convert.ToBase64String(data) +
				"\", address:\"" + a.RemoteAddress.RawName +
				"\",port:\"" + a.RemotePort + "\"}";
			//DispatchCommandResult(new PluginResult(PluginResult.Status.OK, result));
		}
	}
}
