using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Terraria;
using Terraria.Audio;
using Terraria.Chat;
using Terraria.GameContent;
using Terraria.GameContent.Bestiary;
using Terraria.GameContent.Creative;
using Terraria.GameContent.Events;
using Terraria.Graphics.Capture;
using Terraria.IO;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.Net;
using Terraria.Net.Sockets;
using Terraria.Social;
using Terraria.Utilities;
using Terraria.WorldBuilding;

namespace SubworldLibrary
{
	internal class SubserverSocket : ISocket
	{
		private int id;

		internal static RemoteAddress address;

		public SubserverSocket(int id)
		{
			this.id = id;
		}

		void ISocket.AsyncReceive(byte[] data, int offset, int size, SocketReceiveCallback callback, object state) { }

		void ISocket.AsyncSend(byte[] data, int offset, int size, SocketSendCallback callback, object state)
		{
			byte[] packet = new byte[size + 1];
			packet[0] = (byte)id;
			Buffer.BlockCopy(data, offset, packet, 1, size);
			SubworldSystem.pipeOut.Write(packet);
		}

		void ISocket.Close() { }

		void ISocket.Connect(RemoteAddress address) { }

		RemoteAddress ISocket.GetRemoteAddress() => address;

		bool ISocket.IsConnected() => Netplay.Clients[id].IsActive;

		bool ISocket.IsDataAvailable() => false;

		void ISocket.SendQueuedPackets() { }

		bool ISocket.StartListening(SocketConnectionAccepted callback) => false;

		void ISocket.StopListening() { }
	}

	public class SubworldSystem : ModSystem
	{
		internal static List<Subworld> subworlds;

		internal static Subworld current;
		internal static Subworld cache;
		private static WorldFileData main;
		private static int suppressAutoShutdown;

		internal static TagCompound copiedData;
		internal static int[] playerLocations;
		internal static int[] pendingMoves;
		internal static HashSet<ISocket> deniedSockets;

		internal static NamedPipeClientStream pipeIn;
		internal static NamedPipeClientStream pipeOut;

		public override void OnModLoad()
		{
			subworlds = new List<Subworld>();

			playerLocations = new int[256];
			Array.Fill(playerLocations, -1);

			pendingMoves = new int[256];
			Array.Fill(pendingMoves, -1);

			deniedSockets = new HashSet<ISocket>();

			Player.Hooks.OnEnterWorld += OnEnterWorld;
			Netplay.OnDisconnect += OnDisconnect;

			suppressAutoShutdown = -1;
		}

		public override void Unload()
		{
			Player.Hooks.OnEnterWorld -= OnEnterWorld;
			Netplay.OnDisconnect -= OnDisconnect;
		}

		/// <summary>
		/// Hides the Return button.
		/// <br/>Its value is reset before <see cref="Subworld.OnEnter"/> is called, and after <see cref="Subworld.OnExit"/> is called.
		/// </summary>
		public static bool noReturn;
		/// <summary>
		/// Hides the Underworld background.
		/// <br/>Its value is reset before <see cref="Subworld.OnEnter"/> is called, and after <see cref="Subworld.OnExit"/> is called.
		/// </summary>
		public static bool hideUnderworld;

		/// <summary>
		/// The current subworld.
		/// </summary>
		public static Subworld Current => current;
		/// <summary>
		/// Returns true if the current subworld's ID matches the specified ID.
		/// <code>SubworldSystem.IsActive("MyMod/MySubworld")</code>
		/// </summary>
		public static bool IsActive(string id) => current?.FullName == id;
		/// <summary>
		/// Returns true if the specified subworld is active.
		/// </summary>
		public static bool IsActive<T>() where T : Subworld => current?.GetType() == typeof(T);
		/// <summary>
		/// Returns true if not in the main world.
		/// </summary>
		public static bool AnyActive() => current != null;
		/// <summary>
		/// Returns true if the current subworld is from the specified mod.
		/// </summary>
		public static bool AnyActive(Mod mod) => current?.Mod == mod;
		/// <summary>
		/// Returns true if the current subworld is from the specified mod.
		/// </summary>
		public static bool AnyActive<T>() where T : Mod => current?.Mod == ModContent.GetInstance<T>();
		/// <summary>
		/// The current subworld's file path.
		/// </summary>
		public static string CurrentPath => Path.Combine(main.IsCloudSave ? Main.CloudWorldPath : Main.WorldPath, main.UniqueId.ToString(), current.FileName + ".wld");

