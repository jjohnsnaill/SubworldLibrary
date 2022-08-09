using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoMod.Cil;
using MonoMod.RuntimeDetour.HookGen;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.Graphics.Capture;
using Terraria.Graphics.Light;
using Terraria.IO;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.Net;
using Terraria.Net.Sockets;
using Terraria.Social;
using Terraria.Utilities;
using Terraria.WorldBuilding;
using static Mono.Cecil.Cil.OpCodes;

namespace SubworldLibrary
{
	public class SubworldLibrary : Mod
	{
		private static event ILContext.Manipulator AsyncSend
		{
			add
			{
				HookEndpointManager.Modify(typeof(SocialSocket).GetMethod("Terraria.Net.Sockets.ISocket.AsyncSend", BindingFlags.NonPublic | BindingFlags.Instance), value);
				HookEndpointManager.Modify(typeof(TcpSocket).GetMethod("Terraria.Net.Sockets.ISocket.AsyncSend", BindingFlags.NonPublic | BindingFlags.Instance), value);
			}
			remove
			{
				HookEndpointManager.Unmodify(typeof(SocialSocket).GetMethod("Terraria.Net.Sockets.ISocket.AsyncSend", BindingFlags.NonPublic | BindingFlags.Instance), value);
				HookEndpointManager.Unmodify(typeof(TcpSocket).GetMethod("Terraria.Net.Sockets.ISocket.AsyncSend", BindingFlags.NonPublic | BindingFlags.Instance), value);
			}
		}

		public override void Load()
		{
			ModTranslation translation = LocalizationLoader.CreateTranslation("Mods.SubworldLibrary.Return");
			translation.AddTranslation(1, "Return");
			translation.AddTranslation(2, "Wiederkehren");
			translation.AddTranslation(3, "Ritorno");
			translation.AddTranslation(4, "Retour");
			translation.AddTranslation(5, "Regresar");
			translation.AddTranslation(6, "\u0412\u043E\u0437\u0432\u0440\u0430\u0449\u0430\u0442\u044C\u0441\u044F");
			translation.AddTranslation(7, "\u8FD4\u56DE");
			translation.AddTranslation(8, "Regressar");
			translation.AddTranslation(9, "Wraca\u0107");
			LocalizationLoader.AddTranslation(translation);

			FieldInfo current = typeof(SubworldSystem).GetField("current", BindingFlags.NonPublic | BindingFlags.Static);
			FieldInfo cache = typeof(SubworldSystem).GetField("cache", BindingFlags.NonPublic | BindingFlags.Static);
			FieldInfo hideUnderworld = typeof(SubworldSystem).GetField("hideUnderworld");
			MethodInfo normalUpdates = typeof(Subworld).GetMethod("get_NormalUpdates");
			MethodInfo shouldSave = typeof(Subworld).GetMethod("get_ShouldSave");

			if (Main.dedServ)
			{
				IL.Terraria.Main.DedServ_PostModLoad += il =>
				{
					ConstructorInfo gameTime = typeof(GameTime).GetConstructor(Type.EmptyTypes);
					MethodInfo update = typeof(Main).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance);
					FieldInfo saveTime = typeof(Main).GetField("saveTime", BindingFlags.NonPublic | BindingFlags.Static);

					var c = new ILCursor(il);

					if (!c.TryGotoNext(MoveType.After, i => i.MatchStindI1()))
					{
						return;
					}

					c.Emit(Mono.Cecil.Cil.OpCodes.Call, typeof(SubworldSystem).GetMethod("LoadIntoSubworld", BindingFlags.NonPublic | BindingFlags.Static));
					var skip = c.DefineLabel();
					c.Emit(Brfalse, skip);

					c.Emit(Ldarg_0);
					c.Emit(Newobj, gameTime);
					c.Emit(Callvirt, update);

					c.Emit(Newobj, typeof(Stopwatch).GetConstructor(Type.EmptyTypes));
					c.Emit(Stloc_1);
					c.Emit(Ldloc_1);
					c.Emit(Callvirt, typeof(Stopwatch).GetMethod("Start"));

					c.Emit(Ldc_I4_0);
					c.Emit(Stsfld, typeof(Main).GetField("gameMenu"));

					c.Emit(Ldc_R8, 16.666666666666668);
					c.Emit(Stloc_2);
					c.Emit(Ldloc_2);
					c.Emit(Stloc_3);

					var loopStart = c.DefineLabel();
					c.Emit(Br, loopStart);

					var loop = c.DefineLabel();
					c.MarkLabel(loop);

					c.Emit(Mono.Cecil.Cil.OpCodes.Call, typeof(Main).Assembly.GetType("Terraria.ModLoader.Engine.ServerHangWatchdog").GetMethod("Checkin", BindingFlags.NonPublic | BindingFlags.Static));

					c.Emit(Ldsfld, typeof(Netplay).GetField("HasClients"));
					var label = c.DefineLabel();
					c.Emit(Brfalse, label);

					c.Emit(Ldarg_0);
					c.Emit(Newobj, gameTime);
					c.Emit(Callvirt, update);
					var label2 = c.DefineLabel();
					c.Emit(Br, label2);

					c.MarkLabel(label);

					c.Emit(Ldsfld, saveTime);
					c.Emit(Callvirt, typeof(Stopwatch).GetMethod("get_IsRunning"));
					c.Emit(Brfalse, label2);

					c.Emit(Ldsfld, saveTime);
					c.Emit(Callvirt, typeof(Stopwatch).GetMethod("Stop"));
					c.Emit(Br, label2);

					c.MarkLabel(label2);

					c.Emit(Ldloc_1);
					c.Emit(Ldloc_2);
					c.Emit(Ldloca, 3);
					c.Emit(Mono.Cecil.Cil.OpCodes.Call, typeof(SubworldLibrary).GetMethod("Sleep", BindingFlags.NonPublic | BindingFlags.Static));

					c.MarkLabel(loopStart);

					c.Emit(Ldsfld, typeof(Netplay).GetField("Disconnect"));
					c.Emit(Brfalse, loop);

					c.Emit(Ret);

					c.MarkLabel(skip);
				};

				AsyncSend += il =>
				{
					var c = new ILCursor(il);

					c.Emit(Ldarg_0);
					c.Emit(Ldarg_1);
					c.Emit(Ldarg_2);
					c.Emit(Ldarg_3);
					c.Emit(Ldarga, 5);
					c.Emit(Mono.Cecil.Cil.OpCodes.Call, typeof(SubworldLibrary).GetMethod("SendToSubservers", new Type[] { typeof(ISocket), typeof(byte[]), typeof(int), typeof(int), typeof(object).MakeByRefType() }));
					var label = c.DefineLabel();
					c.Emit(Brfalse, label);
					c.Emit(Ret);
					c.MarkLabel(label);
				};
			}
			else
			{
				IL.Terraria.Main.DoDraw += il =>
				{
					var c = new ILCursor(il);

					if (!c.TryGotoNext(MoveType.After, i => i.MatchStsfld(typeof(Main), "HoverItem")))
					{
						return;
					}

					c.Emit(Ldsfld, typeof(Main).GetField("gameMenu"));
					var skip = c.DefineLabel();
					c.Emit(Brfalse, skip);

					c.Emit(Ldsfld, current);
					var label = c.DefineLabel();
					c.Emit(Brfalse, label);

					c.Emit(Ldc_R4, 1f);
					c.Emit(Dup);
					c.Emit(Dup);
					c.Emit(Stsfld, typeof(Main).GetField("_uiScaleWanted", BindingFlags.NonPublic | BindingFlags.Static));
					c.Emit(Stsfld, typeof(Main).GetField("_uiScaleUsed", BindingFlags.NonPublic | BindingFlags.Static));
					c.Emit(Mono.Cecil.Cil.OpCodes.Call, typeof(Matrix).GetMethod("CreateScale", new Type[] { typeof(float) }));
					c.Emit(Stsfld, typeof(Main).GetField("_uiScaleMatrix", BindingFlags.NonPublic | BindingFlags.Static));

					c.Emit(Ldsfld, current);
					c.Emit(Ldarg_0);
					c.Emit(Callvirt, typeof(Subworld).GetMethod("DrawSetup"));
					c.Emit(Ret);

					c.MarkLabel(label);

					c.Emit(Ldsfld, cache);
					c.Emit(Brfalse, skip);

					c.Emit(Ldc_R4, 1f);
					c.Emit(Dup);
					c.Emit(Dup);
					c.Emit(Stsfld, typeof(Main).GetField("_uiScaleWanted", BindingFlags.NonPublic | BindingFlags.Static));
					c.Emit(Stsfld, typeof(Main).GetField("_uiScaleUsed", BindingFlags.NonPublic | BindingFlags.Static));
					c.Emit(Mono.Cecil.Cil.OpCodes.Call, typeof(Matrix).GetMethod("CreateScale", new Type[] { typeof(float) }));
					c.Emit(Stsfld, typeof(Main).GetField("_uiScaleMatrix", BindingFlags.NonPublic | BindingFlags.Static));

					c.Emit(Ldsfld, cache);
					c.Emit(Ldarg_0);
					c.Emit(Callvirt, typeof(Subworld).GetMethod("DrawSetup"));
					c.Emit(Ret);

					c.MarkLabel(skip);
				};

				IL.Terraria.Main.DrawBackground += il =>
				{
					var c = new ILCursor(il);

					if (!c.TryGotoNext(i => i.MatchLdcI4(330)))
					{
						return;
					}

					c.Emit(Ldsfld, hideUnderworld);
					var skip = c.DefineLabel();
					c.Emit(Brfalse, skip);

					c.Emit(Conv_R8);
					var label = c.DefineLabel();
					c.Emit(Br, label);

					c.MarkLabel(skip);

					if (!c.TryGotoNext(i => i.MatchStloc(2), i => i.MatchLdcR4(255)))
					{
						return;
					}

					c.MarkLabel(label);
				};

				IL.Terraria.Main.OldDrawBackground += il =>
				{
					var c = new ILCursor(il);

					if (!c.TryGotoNext(i => i.MatchLdcI4(230)))
					{
						return;
					}

					c.Emit(Ldsfld, hideUnderworld);
					var skip = c.DefineLabel();
					c.Emit(Brfalse, skip);

					c.Emit(Conv_R8);
					var label = c.DefineLabel();
					c.Emit(Br, label);

					c.MarkLabel(skip);

					if (!c.TryGotoNext(i => i.MatchStloc(18), i => i.MatchLdcI4(0)))
					{
						return;
					}

					c.MarkLabel(label);
				};

				IL.Terraria.IngameOptions.Draw += il =>
				{
					var c = new ILCursor(il);

					if (!c.TryGotoNext(i => i.MatchLdsfld(typeof(Lang), "inter"), i => i.MatchLdcI4(35)))
					{
						return;
					}

					c.Emit(Ldsfld, current);
					var skip = c.DefineLabel();
					c.Emit(Brfalse, skip);

					c.Emit(Ldstr, "Mods.SubworldLibrary.Return");
					c.Emit(Mono.Cecil.Cil.OpCodes.Call, typeof(Language).GetMethod("GetTextValue", new Type[] { typeof(string) }));
					var label = c.DefineLabel();
					c.Emit(Br, label);

					c.MarkLabel(skip);

					if (!c.TryGotoNext(MoveType.After, i => i.MatchCallvirt(typeof(LocalizedText), "get_Value")))
					{
						return;
					}

					c.MarkLabel(label);

					if (!c.TryGotoNext(i => i.MatchLdnull(), i => i.MatchCall(typeof(WorldGen), "SaveAndQuit")))
					{
						return;
					}

					c.Emit(Ldsfld, current);
					skip = c.DefineLabel();
					c.Emit(Brfalse, skip);

					c.Emit(Mono.Cecil.Cil.OpCodes.Call, typeof(SubworldSystem).GetMethod("Exit"));
					label = c.DefineLabel();
					c.Emit(Br, label);

					c.MarkLabel(skip);

					if (!c.TryGotoNext(MoveType.After, i => i.MatchCall(typeof(WorldGen), "SaveAndQuit")))
					{
						return;
					}

					c.MarkLabel(label);

					if (!c.TryGotoPrev(MoveType.AfterLabel, i => i.MatchLdloc(23), i => i.MatchLdcI4(1), i => i.MatchAdd(), i => i.MatchStloc(23)))
					{
						return;
					}

					c.Emit(Ldsfld, typeof(SubworldSystem).GetField("noReturn"));
					c.Emit(Brtrue, label);
				};

				IL.Terraria.Graphics.Light.TileLightScanner.GetTileLight += il =>
				{
					var c = new ILCursor(il);

					if (!c.TryGotoNext(MoveType.After, i => i.MatchStloc(1)))
					{
						return;
					}

					c.Emit(Ldsfld, current);
					var skip = c.DefineLabel();
					c.Emit(Brfalse, skip);

					c.Emit(Ldsfld, current);
					c.Emit(Ldloc_0);
					c.Emit(Ldarg_1);
					c.Emit(Ldarg_2);
					c.Emit(Ldloca, 1);
					c.Emit(Ldarg_3);
					c.Emit(Callvirt, typeof(Subworld).GetMethod("GetLight"));

					c.Emit(Brfalse, skip);
					c.Emit(Ret);

					c.MarkLabel(skip);

					if (!c.TryGotoNext(MoveType.AfterLabel, i => i.MatchLdarg(2), i => i.MatchCall(typeof(Main), "get_UnderworldLayer")))
					{
						return;
					}

					c.Emit(Ldsfld, hideUnderworld);
					skip = c.DefineLabel();
					c.Emit(Brtrue, skip);

					if (!c.TryGotoNext(MoveType.After, i => i.MatchCall(typeof(TileLightScanner), "ApplyHellLight")))
					{
						return;
					}

					c.MarkLabel(skip);
				};

				IL.Terraria.Player.UpdateBiomes += il =>
				{
					var c = new ILCursor(il);

					if (!c.TryGotoNext(MoveType.After, i => i.MatchStloc(9)))
					{
						return;
					}

					c.Emit(Ldsfld, hideUnderworld);
					var skip = c.DefineLabel();
					c.Emit(Brfalse, skip);

					c.Emit(Ldc_I4_0);
					var label = c.DefineLabel();
					c.Emit(Br, label);

					c.MarkLabel(skip);

					if (!c.TryGotoNext(i => i.MatchStloc(10)))
					{
						return;
					}

					c.MarkLabel(label);
				};

				IL.Terraria.Main.DrawUnderworldBackground += il =>
				{
					var c = new ILCursor(il);

					c.Emit(Ldsfld, hideUnderworld);
					var skip = c.DefineLabel();
					c.Emit(Brfalse, skip);
					c.Emit(Ret);
					c.MarkLabel(skip);
				};

				IL.Terraria.Netplay.AddCurrentServerToRecentList += il =>
				{
					var c = new ILCursor(il);

					c.Emit(Ldsfld, current);
					var skip = c.DefineLabel();
					c.Emit(Brfalse, skip);
					c.Emit(Ret);
					c.MarkLabel(skip);
				};
			}