		/// <summary>
		/// Tries to enter the subworld with the specified ID.
		/// <code>SubworldSystem.Enter("MyMod/MySubworld")</code>
		/// </summary>
		public static bool Enter(string id)
		{
			if (current != cache)
			{
				return false;
			}

			for (int i = 0; i < subworlds.Count; i++)
			{
				if (subworlds[i].FullName == id)
				{
					BeginEntering(i);
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Enters the specified subworld.
		/// </summary>
		public static bool Enter<T>() where T : Subworld
		{
			if (current != cache)
			{
				return false;
			}

			for (int i = 0; i < subworlds.Count; i++)
			{
				if (subworlds[i].GetType() == typeof(T))
				{
					BeginEntering(i);
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Exits the current subworld.
		/// </summary>
		public static void Exit()
		{
			if (current != null && current == cache)
			{
				BeginEntering(current.ReturnDestination);
			}
		}

		private static void BeginEntering(int index)
		{
			if (Main.netMode == 2)
			{
				return;
			}

			if (index == int.MinValue)
			{
				current = null;
				Main.gameMenu = true;

				Task.Factory.StartNew(ExitWorldCallBack, null);
				return;
			}

			if (Main.netMode == 0)
			{
				if (current == null && index >= 0)
				{
					main = Main.ActiveWorldFileData;
				}

				current = index < 0 ? null : subworlds[index];
				Main.gameMenu = true;

				Task.Factory.StartNew(ExitWorldCallBack, index);
				return;
			}

			ModPacket packet = ModContent.GetInstance<SubworldLibrary>().GetPacket();
			packet.Write(index < 0 ? ushort.MaxValue : (ushort)index);
			packet.Send();
		}

		/// <summary>
		/// Tries to send the specified player to the subworld with the specified ID.
		/// </summary>
		public static void MovePlayerToSubworld(string id, int player)
		{
			if (Main.netMode == 1 || (Main.netMode == 2 && current != null))
			{
				return;
			}

			for (int i = 0; i < subworlds.Count; i++)
			{
				if (subworlds[i].FullName == id)
				{
					if (Main.netMode == 0)
					{
						BeginEntering(i);
						return;
					}

					MovePlayerToSubserver(player, (ushort)i);
					return;
				}
			}
		}

		/// <summary>
		/// Sends the specified player to the specified subworld.
		/// </summary>
		public static void MovePlayerToSubworld<T>(int player) where T : Subworld
		{
			if (Main.netMode == 1 || (Main.netMode == 2 && current != null))
			{
				return;
			}

			for (int i = 0; i < subworlds.Count; i++)
			{
				if (subworlds[i].GetType() == typeof(T))
				{
					if (Main.netMode == 0)
					{
						BeginEntering(i);
						return;
					}

					MovePlayerToSubserver(player, (ushort)i);
					return;
				}
			}
		}

		/// <summary>
		/// Sends the specified player to the main world.
		/// </summary>
		public static void MovePlayerToMainWorld(int player)
		{
			if (Main.netMode == 1 || (Main.netMode == 2 && current != null))
			{
				return;
			}

			if (Main.netMode == 0)
			{
				BeginEntering(-1);
				return;
			}

			MovePlayerToSubserver(player, ushort.MaxValue);
		}

		internal static void MovePlayerToSubserver(int player, ushort id)
		{
			if (pendingMoves[player] >= 0)
			{
				return;
			}

			pendingMoves[player] = id;

			ModPacket packet = ModContent.GetInstance<SubworldLibrary>().GetPacket();
			packet.Write(id);
			packet.Send(player);

			if (playerLocations[player] >= 0)
			{
				subworlds[playerLocations[player]].link?.Send(GetDisconnectPacket(player, ModContent.GetInstance<SubworldLibrary>().NetID));
			}

			if (id != ushort.MaxValue)
			{
				// this respects the vanilla call order

				Main.player[player].active = false;
				NetMessage.SendData(14, -1, player, null, player, 0);
				ChatHelper.BroadcastChatMessage(NetworkText.FromKey("Mods.SubworldLibrary.Move", Netplay.Clients[player].Name, subworlds[id].DisplayName), new Color(255, 240, 20), player);
				Player.Hooks.PlayerDisconnect(player);
			}

			// stop sending packets to the client while they're moving
			NetMessage.buffer[player].broadcast = false;

			if (id < ushort.MaxValue)
			{
				StartSubserver(id);
			}
		}

		internal static void FinishMove(int player)
		{
			// send packets to the client again
			NetMessage.buffer[player].broadcast = true;

			RemoteClient client = Netplay.Clients[player];

			int id = pendingMoves[player];
			if (id == ushort.MaxValue)
			{
				if (Main.autoShutdown && client.Socket.GetRemoteAddress().IsLocalHost())
				{
					// this is reverted in the CheckBytes injection
					Main.autoShutdown = false;
					suppressAutoShutdown = player;
				}

				playerLocations[player] = -1;
				deniedSockets.Remove(client.Socket);

				client.State = 1;
				client.ResetSections();

				// prompt the client to reconnect
				client.Socket.AsyncSend(new byte[] { 5, 0, 3, (byte)player, 0 }, 0, 5, (state) => { });

				pendingMoves[player] = -1;
				return;
			}

			// prompt the client to reconnect, done before setting their location so the packet can go through
			SubserverLink link = subworlds[id].link;
			if (link != null && link.Connected)
			{
				client.Socket.AsyncSend(new byte[] { 5, 0, 3, (byte)player, 0 }, 0, 5, (state) => { });
			}

			// set the client's location. DenyRead and DenySend are now in effect
			playerLocations[player] = id;
			deniedSockets.Add(client.Socket);

			pendingMoves[player] = -1;
		}

		private static void SyncDisconnect(int player)
		{
			if (playerLocations[player] >= 0)
			{
				subworlds[playerLocations[player]].link?.Send(GetDisconnectPacket(player, ModContent.GetInstance<SubworldLibrary>().NetID));

				playerLocations[player] = -1;
			}

			deniedSockets.Remove(Netplay.Clients[player].Socket);

			if (player == suppressAutoShutdown)
			{
				suppressAutoShutdown = -1;
				Main.autoShutdown = true;
			}
		}

		private static byte[] GetDisconnectPacket(int player, int id)
		{
			// client, (ushort) size, packet id, (byte/ushort) sublib net id twice (read a second time by sublib to sync a leaving client)
			if (ModNet.NetModCount < 256)
			{
				return new byte[] { (byte)player, 5, 0, 250, (byte)id, (byte)id };
			}
			else
			{
				return new byte[] { (byte)player, 7, 0, 250, (byte)id, (byte)(id >> 8), (byte)id, (byte)(id >> 8) };
			}
		}

		private static void AllowAutoShutdown(int i)
		{
			if (i == suppressAutoShutdown && Netplay.Clients[i].State == 10)
			{
				suppressAutoShutdown = -1;
				Main.autoShutdown = true;
			}
		}

		/// <summary>
		/// Starts a subserver for the subworld with the specified ID, if one is not running already.
		/// </summary>
		public static void StartSubserver(int id)
		{
			Subworld subworld = subworlds[id];
			if (subworld.link != null)
			{
				return;
			}

			string name = subworld.FileName;

			string args = "tModLoader.dll -server -showserverconsole ";

			args += Main.ActiveWorldFileData.IsCloudSave ? "-cloudworld \"" : "-world \"";

			args += Main.worldPathName + "\" -subworld \"" + name + "\"";

			if (Program.LaunchParameters.TryGetValue("-modpath", out string modpath))
			{
				args += " -modpath \"" + modpath + "\"";
			}
			if (Program.LaunchParameters.TryGetValue("-modpack", out string modpack))
			{
				args += " -modpack \"" + modpack + "\"";
			}
			if (Program.LaunchParameters.TryGetValue("-steamworkshopfolder", out string steamworkshopfolder))
			{
				args += " -steamworkshopfolder \"" + steamworkshopfolder + "\"";
			}
			if (Program.LaunchParameters.TryGetValue("-savedirectory", out string savedirectory))
			{
				args += " -savedirectory \"" + savedirectory + "\"";
			}
			if (Program.LaunchParameters.TryGetValue("-savedirectory", out string tmlsavedirectory))
			{
				args += " -tmlsavedirectory \"" + tmlsavedirectory + "\"";
			}
			if (Program.LaunchParameters.TryGetValue("-config", out string config))
			{
				args += " -config \"" + config + "\"";
			}
			if (Program.LaunchParameters.TryGetValue("-forcepriority", out string forcepriority))
			{
				args += " -forcepriority " + forcepriority;
			}
			if (Netplay.SpamCheck)
			{
				args += " -secure";
			}

			Process p = new Process();
			p.StartInfo.FileName = Process.GetCurrentProcess().MainModule!.FileName;
			p.StartInfo.Arguments = args;
			p.StartInfo.UseShellExecute = true;
			p.Start();

			copiedData = new TagCompound();
			CopyMainWorldData();

			using (MemoryStream stream = new MemoryStream())
			{
				TagIO.ToStream(copiedData, stream);
				subworld.link = new SubserverLink(name, stream.ToArray());
			}

			copiedData = null;

			new Thread(subworld.link.ConnectAndRead)
			{
				Name = "Subserver Packets",
				IsBackground = true
			}.Start(id);

			Task.Run(subworld.link.ConnectAndSend);
		}

		/// <summary>
		/// Stops a subserver for the subworld with the specified ID, if one is running.
		/// </summary>
		public static void StopSubserver(int id)
		{
			Subworld subworld = subworlds[id];
			if (subworld.link == null)
			{
				return;
			}

			subworld.link.Close();
			subworld.link = null;

			for (int i = 0; i < 256; i++)
			{
				if (playerLocations[i] == id)
				{
					playerLocations[i] = -1;
					deniedSockets.Remove(Netplay.Clients[i].Socket);

					pendingMoves[i] = ushort.MaxValue;

					ModPacket packet = ModContent.GetInstance<SubworldLibrary>().GetPacket();
					packet.Write(ushort.MaxValue);
					packet.Send(i);

					NetMessage.buffer[i].broadcast = false;
				}
			}
		}

		/// <summary>
		/// Tries to get the index of the subworld with the specified ID.
		/// <br/> Typically used for <see cref="Subworld.ReturnDestination"/>.
		/// <br/> Returns <see cref="int.MinValue"/> if the subworld couldn't be found.
		/// <code>public override int ReturnDestination => SubworldSystem.GetIndex("MyMod/MySubworld");</code>
		/// </summary>
		public static int GetIndex(string id)
		{
			for (int i = 0; i < subworlds.Count; i++)
			{
				if (subworlds[i].FullName == id)
				{
					return i;
				}
			}
			return int.MinValue;
		}

		/// <summary>
		/// Gets the index of the specified subworld.
		/// <br/> Typically used for <see cref="Subworld.ReturnDestination"/>.
		/// </summary>
		public static int GetIndex<T>() where T : Subworld
		{
			for (int i = 0; i < subworlds.Count; i++)
			{
				if (subworlds[i].GetType() == typeof(T))
				{
					return i;
				}
			}
			return int.MinValue;
		}

		private static byte[] GetPacketHeader(int size, int mod)
		{
			byte[] packet = new byte[size];

			packet[0] = 255; // invalid client under normal circumstances, message 255 from client 255 is treated as a packet from the other server

			packet[1] = (byte)(size - 1);
			packet[2] = (byte)((size - 1) >> 8);

			packet[3] = 255;

			packet[4] = (byte)mod;
			if (ModNet.NetModCount >= 256)
			{
				packet[5] = (byte)(mod >> 8);
			}

			return packet;
		}

		/// <summary>
		/// Sends a packet from the specified mod directly to a subserver.
		/// <br/> Use <see cref="GetIndex"/> to get the subserver's ID.
		/// </summary>
		public static void SendToSubserver(int subserver, Mod mod, byte[] data)
		{
			int header = ModNet.NetModCount < 256 ? 5 : 6;
			byte[] packet = GetPacketHeader(data.Length + header, mod.NetID);
			Buffer.BlockCopy(data, 0, packet, header, data.Length);
			subworlds[subserver].link?.Send(packet);
		}

		/// <summary>
		/// Sends a packet from the specified mod directly to all subservers.
		/// </summary>
		public static void SendToAllSubservers(Mod mod, byte[] data)
		{
			int header = ModNet.NetModCount < 256 ? 5 : 6;
			byte[] packet = GetPacketHeader(data.Length + header, mod.NetID);
			Buffer.BlockCopy(data, 0, packet, header, data.Length);

			for (int i = 0; i < subworlds.Count; i++)
			{
				subworlds[i].link?.Send(packet);
			}
		}

		/// <summary>
		/// Sends a packet from the specified mod directly to all subservers added by that mod.
		/// </summary>
		public static void SendToAllSubserversFromMod(Mod mod, byte[] data)
		{
			int header = ModNet.NetModCount < 256 ? 5 : 6;
			byte[] packet = GetPacketHeader(data.Length + header, mod.NetID);
			Buffer.BlockCopy(data, 0, packet, header, data.Length);

			for (int i = 0; i < subworlds.Count; i++)
			{
				Subworld subworld = subworlds[i];
				if (subworld.Mod == mod)
				{
					subworld.link?.Send(packet);
				}
			}
		}

		/// <summary>
		/// Sends a packet from the specified mod directly to the main server.
		/// </summary>
		public static void SendToMainServer(Mod mod, byte[] data)
		{
			int header = ModNet.NetModCount < 256 ? 5 : 6;
			byte[] packet = GetPacketHeader(data.Length + header, mod.NetID);
			Buffer.BlockCopy(data, 0, packet, header, data.Length);
			pipeOut.Write(packet);
		}

		/// <summary>
		/// Can only be called in <see cref="Subworld.CopyMainWorldData"/> or <see cref="Subworld.OnExit"/>!
		/// <br/>Stores data to be transferred between worlds under the specified key, if that key is not already in use.
		/// <br/>Naming the key after the variable pointing to the data is highly recommended to avoid redundant copying. This can be done automatically with nameof().
		/// <br/>Keys starting with '!' cannot be changed once copied, and don't need to be copied back to the main world.
		/// <code>SubworldSystem.CopyWorldData(nameof(DownedSystem.downedBoss), DownedSystem.downedBoss);</code>
		/// </summary>
		public static void CopyWorldData(string key, object data)
		{
			if (data != null && (key[0] != '!' || !copiedData.ContainsKey(key)))
			{
				copiedData[key] = data;
			}
		}

		/// <summary>
		/// Can only be called in <see cref="Subworld.ReadCopiedMainWorldData"/> or <see cref="Subworld.ReadCopiedSubworldData"/>!
		/// <br/>Reads data copied from another world stored under the specified key.
		/// <br/>Keys starting with '!' don't need to be copied back to the main world. (SubworldSystem.Current == null)
		/// <code>DownedSystem.downedBoss = SubworldSystem.ReadCopiedWorldData&lt;bool&gt;(nameof(DownedSystem.downedBoss));</code>
		/// </summary>
		public static T ReadCopiedWorldData<T>(string key) => copiedData.Get<T>(key);

		private static bool ChangeAudio()
		{
			if (current != null)
			{
				return current.ChangeAudio();
			}
			if (cache != null)
			{
				return cache.ChangeAudio();
			}
			return false;
		}

		private static bool ManualAudioUpdates()
		{
			if (current != null)
			{
				return current.ManualAudioUpdates;
			}
			if (cache != null)
			{
				return cache.ManualAudioUpdates;
			}
			return false;
		}

		private static void CopyMainWorldData()
		{
			copiedData["!mainId"] = (Main.netMode != 2 || current != null) ? main.UniqueId.ToByteArray() : Main.ActiveWorldFileData.UniqueId.ToByteArray();
			copiedData["!seed"] = Main.ActiveWorldFileData.SeedText;
			copiedData["!gameMode"] = Main.ActiveWorldFileData.GameMode;
			copiedData["!hardMode"] = Main.hardMode;

			// it's called reflection because the code is ugly like you
			using (MemoryStream stream = new MemoryStream())
			{
				using BinaryWriter writer = new BinaryWriter(stream);

				FieldInfo field = typeof(NPCKillsTracker).GetField("_killCountsByNpcId", BindingFlags.NonPublic | BindingFlags.Instance);
				Dictionary<string, int> kills = (Dictionary<string, int>)field.GetValue(Main.BestiaryTracker.Kills);

				writer.Write(kills.Count);
				foreach (KeyValuePair<string, int> item in kills)
				{
					writer.Write(item.Key);
					writer.Write(item.Value);
				}

				field = typeof(NPCWasNearPlayerTracker).GetField("_wasNearPlayer", BindingFlags.NonPublic | BindingFlags.Instance);
				HashSet<string> sights = (HashSet<string>)field.GetValue(Main.BestiaryTracker.Sights);

				writer.Write(sights.Count);
				foreach (string item in sights)
				{
					writer.Write(item);
				}

				field = typeof(NPCWasChatWithTracker).GetField("_chattedWithPlayer", BindingFlags.NonPublic | BindingFlags.Instance);
				HashSet<string> chats = (HashSet<string>)field.GetValue(Main.BestiaryTracker.Chats);

				writer.Write(chats.Count);
				foreach (string item in chats)
				{
					writer.Write(item);
				}

				copiedData["bestiary"] = stream.GetBuffer();
			}
			using (MemoryStream stream = new MemoryStream())
			{
				using BinaryWriter writer = new BinaryWriter(stream);

				FieldInfo field = typeof(CreativePowerManager).GetField("_powersById", BindingFlags.NonPublic | BindingFlags.Instance);
				foreach (KeyValuePair<ushort, ICreativePower> item in (Dictionary<ushort, ICreativePower>)field.GetValue(CreativePowerManager.Instance))
				{
					if (item.Value is IPersistentPerWorldContent power)
					{
						writer.Write((ushort)(item.Key + 1));
						power.Save(writer);
					}
				}
				writer.Write((ushort)0);

				copiedData["powers"] = stream.GetBuffer();
			}

			copiedData[nameof(Main.drunkWorld)] = Main.drunkWorld;
			copiedData[nameof(Main.getGoodWorld)] = Main.getGoodWorld;
			copiedData[nameof(Main.tenthAnniversaryWorld)] = Main.tenthAnniversaryWorld;
			copiedData[nameof(Main.dontStarveWorld)] = Main.dontStarveWorld;
			copiedData[nameof(Main.notTheBeesWorld)] = Main.notTheBeesWorld;
			copiedData[nameof(Main.remixWorld)] = Main.remixWorld;
			copiedData[nameof(Main.noTrapsWorld)] = Main.noTrapsWorld;
			copiedData[nameof(Main.zenithWorld)] = Main.zenithWorld;

			CopyDowned();

			foreach (ICopyWorldData data in ModContent.GetContent<ICopyWorldData>())
			{
				data.CopyMainWorldData();
			}
		}

		private static void ReadCopiedMainWorldData()
		{
			if (current != null)
			{
				main.UniqueId = new Guid(copiedData.Get<byte[]>("!mainId"));
				Main.ActiveWorldFileData.SetSeed(copiedData.Get<string>("!seed"));
				Main.GameMode = copiedData.Get<int>("!gameMode");
				Main.hardMode = copiedData.Get<bool>("!hardMode");
			}

			// i'm sorry that was mean
			using (MemoryStream stream = new MemoryStream(copiedData.Get<byte[]>("bestiary")))
			{
				using BinaryReader reader = new BinaryReader(stream);

				FieldInfo field = typeof(NPCKillsTracker).GetField("_killCountsByNpcId", BindingFlags.NonPublic | BindingFlags.Instance);
				Dictionary<string, int> kills = (Dictionary<string, int>)field.GetValue(Main.BestiaryTracker.Kills);

				int count = reader.ReadInt32();
				for (int i = 0; i < count; i++)
				{
					kills[reader.ReadString()] = reader.ReadInt32();
				}

				field = typeof(NPCWasNearPlayerTracker).GetField("_wasNearPlayer", BindingFlags.NonPublic | BindingFlags.Instance);
				HashSet<string> sights = (HashSet<string>)field.GetValue(Main.BestiaryTracker.Sights);

				count = reader.ReadInt32();
				for (int i = 0; i < count; i++)
				{
					sights.Add(reader.ReadString());
				}

				field = typeof(NPCWasChatWithTracker).GetField("_chattedWithPlayer", BindingFlags.NonPublic | BindingFlags.Instance);
				HashSet<string> chats = (HashSet<string>)field.GetValue(Main.BestiaryTracker.Chats);

				count = reader.ReadInt32();
				for (int i = 0; i < count; i++)
				{
					chats.Add(reader.ReadString());
				}
			}
			using (MemoryStream stream = new MemoryStream(copiedData.Get<byte[]>("powers")))
			{
				using BinaryReader reader = new BinaryReader(stream);

				FieldInfo field = typeof(CreativePowerManager).GetField("_powersById", BindingFlags.NonPublic | BindingFlags.Instance);
				Dictionary<ushort, ICreativePower> powers = (Dictionary<ushort, ICreativePower>)field.GetValue(CreativePowerManager.Instance);

				ushort id;
				while ((id = reader.ReadUInt16()) > 0)
				{
					((IPersistentPerWorldContent)powers[(ushort)(id - 1)]).Load(reader, 0);
				}
			}

			Main.drunkWorld = copiedData.Get<bool>(nameof(Main.drunkWorld));
			Main.getGoodWorld = copiedData.Get<bool>(nameof(Main.getGoodWorld));
			Main.tenthAnniversaryWorld = copiedData.Get<bool>(nameof(Main.tenthAnniversaryWorld));
			Main.dontStarveWorld = copiedData.Get<bool>(nameof(Main.dontStarveWorld));
			Main.notTheBeesWorld = copiedData.Get<bool>(nameof(Main.notTheBeesWorld));
			Main.remixWorld = copiedData.Get<bool>(nameof(Main.remixWorld));
			Main.noTrapsWorld = copiedData.Get<bool>(nameof(Main.noTrapsWorld));
			Main.zenithWorld = copiedData.Get<bool>(nameof(Main.zenithWorld));

			ReadCopiedDowned();

			foreach (ICopyWorldData data in ModContent.GetContent<ICopyWorldData>())
			{
				data.ReadCopiedMainWorldData();
			}
		}

		private static void CopyDowned()
		{
			copiedData[nameof(NPC.downedSlimeKing)] = NPC.downedSlimeKing;

			copiedData[nameof(NPC.downedBoss1)] = NPC.downedBoss1;
			copiedData[nameof(NPC.downedBoss2)] = NPC.downedBoss2;
			copiedData[nameof(NPC.downedBoss3)] = NPC.downedBoss3;

			copiedData[nameof(NPC.downedQueenBee)] = NPC.downedQueenBee;
			copiedData[nameof(NPC.downedDeerclops)] = NPC.downedDeerclops;

			copiedData[nameof(NPC.downedQueenSlime)] = NPC.downedQueenSlime;

			copiedData[nameof(NPC.downedMechBoss1)] = NPC.downedMechBoss1;
			copiedData[nameof(NPC.downedMechBoss2)] = NPC.downedMechBoss2;
			copiedData[nameof(NPC.downedMechBoss3)] = NPC.downedMechBoss3;
			copiedData[nameof(NPC.downedMechBossAny)] = NPC.downedMechBossAny;

			copiedData[nameof(NPC.downedPlantBoss)] = NPC.downedPlantBoss;
			copiedData[nameof(NPC.downedGolemBoss)] = NPC.downedGolemBoss;

			copiedData[nameof(NPC.downedFishron)] = NPC.downedFishron;
			copiedData[nameof(NPC.downedEmpressOfLight)] = NPC.downedEmpressOfLight;

			copiedData[nameof(NPC.downedAncientCultist)] = NPC.downedAncientCultist;

			copiedData[nameof(NPC.downedTowerSolar)] = NPC.downedTowerSolar;
			copiedData[nameof(NPC.downedTowerVortex)] = NPC.downedTowerVortex;
			copiedData[nameof(NPC.downedTowerNebula)] = NPC.downedTowerNebula;
			copiedData[nameof(NPC.downedTowerStardust)] = NPC.downedTowerStardust;

			copiedData[nameof(NPC.downedMoonlord)] = NPC.downedMoonlord;

			copiedData[nameof(NPC.downedGoblins)] = NPC.downedGoblins;
			copiedData[nameof(NPC.downedClown)] = NPC.downedClown;
			copiedData[nameof(NPC.downedFrost)] = NPC.downedFrost;
			copiedData[nameof(NPC.downedPirates)] = NPC.downedPirates;
			copiedData[nameof(NPC.downedMartians)] = NPC.downedMartians;

			copiedData[nameof(NPC.downedHalloweenTree)] = NPC.downedHalloweenTree;
			copiedData[nameof(NPC.downedHalloweenKing)] = NPC.downedHalloweenKing;

			copiedData[nameof(NPC.downedChristmasTree)] = NPC.downedChristmasTree;
			copiedData[nameof(NPC.downedChristmasSantank)] = NPC.downedChristmasSantank;
			copiedData[nameof(NPC.downedChristmasIceQueen)] = NPC.downedChristmasIceQueen;

			copiedData[nameof(DD2Event.DownedInvasionT1)] = DD2Event.DownedInvasionT1;
			copiedData[nameof(DD2Event.DownedInvasionT2)] = DD2Event.DownedInvasionT2;
			copiedData[nameof(DD2Event.DownedInvasionT3)] = DD2Event.DownedInvasionT3;
		}

		private static void ReadCopiedDowned()
		{
			NPC.downedSlimeKing = copiedData.Get<bool>(nameof(NPC.downedSlimeKing));

			NPC.downedBoss1 = copiedData.Get<bool>(nameof(NPC.downedBoss1));
			NPC.downedBoss2 = copiedData.Get<bool>(nameof(NPC.downedBoss2));
			NPC.downedBoss3 = copiedData.Get<bool>(nameof(NPC.downedBoss3));

			NPC.downedQueenBee = copiedData.Get<bool>(nameof(NPC.downedQueenBee));
			NPC.downedDeerclops = copiedData.Get<bool>(nameof(NPC.downedDeerclops));

			NPC.downedQueenSlime = copiedData.Get<bool>(nameof(NPC.downedQueenSlime));

			NPC.downedMechBoss1 = copiedData.Get<bool>(nameof(NPC.downedMechBoss1));
			NPC.downedMechBoss2 = copiedData.Get<bool>(nameof(NPC.downedMechBoss2));
			NPC.downedMechBoss3 = copiedData.Get<bool>(nameof(NPC.downedMechBoss3));
			NPC.downedMechBossAny = copiedData.Get<bool>(nameof(NPC.downedMechBossAny));

			NPC.downedPlantBoss = copiedData.Get<bool>(nameof(NPC.downedPlantBoss));
			NPC.downedGolemBoss = copiedData.Get<bool>(nameof(NPC.downedGolemBoss));

			NPC.downedFishron = copiedData.Get<bool>(nameof(NPC.downedFishron));
			NPC.downedEmpressOfLight = copiedData.Get<bool>(nameof(NPC.downedEmpressOfLight));

			NPC.downedAncientCultist = copiedData.Get<bool>(nameof(NPC.downedAncientCultist));

			NPC.downedTowerSolar = copiedData.Get<bool>(nameof(NPC.downedTowerSolar));
			NPC.downedTowerVortex = copiedData.Get<bool>(nameof(NPC.downedTowerVortex));
			NPC.downedTowerNebula = copiedData.Get<bool>(nameof(NPC.downedTowerNebula));
			NPC.downedTowerStardust = copiedData.Get<bool>(nameof(NPC.downedTowerStardust));

			NPC.downedMoonlord = copiedData.Get<bool>(nameof(NPC.downedMoonlord));

			NPC.downedGoblins = copiedData.Get<bool>(nameof(NPC.downedGoblins));
			NPC.downedClown = copiedData.Get<bool>(nameof(NPC.downedClown));
			NPC.downedFrost = copiedData.Get<bool>(nameof(NPC.downedFrost));
			NPC.downedPirates = copiedData.Get<bool>(nameof(NPC.downedPirates));
			NPC.downedMartians = copiedData.Get<bool>(nameof(NPC.downedMartians));

			NPC.downedHalloweenTree = copiedData.Get<bool>(nameof(NPC.downedHalloweenTree));
			NPC.downedHalloweenKing = copiedData.Get<bool>(nameof(NPC.downedHalloweenKing));

			NPC.downedChristmasTree = copiedData.Get<bool>(nameof(NPC.downedChristmasTree));
			NPC.downedChristmasSantank = copiedData.Get<bool>(nameof(NPC.downedChristmasSantank));
			NPC.downedChristmasIceQueen = copiedData.Get<bool>(nameof(NPC.downedChristmasIceQueen));

			DD2Event.DownedInvasionT1 = copiedData.Get<bool>(nameof(DD2Event.DownedInvasionT1));
			DD2Event.DownedInvasionT2 = copiedData.Get<bool>(nameof(DD2Event.DownedInvasionT2));
			DD2Event.DownedInvasionT3 = copiedData.Get<bool>(nameof(DD2Event.DownedInvasionT3));
		}

		private static void CheckBytes()
		{
			if (!NetMessage.buffer[256].checkBytes)
			{
				return;
			}

			lock (NetMessage.buffer[256])
			{
				int pos = 0;
				int len = NetMessage.buffer[256].totalData;

				while (len >= 2)
				{
					int packetLen = BitConverter.ToUInt16(NetMessage.buffer[256].readBuffer, pos);
					if (len < packetLen)
					{
						break;
					}

					BinaryReader reader = NetMessage.buffer[256].reader;
					if (reader == null)
					{
						NetMessage.buffer[256].ResetReader();
						reader = NetMessage.buffer[256].reader;
					}

					long streamPos = reader.BaseStream.Position;
					reader.BaseStream.Position = pos + 3;

					//ModContent.GetInstance<SubworldLibrary>().Logger.Info(Convert.ToHexString(NetMessage.buffer[256].readBuffer, pos, len));

					ModNet.GetMod(ModNet.NetModCount < 256 ? reader.ReadByte() : reader.ReadUInt16()).HandlePacket(reader, 256);

					reader.BaseStream.Position = streamPos + packetLen;
					len -= packetLen;
					pos += packetLen;
				}

				if (len != NetMessage.buffer[256].totalData)
				{
					// this is what vanilla does, but BlockCopy may be faster
					for (int i = 0; i < len; i++)
					{
						NetMessage.buffer[256].readBuffer[i] = NetMessage.buffer[256].readBuffer[i + pos];
					}
					NetMessage.buffer[256].totalData = len;
				}

				NetMessage.buffer[256].checkBytes = false;
			}
		}

		private static void LoadIntoSubworld()
		{
			playerLocations = null;

			if (Program.LaunchParameters.TryGetValue("-subworld", out string id))
			{
				for (int i = 0; i < subworlds.Count; i++)
				{
					if (subworlds[i].FileName != id)
					{
						continue;
					}

					Main.myPlayer = 255;
					main = Main.ActiveWorldFileData;
					current = subworlds[i];

					pipeIn = new NamedPipeClientStream(".", current.FileName + ".IN", PipeDirection.In);
					pipeIn.Connect();

					copiedData = TagIO.FromStream(pipeIn);
					LoadWorld();
					copiedData = null;

					// replicates Netplay.InitializeServer, no need to set ReadBuffer because it's not used
					for (int j = 0; j < 256; j++)
					{
						Netplay.Clients[j].Id = j;
						Netplay.Clients[j].TileSections = new bool[Main.maxTilesX / 200 + 1, Main.maxTilesY / 150 + 1];
						Netplay.Clients[j].Reset();
					}
					SubserverSocket.address = new TcpAddress(IPAddress.Any, 0);

					new Thread(SubserverLoop)
					{
						IsBackground = true
					}.Start();

					new Thread(CheckTimeout)
					{
						IsBackground = true
					}.Start();

					pipeOut = new NamedPipeClientStream(".", current.FileName + ".OUT", PipeDirection.Out);
					pipeOut.Connect();

					return;
				}
			}

			Netplay.Disconnect = true;
			Main.instance.Exit();
		}

		private static void SubserverLoop()
		{
			try
			{
				while (pipeIn.IsConnected && !Netplay.Disconnect)
				{
					byte[] packetInfo = new byte[3];
					if (pipeIn.Read(packetInfo) < 3)
					{
						break;
					}

					suppressAutoShutdown = 0;

					MessageBuffer buffer = NetMessage.buffer[packetInfo[0]];
					int length = BitConverter.ToUInt16(packetInfo, 1);

					lock (buffer)
					{
						while (buffer.totalData + length > buffer.readBuffer.Length)
						{
							Monitor.Exit(buffer);
							Thread.Yield();
							Monitor.Enter(buffer);
						}

						buffer.readBuffer[buffer.totalData] = packetInfo[1];
						buffer.readBuffer[buffer.totalData + 1] = packetInfo[2];
						pipeIn.Read(buffer.readBuffer, buffer.totalData + 2, length - 2);

						if (packetInfo[0] == 255 && buffer.readBuffer[buffer.totalData + 2] == 255)
						{
							// this packet actually came from the main server, put it in message buffer 256 for reading on the main thread
							MessageBuffer serverBuffer = NetMessage.buffer[256];
							lock (serverBuffer)
							{
								Buffer.BlockCopy(buffer.readBuffer, buffer.totalData, serverBuffer.readBuffer, serverBuffer.totalData, length);
								serverBuffer.totalData += length;
								serverBuffer.checkBytes = true;
							}
							continue;
						}

						if (!Netplay.Clients[buffer.whoAmI].IsActive)
						{
							RemoteClient client = Netplay.Clients[buffer.whoAmI];
							if (client.Socket == null)
							{
								client.Socket = new SubserverSocket(buffer.whoAmI);
							}
							client.State = 1;
							client.IsActive = true;
						}

						buffer.totalData += length;
						buffer.checkBytes = true;
					}
				}
			}
			finally
			{
				Netplay.Disconnect = true;
				pipeIn?.Close();
				pipeOut?.Close();
			}
		}

		private static void CheckTimeout()
		{
			Stopwatch timer = new Stopwatch();
			timer.Start();

			while (true)
			{
				if (suppressAutoShutdown == 0)
				{
					timer.Restart();
					suppressAutoShutdown = -1;
				}
				else if (timer.ElapsedMilliseconds > 30000)
				{
					ModContent.GetInstance<SubworldLibrary>().Logger.Info("No packets received in 30 seconds, closing");
					Netplay.Disconnect = true;
					return;
				}
			}
		}

		internal static void ExitWorldCallBack(object index)
		{
			// presumably avoids a race condition?
			int netMode = Main.netMode;

			if (index != null)
			{
				if (netMode == 0)
				{
					WorldFile.CacheSaveTime();

					if (copiedData == null)
					{
						copiedData = new TagCompound();
					}
					if (cache != null)
					{
						cache.CopySubworldData();
						cache.OnExit();
					}

					CopyMainWorldData();
				}
				else
				{
					Netplay.Connection.State = 3;
					cache?.OnExit();
				}
			}

			Main.invasionProgress = -1;
			Main.invasionProgressDisplayLeft = 0;
			Main.invasionProgressAlpha = 0;
			Main.invasionProgressIcon = 0;

			noReturn = false;

			if (current != null)
			{
				hideUnderworld = true;
				current.OnEnter();
			}
			else
			{
				hideUnderworld = false;
			}

			SoundEngine.StopTrackedSounds();
			CaptureInterface.ResetFocus();

			Main.ActivePlayerFileData.StopPlayTimer();
			Player.SavePlayer(Main.ActivePlayerFileData);
			Player.ClearPlayerTempInfo();

			Rain.ClearRain();

			if (netMode != 1)
			{
				WorldFile.SaveWorld();
			}
			else if (index == null)
			{
				Netplay.Disconnect = true;
				Main.netMode = 0;
			}
			SystemLoader.OnWorldUnload();

			Main.fastForwardTimeToDawn = false;
			Main.fastForwardTimeToDusk = false;
			Main.UpdateTimeRate();

			if (index == null)
			{
				cache = null;
				Main.menuMode = 0;
				return;
			}

			WorldGen.noMapUpdate = true;
			if (cache != null && cache.NoPlayerSaving)
			{
				PlayerFileData playerData = Player.GetFileData(Main.ActivePlayerFileData.Path, Main.ActivePlayerFileData.IsCloudSave);
				if (playerData != null)
				{
					playerData.Player.whoAmI = Main.myPlayer;
					playerData.SetAsActive();
				}
			}

			for (int i = 0; i < 255; i++)
			{
				if (i != Main.myPlayer)
				{
					Main.player[i].active = false;
				}
			}

			if (netMode != 1)
			{
				LoadWorld();
			}
			// the subserver prompts packets from the client first now, so this is no longer needed
			/*else
			{
				NetMessage.SendData(1);
				Main.autoPass = true;
			}*/
		}

		private static void LoadWorld()
		{
			bool isSubworld = current != null;
			bool cloud = main.IsCloudSave;
			string path = isSubworld ? CurrentPath : main.Path;

			Main.rand = new UnifiedRandom((int)DateTime.Now.Ticks);

			cache?.OnUnload();

			Main.ToggleGameplayUpdates(false);

			WorldGen.gen = true;
			WorldGen.loadFailed = false;
			WorldGen.loadSuccess = false;

			if (!isSubworld || current.ShouldSave)
			{
				if (!isSubworld)
				{
					Main.ActiveWorldFileData = main;
				}

				TryLoadWorldFile(path, cloud, 0);
			}

			if (isSubworld)
			{
				if (WorldGen.loadFailed)
				{
					ModContent.GetInstance<SubworldLibrary>().Logger.Warn("Failed to load \"" + Main.worldName + (WorldGen.worldBackup ? "\" from file" : "\" from file, no backup"));
				}

				if (!WorldGen.loadSuccess)
				{
					LoadSubworld(path, cloud);
				}

				current.OnLoad();
			}
			else if (!WorldGen.loadSuccess)
			{
				ModContent.GetInstance<SubworldLibrary>().Logger.Error("Failed to load \"" + main.Name + (WorldGen.worldBackup ? "\" from file" : "\" from file, no backup"));
				Main.menuMode = 0;
				if (Main.netMode == 2)
				{
					Netplay.Disconnect = true;
				}
				return;
			}

			WorldGen.gen = false;

			if (Main.netMode != 2)
			{
				if (Main.mapEnabled)
				{
					Main.Map.Load();
				}
				Main.sectionManager.SetAllSectionsLoaded();
				while (Main.mapEnabled && Main.loadMapLock)
				{
					Main.statusText = Lang.gen[68].Value + " " + (int)((float)Main.loadMapLastX / Main.maxTilesX * 100 + 1) + "%";
					Thread.Sleep(0);
				}

				if (Main.anglerWhoFinishedToday.Contains(Main.LocalPlayer.name))
				{
					Main.anglerQuestFinished = true;
				}

				Main.QueueMainThreadAction(SpawnPlayer);
			}
		}

		private static void SpawnPlayer()
		{
			Main.LocalPlayer.Spawn(PlayerSpawnContext.SpawningIntoWorld);
			WorldFile.SetOngoingToTemps();
			Main.resetClouds = true;
			Main.gameMenu = false;
		}

		private static void OnEnterWorld(Player player)
		{
			if (Main.netMode == 1)
			{
				cache?.OnUnload();
				current?.OnLoad();
			}
			cache = current;
		}

		private static void OnDisconnect()
		{
			if (current != null || cache != null)
			{
				Main.menuMode = 14;
			}
			current = null;
			cache = null;
		}

		private static void LoadSubworld(string path, bool cloud)
		{
			Main.worldName = current.DisplayName.Value;
			if (Main.netMode == 2)
			{
				Console.Title = Main.worldName;
			}
			WorldFileData data = new WorldFileData(path, cloud)
			{
				Name = Main.worldName,
				CreationTime = DateTime.Now,
				Metadata = FileMetadata.FromCurrentSettings(FileType.World),
				WorldGeneratorVersion = Main.WorldGeneratorVersion,
				UniqueId = Guid.NewGuid()
			};
			Main.ActiveWorldFileData = data;

			Main.maxTilesX = current.Width;
			Main.maxTilesY = current.Height;
			Main.spawnTileX = Main.maxTilesX / 2;
			Main.spawnTileY = Main.maxTilesY / 2;
			WorldGen.setWorldSize();
			WorldGen.clearWorld();
			Main.worldSurface = Main.maxTilesY * 0.3;
			Main.rockLayer = Main.maxTilesY * 0.5;
			GenVars.waterLine = Main.maxTilesY;
			Main.weatherCounter = 18000;
			Cloud.resetClouds();

			ReadCopiedMainWorldData();

			double weight = 0;
			for (int i = 0; i < current.Tasks.Count; i++)
			{
				weight += current.Tasks[i].Weight;
			}
			WorldGenerator.CurrentGenerationProgress = new GenerationProgress();
			WorldGenerator.CurrentGenerationProgress.TotalWeight = weight;

			WorldGenConfiguration config = current.Config;

			for (int i = 0; i < current.Tasks.Count; i++)
			{
				WorldGen._genRand = new UnifiedRandom(data.Seed);
				Main.rand = new UnifiedRandom(data.Seed);

				GenPass task = current.Tasks[i];

				WorldGenerator.CurrentGenerationProgress.Start(task.Weight);
				task.Apply(WorldGenerator.CurrentGenerationProgress, config?.GetPassConfiguration(task.Name));
				WorldGenerator.CurrentGenerationProgress.End();
			}
			WorldGenerator.CurrentGenerationProgress = null;

			Main.WorldFileMetadata = FileMetadata.FromCurrentSettings(FileType.World);

			if (current.ShouldSave)
			{
				WorldFile.SaveWorld(cloud);
			}

			SystemLoader.OnWorldLoad();
		}

		private static void TryLoadWorldFile(string path, bool cloud, int tries)
		{
			LoadWorldFile(path, cloud);
			if (WorldGen.loadFailed)
			{
				if (tries == 1)
				{
					if (FileUtilities.Exists(path + ".bak", cloud))
					{
						WorldGen.worldBackup = true;

						FileUtilities.Copy(path, path + ".bad", cloud);
						FileUtilities.Copy(path + ".bak", path, cloud);
						FileUtilities.Delete(path + ".bak", cloud);

						string tMLPath = Path.ChangeExtension(path, ".twld");
						if (FileUtilities.Exists(tMLPath, cloud))
						{
							FileUtilities.Copy(tMLPath, tMLPath + ".bad", cloud);
						}
						if (FileUtilities.Exists(tMLPath + ".bak", cloud))
						{
							FileUtilities.Copy(tMLPath + ".bak", tMLPath, cloud);
							FileUtilities.Delete(tMLPath + ".bak", cloud);
						}
					}
					else
					{
						WorldGen.worldBackup = false;
						return;
					}
				}
				else if (tries == 3)
				{
					FileUtilities.Copy(path, path + ".bak", cloud);
					FileUtilities.Copy(path + ".bad", path, cloud);
					FileUtilities.Delete(path + ".bad", cloud);

					string tMLPath = Path.ChangeExtension(path, ".twld");
					if (FileUtilities.Exists(tMLPath, cloud))
					{
						FileUtilities.Copy(tMLPath, tMLPath + ".bak", cloud);
					}
					if (FileUtilities.Exists(tMLPath + ".bad", cloud))
					{
						FileUtilities.Copy(tMLPath + ".bad", tMLPath, cloud);
						FileUtilities.Delete(tMLPath + ".bad", cloud);
					}

					return;
				}
				TryLoadWorldFile(path, cloud, tries++);
			}
		}

		private static void LoadWorldFile(string path, bool cloud)
		{
			bool flag = cloud && SocialAPI.Cloud != null;
			if (!FileUtilities.Exists(path, flag))
			{
				return;
			}

			if (current != null)
			{
				Main.ActiveWorldFileData = new WorldFileData(path, cloud);
			}

			try
			{
				int status;
				using (BinaryReader reader = new BinaryReader(new MemoryStream(FileUtilities.ReadAllBytes(path, flag))))
				{
					status = current != null ? current.ReadFile(reader) : WorldFile.LoadWorld_Version2(reader);
				}
				if (Main.netMode == 2)
				{
					Console.Title = Main.worldName;
				}
				SystemLoader.OnWorldLoad();
				typeof(ModLoader).Assembly.GetType("Terraria.ModLoader.IO.WorldIO").GetMethod("Load", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { path, flag });
				if (status != 0)
				{
					WorldGen.loadFailed = true;
					WorldGen.loadSuccess = false;
					return;
				}
				WorldGen.loadSuccess = true;
				WorldGen.loadFailed = false;

				if (current != null)
				{
					current.PostReadFile();
					cache?.ReadCopiedSubworldData();
					ReadCopiedMainWorldData();
				}
				else
				{
					PostLoadWorldFile();
					cache.ReadCopiedSubworldData();
					ReadCopiedMainWorldData();
					copiedData = null;
				}
			}
			catch
			{
				WorldGen.loadFailed = true;
				WorldGen.loadSuccess = false;
			}
		}

		internal static void PostLoadWorldFile()
		{
			GenVars.waterLine = Main.maxTilesY;
			Liquid.QuickWater(2);
			WorldGen.WaterCheck();
			Liquid.quickSettle = true;
			int updates = 0;
			int amount = Liquid.numLiquid + LiquidBuffer.numLiquidBuffer;
			float num = 0;
			while (Liquid.numLiquid > 0 && updates < 100000)
			{
				updates++;
				float progress = (amount - Liquid.numLiquid + LiquidBuffer.numLiquidBuffer) / (float)amount;
				if (Liquid.numLiquid + LiquidBuffer.numLiquidBuffer > amount)
				{
					amount = Liquid.numLiquid + LiquidBuffer.numLiquidBuffer;
				}
				if (progress > num)
				{
					num = progress;
				}
				else
				{
					progress = num;
				}
				Main.statusText = Lang.gen[27].Value + " " + (int)(progress * 100 / 2 + 50) + "%";
				Liquid.UpdateLiquid();
			}
			Liquid.quickSettle = false;
			Main.weatherCounter = WorldGen.genRand.Next(3600, 18000);
			Cloud.resetClouds();
			WorldGen.WaterCheck();
			NPC.setFireFlyChance();
			if (Main.slimeRainTime > 0)
			{
				Main.StartSlimeRain(false);
			}
			NPC.SetWorldSpecificMonstersByWorldID();
		}
	}
}