			IL.Terraria.WorldGen.do_worldGenCallBack += il =>
			{
				var c = new ILCursor(il);

				if (!c.TryGotoNext(MoveType.After, i => i.MatchCall(typeof(WorldFile), "saveWorld")))
				{
					return;
				}

				c.Emit(Mono.Cecil.Cil.OpCodes.Call, typeof(SubworldSystem).GetMethod("GenerateSubworlds", BindingFlags.NonPublic | BindingFlags.Static));
			};

			IL.Terraria.Main.EraseWorld += il =>
			{
				var c = new ILCursor(il);

				c.Emit(Ldarg_0);
				c.Emit(Mono.Cecil.Cil.OpCodes.Call, typeof(SubworldSystem).GetMethod("EraseSubworlds", BindingFlags.NonPublic | BindingFlags.Static));
			};

			IL.Terraria.Main.DoUpdateInWorld += il =>
			{
				var c = new ILCursor(il);

				if (!c.TryGotoNext(MoveType.After, i => i.MatchCall(typeof(SystemLoader), "PreUpdateTime")))
				{
					return;
				}

				c.Emit(Ldsfld, current);
				var skip = c.DefineLabel();
				c.Emit(Brfalse, skip);

				c.Emit(Ldsfld, current);
				c.Emit(Callvirt, normalUpdates);
				var label = c.DefineLabel();
				c.Emit(Brfalse, label);

				c.MarkLabel(skip);

				if (!c.TryGotoNext(i => i.MatchCall(typeof(SystemLoader), "PostUpdateTime")))
				{
					return;
				}

				c.MarkLabel(label);
			};

			IL.Terraria.WorldGen.UpdateWorld += il =>
			{
				var c = new ILCursor(il);

				if (!c.TryGotoNext(i => i.MatchCall(typeof(WorldGen), "UpdateWorld_Inner")))
				{
					return;
				}

				c.Emit(Ldsfld, current);
				var skip = c.DefineLabel();
				c.Emit(Brfalse, skip);

				c.Emit(Ldsfld, current);
				c.Emit(Callvirt, normalUpdates);
				var label = c.DefineLabel();
				c.Emit(Brfalse, label);

				c.MarkLabel(skip);

				c.Index++;

				c.MarkLabel(label);
			};

			IL.Terraria.Player.Update += il =>
			{
				var c = new ILCursor(il);

				if (!c.TryGotoNext(MoveType.AfterLabel, i => i.MatchLdsfld(typeof(Main), "maxTilesX"), i => i.MatchLdcI4(4200)))
				{
					return;
				}

				c.Emit(Ldsfld, current);
				var skip = c.DefineLabel();
				c.Emit(Brfalse, skip);

				c.Emit(Ldsfld, current);
				c.Emit(Callvirt, normalUpdates);
				var label = c.DefineLabel();
				c.Emit(Brfalse, label);

				c.MarkLabel(skip);

				if (!c.TryGotoNext(MoveType.After, i => i.MatchLdloc(3), i => i.MatchMul(), i => i.MatchStfld(typeof(Player), "gravity")))
				{
					return;
				}

				c.MarkLabel(label);
			};

			IL.Terraria.NPC.UpdateNPC_UpdateGravity += il =>
			{
				var c = new ILCursor(il);

				if (!c.TryGotoNext(MoveType.AfterLabel, i => i.MatchLdsfld(typeof(Main), "maxTilesX"), i => i.MatchLdcI4(4200)))
				{
					return;
				}

				c.Emit(Ldsfld, current);
				var skip = c.DefineLabel();
				c.Emit(Brfalse, skip);

				c.Emit(Ldsfld, current);
				c.Emit(Callvirt, normalUpdates);
				var label = c.DefineLabel();
				c.Emit(Brfalse, label);

				c.MarkLabel(skip);

				if (!c.TryGotoNext(MoveType.After, i => i.MatchLdloc(1), i => i.MatchMul(), i => i.MatchStsfld(typeof(NPC), "gravity")))
				{
					return;
				}

				c.MarkLabel(label);
			};

			IL.Terraria.Liquid.Update += il =>
			{
				var c = new ILCursor(il);

				if (!c.TryGotoNext(i => i.MatchLdarg(0), i => i.MatchLdfld(typeof(Liquid), "y"), i => i.MatchCall(typeof(Main), "UnderworldLayer")))
				{
					return;
				}

				c.Emit(Ldsfld, current);
				var skip = c.DefineLabel();
				c.Emit(Brfalse, skip);

				c.Emit(Ldsfld, current);
				c.Emit(Callvirt, normalUpdates);
				var label = c.DefineLabel();
				c.Emit(Brfalse, label);

				c.MarkLabel(skip);

				if (!c.TryGotoNext(MoveType.After, i => i.MatchConvU1(), i => i.MatchStfld(typeof(Tile), "liquid")))
				{
					return;
				}

				c.MarkLabel(label);
			};

			IL.Terraria.Player.SavePlayer += il =>
			{
				var c = new ILCursor(il);

				if (!c.TryGotoNext(i => i.MatchCall(typeof(Player), "InternalSaveMap")))
				{
					return;
				}
				c.Index -= 3;

				c.Emit(Ldsfld, cache);
				var skip = c.DefineLabel();
				c.Emit(Brfalse, skip);

				c.Emit(Ldsfld, cache);
				c.Emit(Callvirt, shouldSave);
				var label = c.DefineLabel();
				c.Emit(Brfalse, label);

				c.MarkLabel(skip);

				if (!c.TryGotoNext(MoveType.AfterLabel, i => i.MatchLdsfld(typeof(Main), "ServerSideCharacter")))
				{
					return;
				}

				c.MarkLabel(label);

				c.Emit(Ldsfld, cache);
				skip = c.DefineLabel();
				c.Emit(Brfalse, skip);

				c.Emit(Ldsfld, cache);
				c.Emit(Callvirt, typeof(Subworld).GetMethod("get_NoPlayerSaving"));
				label = c.DefineLabel();
				c.Emit(Brtrue, label);

				c.MarkLabel(skip);

				if (!c.TryGotoNext(MoveType.After, i => i.MatchCall(typeof(FileUtilities), "ProtectedInvoke")))
				{
					return;
				}

				c.MarkLabel(label);
			};

			IL.Terraria.IO.WorldFile.SaveWorld_bool_bool += il =>
			{
				var c = new ILCursor(il);

				c.Emit(Ldsfld, cache);
				var skip = c.DefineLabel();
				c.Emit(Brfalse, skip);
				c.Emit(Ldsfld, cache);
				c.Emit(Callvirt, shouldSave);
				c.Emit(Brtrue, skip);
				c.Emit(Ret);

				c.MarkLabel(skip);
			};

			IL.Terraria.NetMessage.CheckBytes += il =>
			{
				var c = new ILCursor(il);

				if (!c.TryGotoNext(MoveType.After, i => i.MatchCallvirt(typeof(Stream), "get_Position"), i => i.MatchStloc(5)))
				{
					return;
				}

				c.Emit(Ldsfld, typeof(NetMessage).GetField("buffer"));
				c.Emit(Ldarg_0);
				c.Emit(Ldelem_Ref);
				c.Emit(Ldloc_2);
				c.Emit(Ldloc, 4);
				c.Emit(Mono.Cecil.Cil.OpCodes.Call, typeof(SubworldLibrary).GetMethod("SendToSubservers", new Type[] { typeof(MessageBuffer), typeof(int), typeof(int) }));

				var label = c.DefineLabel();
				c.Emit(Brtrue, label);

				if (!c.TryGotoNext(i => i.MatchLdsfld(typeof(NetMessage), "buffer"), i => i.MatchLdarg(0), i => i.MatchLdelemRef(), i => i.MatchLdfld(typeof(MessageBuffer), "reader"), i => i.MatchCallvirt(typeof(BinaryReader), "get_BaseStream")))
				{
					return;
				}

				c.MarkLabel(label);
			};
		}

		public static bool SendToSubservers(MessageBuffer buffer, int start, int length)
		{
			if (Main.netMode == 1)
			{
				return false;
			}
			if (buffer.readBuffer[start + 2] == 250 && (ModNet.NetModCount < 256 ? buffer.readBuffer[start + 3] : BitConverter.ToUInt16(buffer.readBuffer, start + 3)) == ModContent.GetInstance<SubworldLibrary>().NetID)
			{
				return false;
			}
			if (!SubworldSystem.playerLocations.ContainsKey(Netplay.Clients[buffer.whoAmI].Socket.GetRemoteAddress()))
			{
				return false;
			}

			Netplay.Clients[buffer.whoAmI].TimeOutTimer = 0;

			byte[] packet = new byte[length + 1];
			packet[0] = (byte)buffer.whoAmI;
			Buffer.BlockCopy(buffer.readBuffer, start, packet, 1, length);
			Task.Factory.StartNew(SendToSubserversCallBack, packet);

			//string str = ">R" + packet[3] + "(" + length + ") ";
			//for (int i = 0; i < length + 1; i++)
			//{
			//	str += packet[i] + " ";
			//}
			//ModContent.GetInstance<SubworldLibrary>().Logger.Info(str);

			return packet[3] != 2; // TODO: softcode this
		}

		public static bool SendToSubservers(ISocket socket, byte[] data, int start, int length, ref object state)
		{
			if (Main.netMode == 1)
			{
				return false;
			}
			if (!SubworldSystem.playerLocations.ContainsKey(socket.GetRemoteAddress()))
			{
				return false;
			}
			return state is not bool;
		}

		private static void SendToSubserversCallBack(object data)
		{
			using NamedPipeClientStream pipe = new NamedPipeClientStream(".", "World", PipeDirection.Out);
			pipe.Connect();
			pipe.Write((byte[])data);
		}

		private static void Sleep(Stopwatch stopwatch, double delta, ref double target)
		{
			double now = stopwatch.ElapsedMilliseconds;
			double remaining = target - now;
			target += delta;
			if (target < now)
			{
				target = now + delta;
			}
			if (remaining <= 0)
			{
				Thread.Sleep(0);
				return;
			}
			Thread.Sleep((int)remaining);
		}

		public override void HandlePacket(BinaryReader reader, int whoAmI)
		{
			switch (reader.ReadByte())
			{
				case 0:
					if (Main.netMode == 2)
					{
						ushort id = reader.ReadUInt16();

						RemoteAddress address = Netplay.Clients[whoAmI].Socket.GetRemoteAddress();
						if (address != null)
						{
							ModPacket packet = GetPacket();
							packet.Write((byte)0);
							packet.Write(id);
							packet.Send(whoAmI);

							Main.player[whoAmI].active = false;
							if (!SubworldSystem.playerLocations.ContainsValue(id))
							{
								SubworldSystem.StartSubserver(SubworldSystem.subworlds[id].FullName);
							}

							SubworldSystem.playerLocations[address] = id;
						}
					}
					else
					{
						Netplay.Connection.State = 1;
						SubworldSystem.current = SubworldSystem.subworlds[reader.ReadUInt16()];
						Task.Factory.StartNew(SubworldSystem.ExitWorldCallBack);
					}
					break;
				case 1:
					if (Main.netMode == 2)
					{
						RemoteAddress address = Netplay.Clients[whoAmI].Socket.GetRemoteAddress();
						if (address != null)
						{
							Task.Factory.StartNew(SendToSubserversCallBack, new byte[7] { (byte)whoAmI, 6, 0, 250, (byte)NetID, 2, (byte)whoAmI }); // client, size (ushort), packet id, sublib net id

							SubworldSystem.playerLocations.Remove(address);
							Netplay.Clients[whoAmI].State = 0;

							ModPacket packet = GetPacket();
							packet.Write((byte)1);
							packet.Send(whoAmI);
						}
					}
					else
					{
						Netplay.Connection.State = 1;
						SubworldSystem.current = null;
						Task.Factory.StartNew(SubworldSystem.ExitWorldCallBack);
					}
					break;
				case 2:
					byte index = reader.ReadByte();

					Main.player[index] = new Player();

					RemoteClient client = Netplay.Clients[index];
					client.IsActive = false;
					client.Socket = null;
					client.State = 0;
					client.ResetSections();
					client.SpamClear();

					NetMessage.SyncDisconnectedPlayer(index);

					bool connection = false;
					for (int i = 0; i < 255; i++)
					{
						if (Netplay.Clients[i].State > 0)
						{
							connection = true;
							break;
						}
					}
					if (!connection)
					{
						Netplay.Disconnect = true;
						Netplay.HasClients = false;
					}
					break;
			}
		}
	}

	public abstract class Subworld : ModType
	{
		protected sealed override void Register()
		{
			SubworldSystem.subworlds.Add(this);
			ModTypeLookup<Subworld>.Register(this);
		}

		public sealed override void SetupContent() => SetStaticDefaults();

		public abstract int Width { get; }
		public abstract int Height { get; }
		public abstract List<GenPass> Tasks { get; }
		public virtual WorldGenConfiguration Config => null;
		public virtual bool ShouldSave => false;
		public virtual bool NoPlayerSaving => false;
		public virtual bool NormalUpdates => false;
		public virtual void OnEnter() { }
		public virtual void OnExit() { }
		public virtual void OnLoad() { }
		public virtual void OnUnload() { }
		public virtual void SyncVariables() { }
		public virtual void DrawSetup(GameTime gameTime)
		{
			PlayerInput.SetZoom_Unscaled();

			Main.instance.GraphicsDevice.Clear(Color.Black);

			Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, Main.Rasterizer, null, Main.UIScaleMatrix);

			DrawMenu(gameTime);
			Main.DrawCursor(Main.DrawThickCursor());

			Main.spriteBatch.End();
		}
		public virtual void DrawMenu(GameTime gameTime)
		{
			Main.spriteBatch.DrawString(FontAssets.DeathText.Value, Main.statusText, new Vector2(Main.screenWidth, Main.screenHeight) / 2 - FontAssets.DeathText.Value.MeasureString(Main.statusText) / 2, Color.White);
		}
		public virtual bool GetLight(Tile tile, int x, int y, ref FastRandom rand, ref Vector3 color) => false;
	}

	public class SubserverSocket : ISocket
	{
		private readonly int index;

		public SubserverSocket(int index)
		{
			this.index = index;
		}

		void ISocket.AsyncReceive(byte[] data, int offset, int size, SocketReceiveCallback callback, object state = null) { }

		void ISocket.AsyncSend(byte[] data, int offset, int size, SocketSendCallback callback, object state = null)
		{
			byte[] packet = new byte[size + 1];
			packet[0] = (byte)index;
			Buffer.BlockCopy(data, offset, packet, 1, size);
			Task.Factory.StartNew(SendToMainServerCallBack, packet);

			//string str = "W" + packet[3] + "(" + size + ") ";
			//for (int i = 0; i < size + 1; i++)
			//{
			//	str += packet[i] + " ";
			//}
			//ModContent.GetInstance<SubworldLibrary>().Logger.Info(str);
		}

		void ISocket.Close() { }

		void ISocket.Connect(RemoteAddress address) { }

		RemoteAddress ISocket.GetRemoteAddress() => new TcpAddress(new IPAddress(new byte[4]), 0); // TODO: figure out the cleanest way to get the actual address in case anything needs it

		bool ISocket.IsConnected() => true;

		bool ISocket.IsDataAvailable() => true;

		void ISocket.SendQueuedPackets() { }

		bool ISocket.StartListening(SocketConnectionAccepted callback) => true;

		void ISocket.StopListening() { }

		private static void SendToMainServerCallBack(object data)
		{
			using NamedPipeClientStream pipe = new NamedPipeClientStream(".", SubworldSystem.current.FullName, PipeDirection.Out);
			pipe.Connect();
			pipe.Write((byte[])data);
		}
	}

	public class SubworldSystem : ModSystem
	{
		internal static List<Subworld> subworlds;
		public static Dictionary<RemoteAddress, int> playerLocations;
		internal static Subworld current;
		internal static Subworld cache;
		internal static WorldFileData main;

		public static bool noReturn;
		public static bool hideUnderworld;

		public override void OnModLoad()
		{
			subworlds = new List<Subworld>();
			playerLocations = new Dictionary<RemoteAddress, int>();
			Player.Hooks.OnEnterWorld += OnEnterWorld;
		}

		public override void Unload()
		{
			Player.Hooks.OnEnterWorld -= OnEnterWorld;
		}

		public static Subworld Current => current;
		public static bool IsActive(string id) => current?.FullName == id;
		public static bool IsActive<T>() where T : Subworld => current?.GetType() == typeof(T);
		public static bool AnyActive(Mod mod) => current?.Mod == mod;
		public static bool AnyActive<T>() where T : Mod => current?.Mod == ModContent.GetInstance<T>();
		public static string CurrentPath => Path.Combine(Main.WorldPath, Path.GetFileNameWithoutExtension(main.Path), current.Mod.Name + "_" + current.Name + ".wld");

		private static void BeginEntering(int index)
		{
			if (Main.netMode == 0)
			{
				if (current == null)
				{
					main = Main.ActiveWorldFileData;
				}
				current = subworlds[index];
				Task.Factory.StartNew(ExitWorldCallBack);
			}
			else if (Main.netMode == 1)
			{
				ModPacket packet = ModContent.GetInstance<SubworldLibrary>().GetPacket();
				packet.Write((byte)0);
				packet.Write((ushort)index);
				packet.Send();
			}
		}

		public static bool Enter(string id)
		{
			if (current == cache)
			{
				for (int i = 0; i < subworlds.Count; i++)
				{
					if (subworlds[i].FullName == id)
					{
						BeginEntering(i);
						return true;
					}
				}
			}
			return false;
		}

		public static bool Enter<T>() where T : Subworld
		{
			if (current == cache)
			{
				for (int i = 0; i < subworlds.Count; i++)
				{
					if (subworlds[i].GetType() == typeof(T))
					{
						BeginEntering(i);
						return true;
					}
				}
			}
			return false;
		}

		public static void Exit()
		{
			if (current != null && current == cache)
			{
				if (Main.netMode == 0)
				{
					current = null;
					Task.Factory.StartNew(ExitWorldCallBack);
				}
				else if (Main.netMode == 1)
				{
					ModPacket packet = ModContent.GetInstance<SubworldLibrary>().GetPacket();
					packet.Write((byte)1);
					packet.Send();
				}
			}
		}

		public static void StartSubserver(string id)
		{
			Process p = new Process();
			p.StartInfo.FileName = Process.GetCurrentProcess().MainModule!.FileName;
			p.StartInfo.Arguments = "tModLoader.dll -server -showserverconsole -world \"" + Main.worldPathName + "\" -subworld \"" + id + "\"";
			p.StartInfo.UseShellExecute = true;

			Task.Factory.StartNew(SubserverCallBack, id);
			p.Start();
		}

		private static void GenerateSubworlds()
		{
			main = Main.ActiveWorldFileData;
			bool cloud = main.IsCloudSave;

			foreach (Subworld subworld in subworlds)
			{
				if (subworld.ShouldSave)
				{
					current = subworld;
					LoadSubworld(CurrentPath, cloud);
					WorldFile.SaveWorld(cloud);
					Main.ActiveWorldFileData = main;
				}
			}
		}

		private static void EraseSubworlds(int index)
		{
			WorldFileData world = Main.WorldList[index];
			string path = Path.Combine(Main.WorldPath, Path.GetFileNameWithoutExtension(world.Path));
			if (FileUtilities.Exists(path, world.IsCloudSave))
			{
				FileUtilities.Delete(path, world.IsCloudSave);
			}
		}

		private static bool LoadIntoSubworld()
		{
			if (Program.LaunchParameters.TryGetValue("-subworld", out string id))
			{
				for (int i = 0; i < subworlds.Count; i++)
				{
					if (subworlds[i].FullName == id)
					{
						Main.myPlayer = 255;
						main = Main.ActiveWorldFileData;
						current = subworlds[i];
						LoadWorld();
						Console.Title = Main.worldName;

						for (int j = 0; j < Netplay.Clients.Length; j++)
						{
							Netplay.Clients[j].Id = j;
							Netplay.Clients[j].Reset();
							Netplay.Clients[j].ReadBuffer = null; // not used, saves 262kb
						}

						new Thread(new ThreadStart(ServerCallBack)).Start();

						return true;
					}
				}
				Main.instance.Exit();
			}

			return false;
		}

		private static void SubserverCallBack(object id)
		{
			using NamedPipeServerStream pipe = new NamedPipeServerStream((string)id, PipeDirection.In, -1);
			while (true)
			{
				pipe.WaitForConnection();

				int client = pipe.ReadByte();

				int lowByte = pipe.ReadByte();
				int highByte = pipe.ReadByte();
				int length = (highByte << 8) | lowByte;

				byte[] data = new byte[length];
				pipe.Read(data, 2, length - 2);
				data[0] = (byte)lowByte;
				data[1] = (byte)highByte;

				//string str = ">W" + data[2] + "(" + length + ") " + client + " ";
				//for (int i = 0; i < length; i++)
				//{
				//	str += data[i] + " ";
				//}
				//ModContent.GetInstance<SubworldLibrary>().Logger.Info(str);

				Netplay.Clients[client].Socket.AsyncSend(data, 0, length, (state) => { }, true);

				pipe.Disconnect();
			}
		}

		private static void ServerCallBack()
		{
			using NamedPipeServerStream pipe = new NamedPipeServerStream("World", PipeDirection.In, -1);
			while (!Netplay.Disconnect)
			{
				pipe.WaitForConnection();

				MessageBuffer buffer = NetMessage.buffer[pipe.ReadByte()];

				pipe.Read(buffer.readBuffer, 0, 2);
				int length = BitConverter.ToUInt16(buffer.readBuffer, 0);
				pipe.Read(buffer.readBuffer, 2, length - 2);

				if (buffer.readBuffer[2] == 1)
				{
					Netplay.Clients[buffer.whoAmI].Socket = new SubserverSocket(buffer.whoAmI);
					Netplay.Clients[buffer.whoAmI].IsActive = true;
					Netplay.HasClients = true;
				}

				//string str = "R" + buffer.readBuffer[2] + "(" + length + ") " + buffer.whoAmI + " ";
				//for (int i = 0; i < length; i++)
				//{
				//	str += buffer.readBuffer[i] + " ";
				//}
				//ModContent.GetInstance<SubworldLibrary>().Logger.Info(str);

				buffer.GetData(2, length - 2, out var _);

				pipe.Disconnect();
			}

			// no caching; world loading is very infrequent
			typeof(ModLoader).Assembly.GetType("Terraria.ModLoader.IO.TileIO").GetMethod("PostExitWorldCleanup", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
		}

		internal static void ExitWorldCallBack()
		{
			// added in 1.4, presumably avoids a race condition?
			int netMode = Main.netMode;

			if (netMode != 2)
			{
				cache?.OnExit();

				if (netMode == 0)
				{
					WorldFile.CacheSaveTime();
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

				Main.gameMenu = true;

				SoundEngine.StopTrackedSounds();
				CaptureInterface.ResetFocus();

				Main.ActivePlayerFileData.StopPlayTimer();
				Player.SavePlayer(Main.ActivePlayerFileData);
				Player.ClearPlayerTempInfo();

				Rain.ClearRain();
			}

			if (netMode != 1)
			{
				WorldFile.SaveWorld();
			}
			SystemLoader.OnWorldUnload();

			Main.fastForwardTime = false;
			Main.UpdateTimeRate();
			WorldGen.noMapUpdate = true;

			if (cache != null && cache.NoPlayerSaving && netMode != 2)
			{
				PlayerFileData playerData = Player.GetFileData(Main.ActivePlayerFileData.Path, Main.ActivePlayerFileData.IsCloudSave);
				if (playerData != null)
				{
					playerData.Player.whoAmI = Main.myPlayer;
					playerData.SetAsActive();
				}
			}

			if (netMode != 1)
			{
				LoadWorld();
			}
			else
			{
				NetMessage.SendData(1);
			}
		}

		private static void LoadWorld()
		{
			bool isSubworld = current != null;

			WorldGen.gen = true;
			WorldGen.loadFailed = false;
			WorldGen.loadSuccess = false;

			Main.rand = new UnifiedRandom((int)DateTime.Now.Ticks);

			bool cloud = main.IsCloudSave;
			string path = isSubworld ? CurrentPath : main.Path;

			if (!isSubworld || current.ShouldSave)
			{
				if (!isSubworld)
				{
					Main.ActiveWorldFileData = main;
				}

				TryLoadWorldFile(path, cloud, 0);
			}

			cache?.OnUnload();

			if (isSubworld)
			{
				Main.worldName = Language.GetTextValue("Mods." + current.Mod.Name + ".SubworldName." + current.Name);
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
				Main.sectionManager.SetAllFramesLoaded();
				while (Main.mapEnabled && Main.loadMapLock)
				{
					Main.statusText = Lang.gen[68].Value + " " + (int)((float)Main.loadMapLastX / Main.maxTilesX * 100 + 1) + "%";
					Thread.Sleep(0);
				}

				Player player = Main.LocalPlayer;
				if (Main.anglerWhoFinishedToday.Contains(player.name))
				{
					Main.anglerQuestFinished = true;
				}
				player.Spawn(PlayerSpawnContext.SpawningIntoWorld);
				Main.ActivePlayerFileData.StartPlayTimer();
				Player.Hooks.EnterWorld(Main.myPlayer);
				Main.resetClouds = true;
				Main.gameMenu = false;
			}
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

		private static void LoadSubworld(string path, bool cloud)
		{
			WorldFileData data = new WorldFileData(path, cloud)
			{
				Name = Main.worldName,
				GameMode = Main.GameMode,
				CreationTime = DateTime.Now,
				Metadata = FileMetadata.FromCurrentSettings(FileType.World),
				WorldGeneratorVersion = Main.WorldGeneratorVersion
			};
			data.SetSeed(main.SeedText);
			using (MD5 md5 = MD5.Create())
			{
				data.UniqueId = new Guid(md5.ComputeHash(Encoding.ASCII.GetBytes(Path.GetFileNameWithoutExtension(main.Path) + current.Name)));
			}
			Main.ActiveWorldFileData = data;

			Main.maxTilesX = current.Width;
			Main.maxTilesY = current.Height;
			Main.spawnTileX = Main.maxTilesX / 2;
			Main.spawnTileY = Main.maxTilesY / 2;
			WorldGen.setWorldSize();
			WorldGen.clearWorld();
			Main.worldSurface = Main.maxTilesY * 0.3;
			Main.rockLayer = Main.maxTilesY * 0.5;
			WorldGen.waterLine = Main.maxTilesY;
			Main.weatherCounter = int.MaxValue;
			Cloud.resetClouds();

			float weight = 0;
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
						WorldGen.worldBackup = false;

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
				WorldFileData data = WorldFile.GetAllMetadata(path, cloud);
				if (data != null)
				{
					Main.ActiveWorldFileData = data;
				}
			}

			using MemoryStream stream = new MemoryStream(FileUtilities.ReadAllBytes(path, flag));
			using BinaryReader reader = new BinaryReader(stream);

			try
			{
				int status = WorldFile.LoadWorld_Version2(reader);
				reader.Close();
				stream.Close();
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
				WorldGen.waterLine = Main.maxTilesY;
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
				Main.weatherCounter = int.MaxValue;
				Cloud.resetClouds();
				WorldGen.WaterCheck();
				if (Main.slimeRainTime > 0)
				{
					Main.StartSlimeRain(false);
				}
				WorldFile.SetOngoingToTemps();
			}
			catch
			{
				WorldGen.loadFailed = true;
				WorldGen.loadSuccess = false;
				try
				{
					reader.Close();
					stream.Close();
				}
				catch
				{
				}
			}
		}
	}
}