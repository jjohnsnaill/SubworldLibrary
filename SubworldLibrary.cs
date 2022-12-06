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
using System.Threading;
using System.Threading.Tasks;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.Events;
using Terraria.GameContent.NetModules;
using Terraria.GameInput;
using Terraria.Graphics.Capture;
using Terraria.Graphics.Light;
using Terraria.IO;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
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

				if (!Program.LaunchParameters.ContainsKey("-subworld"))
				{
					IL.Terraria.NetMessage.CheckBytes += il =>
					{
						var c = new ILCursor(il);

						if (!c.TryGotoNext(MoveType.After, i => i.MatchCall(typeof(BitConverter), "ToUInt16")) || !c.Instrs[c.Index].MatchStloc(out int index))
						{
							return;
						}

						if (!c.TryGotoNext(MoveType.After, i => i.MatchCallvirt(typeof(Stream), "get_Position"), i => i.MatchStloc(out _)))
						{
							return;
						}

						c.Emit(Ldsfld, typeof(NetMessage).GetField("buffer"));
						c.Emit(Ldarg_0);
						c.Emit(Ldelem_Ref);
						c.Emit(Ldloc_2);
						c.Emit(Ldloc, index);
						c.Emit(Mono.Cecil.Cil.OpCodes.Call, typeof(SubworldLibrary).GetMethod("DenyRead"));

						var label = c.DefineLabel();
						c.Emit(Brtrue, label);

						if (!c.TryGotoNext(i => i.MatchLdsfld(typeof(NetMessage), "buffer"), i => i.MatchLdarg(0), i => i.MatchLdelemRef(), i => i.MatchLdfld(typeof(MessageBuffer), "reader")))
						{
							return;
						}

						c.MarkLabel(label);
					};

					AsyncSend += il =>
					{
						var c = new ILCursor(il);

						c.Emit(Ldarg_0);
						c.Emit(Ldarg_1);
						c.Emit(Ldarg_2);
						c.Emit(Ldarg_3);
						c.Emit(Ldarga, 5);
						c.Emit(Mono.Cecil.Cil.OpCodes.Call, typeof(SubworldLibrary).GetMethod("DenySend"));
						var label = c.DefineLabel();
						c.Emit(Brfalse, label);
						c.Emit(Ret);
						c.MarkLabel(label);
					};
				}
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

					if (!c.TryGotoPrev(MoveType.AfterLabel, i => i.MatchLdloc(out _), i => i.MatchLdcI4(1), i => i.MatchAdd(), i => i.MatchStloc(out _)))
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

				if (!c.TryGotoNext(MoveType.AfterLabel, i => i.MatchLdsfld(out _), i => i.MatchLdcI4(4200)))
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

				if (!c.TryGotoNext(MoveType.AfterLabel, i => i.MatchLdsfld(out _), i => i.MatchLdcI4(4200)))
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

				if (c.TryGotoNext(i => i.MatchLdarg(0), i => i.MatchLdfld(typeof(Liquid), "y"), i => i.MatchCall(typeof(Main), "get_UnderworldLayer")))
				{
					c.Emit(Ldsfld, current);
					var skip = c.DefineLabel();
					c.Emit(Brfalse, skip);

					c.Emit(Ldsfld, current);
					c.Emit(Callvirt, normalUpdates);
					c.Emit(Brfalse, (ILLabel)c.Instrs[c.Index + 3].Operand);

					c.MarkLabel(skip);
				}
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
		}

		public static bool DenyRead(MessageBuffer buffer, int start, int length)
		{
			if (buffer.readBuffer[start + 2] == 250 && (ModNet.NetModCount < 256 ? buffer.readBuffer[start + 3] : BitConverter.ToUInt16(buffer.readBuffer, start + 3)) == ModContent.GetInstance<SubworldLibrary>().NetID)
			{
				return false;
			}
			if (!SubworldSystem.playerLocations.TryGetValue(Netplay.Clients[buffer.whoAmI].Socket.GetRemoteAddress(), out int id))
			{
				return false;
			}

			Netplay.Clients[buffer.whoAmI].TimeOutTimer = 0;

			byte[] packet = new byte[length + 1];
			packet[0] = (byte)buffer.whoAmI;
			Buffer.BlockCopy(buffer.readBuffer, start, packet, 1, length);
			SubworldSystem.SendToPipe(SubworldSystem.pipes[id], packet);

			return packet[3] != 2 && (packet[3] != 82 || BitConverter.ToUInt16(packet, 4) != NetManager.Instance.GetId<NetBestiaryModule>());
		}

		public static bool DenySend(ISocket socket, byte[] data, int start, int length, ref object state)
		{
			if (Thread.CurrentThread.Name == "Subserver Packet Thread")
			{
				return false;
			}
			return SubworldSystem.playerLocations.ContainsKey(socket.GetRemoteAddress());
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

							SubworldSystem.StartSubserver(id);

							SubworldSystem.playerLocations[address] = id;
						}
					}
					else
					{
						Task.Factory.StartNew(SubworldSystem.ExitWorldCallBack, SubworldSystem.subworlds[reader.ReadUInt16()]);
					}
					break;
				case 1:
					if (Main.netMode == 2)
					{
						RemoteAddress address = Netplay.Clients[whoAmI].Socket.GetRemoteAddress();
						if (address != null)
						{
							byte[] data = new byte[7] { (byte)whoAmI, 6, 0, 250, (byte)NetID, 2, (byte)whoAmI }; // client, (ushort) size, packet id, sublib net id
							SubworldSystem.SendToPipe(SubworldSystem.pipes[SubworldSystem.playerLocations[address]], data);

							SubworldSystem.playerLocations.Remove(address);
							Netplay.Clients[whoAmI].State = 0;

							ModPacket packet = GetPacket();
							packet.Write((byte)1);
							packet.Send(whoAmI);
						}
					}
					else
					{
						Task.Factory.StartNew(SubworldSystem.ExitWorldCallBack, null);
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
			ModTypeLookup<Subworld>.Register(this);
			SubworldSystem.subworlds.Add(this);
		}
		public sealed override void SetupContent() => SetStaticDefaults();

		/// <summary>
		/// The subworld's width.
		/// </summary>
		public abstract int Width { get; }
		/// <summary>
		/// The subworld's height.
		/// </summary>
		public abstract int Height { get; }
		/// <summary>
		/// The subworld's generation tasks.
		/// </summary>
		public abstract List<GenPass> Tasks { get; }
		public virtual WorldGenConfiguration Config => null;
		/// <summary>
		/// Whether the subworld should save or not.
		/// <br/>Default: false
		/// </summary>
		public virtual bool ShouldSave => false;
		/// <summary>
		/// Reverts changes to players when they leave the subworld.
		/// <br/>Default: false
		/// </summary>
		public virtual bool NoPlayerSaving => false;
		/// <summary>
		/// Completely disables vanilla world updating in the subworld.
		/// <br/>Do not enable unless you are replicating a standard world!
		/// <br/>Default: false
		/// </summary>
		public virtual bool NormalUpdates => false;
		/// <summary>
		/// Called when entering a subworld.
		/// <br/>Before this is called, the return button and underworld's visibility are reset.
		/// </summary>
		public virtual void OnEnter() { }
		/// <summary>
		/// Called when exiting a subworld.
		/// <br/>After this is called, the return button and underworld's visibility are reset.
		/// </summary>
		public virtual void OnExit() { }
		/// <summary>
		/// Called on all subworlds before <see cref="OnEnter"/>, and after <see cref="OnExit"/>.
		/// <br/>This is where you copy data from the main world to the subworld, via <see cref="SubworldSystem.CopyWorldData"/>.
		/// <code>SubworldSystem.CopyWorldData(nameof(DownedSystem.downedBoss), DownedSystem.downedBoss);</code>
		/// </summary>
		public virtual void CopyMainWorldData() { }
		/// <summary>
		/// Called before <see cref="OnExit"/>.
		/// <br/>This is where you copy data from the subworld to another world, via <see cref="SubworldSystem.CopyWorldData"/>.
		/// <code>SubworldSystem.CopyWorldData(nameof(DownedSystem.downedBoss), DownedSystem.downedBoss);</code>
		/// </summary>
		public virtual void CopySubworldData() { }
		/// <summary>
		/// Called on all subworlds before one generates, or after one loads from file.
		/// <br/>This is where you read data copied from the main world to the subworld, via <see cref="SubworldSystem.ReadCopiedWorldData"/>.
		/// <code>DownedSystem.downedBoss = SubworldSystem.ReadCopiedWorldData&lt;bool&gt;(nameof(DownedSystem.downedBoss));</code>
		/// </summary>
		public virtual void ReadCopiedMainWorldData() { }
		/// <summary>
		/// Called while leaving the subworld, either before a different world generates, or after a different world loads from file.
		/// <br/>This is where you read data copied from the subworld to another world, via <see cref="SubworldSystem.ReadCopiedWorldData"/>.
		/// <code>DownedSystem.downedBoss = SubworldSystem.ReadCopiedWorldData&lt;bool&gt;(nameof(DownedSystem.downedBoss));</code>
		/// </summary>
		public virtual void ReadCopiedSubworldData() { }
		/// <summary>
		/// Called after the subworld generates or loads from file.
		/// </summary>
		public virtual void OnLoad() { }
		/// <summary>
		/// Called while leaving the subworld, before a different world either generates or loads from file.
		/// </summary>
		public virtual void OnUnload() { }
		/// <summary>
		/// Requires knowledge of how vanilla world file loading works to use properly! Only override this if you know what you are doing.
		/// </summary>
		/// <returns>The exit status. A number above 0 indicates that world file reading has failed.</returns>
		public virtual int ReadFile(BinaryReader reader)
		{
			int status = WorldFile.LoadWorld_Version2(reader);
			Main.ActiveWorldFileData.Name = Main.worldName;
			Main.ActiveWorldFileData.Metadata = Main.WorldFileMetadata;
			return status;
		}
		/// <summary>
		/// Requires knowledge of how vanilla world file loading works to use properly! Only override this if you know what you are doing.
		/// </summary>
		public virtual void PostReadFile()
		{
			SubworldSystem.PostLoadWorldFile();
		}
		/// <summary>
		/// Corrects zoom and clears the screen, then calls DrawMenu and draws the cursor.
		/// <code>
		/// PlayerInput.SetZoom_Unscaled();
		/// Main.instance.GraphicsDevice.Clear(Color.Black);
		/// Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, Main.Rasterizer, null, Main.UIScaleMatrix);
		/// DrawMenu(gameTime);
		/// Main.DrawCursor(Main.DrawThickCursor());
		/// Main.spriteBatch.End();
		/// </code>
		/// </summary>
		public virtual void DrawSetup(GameTime gameTime)
		{
			PlayerInput.SetZoom_Unscaled();

			Main.instance.GraphicsDevice.Clear(Color.Black);

			Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, Main.Rasterizer, null, Main.UIScaleMatrix);

			DrawMenu(gameTime);
			Main.DrawCursor(Main.DrawThickCursor());

			Main.spriteBatch.End();
		}
		/// <summary>
		/// Called by DrawSetup to draw the subworld's loading menu.
		/// <br/>Defaults to text on a black background.
		/// </summary>
		public virtual void DrawMenu(GameTime gameTime)
		{
			Main.spriteBatch.DrawString(FontAssets.DeathText.Value, Main.statusText, new Vector2(Main.screenWidth, Main.screenHeight) / 2 - FontAssets.DeathText.Value.MeasureString(Main.statusText) / 2, Color.White);
		}
		/// <summary>
		/// Controls how a tile in the subworld is lit.
		/// <br/>Return true to disable vanilla behaviour.
		/// <br/>Default: false
		/// </summary>
		public virtual bool GetLight(Tile tile, int x, int y, ref FastRandom rand, ref Vector3 color) => false;
	}

	internal class SubserverSocket : ISocket
	{
		private readonly int index;

		internal static NamedPipeClientStream pipe;

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
			SubworldSystem.SendToPipe(pipe, packet);
		}

		void ISocket.Close() { }

		void ISocket.Connect(RemoteAddress address) { }

		RemoteAddress ISocket.GetRemoteAddress() => new TcpAddress(IPAddress.Any, 0);

		bool ISocket.IsConnected() => true;

		bool ISocket.IsDataAvailable() => true;

		void ISocket.SendQueuedPackets() { }

		bool ISocket.StartListening(SocketConnectionAccepted callback) => true;

		void ISocket.StopListening() { }
	}

	public class SubworldSystem : ModSystem
	{
		internal static List<Subworld> subworlds;

		internal static Subworld current;
		internal static Subworld cache;
		internal static WorldFileData main;

		internal static TagCompound copiedData;

		internal static Dictionary<RemoteAddress, int> playerLocations;
		internal static Dictionary<int, NamedPipeClientStream> pipes;

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

		public override void OnModLoad()
		{
			subworlds = new List<Subworld>();
			Player.Hooks.OnEnterWorld += OnEnterWorld;
			Netplay.OnDisconnect += OnDisconnect;
		}

		public override void Unload()
		{
			Player.Hooks.OnEnterWorld -= OnEnterWorld;
			Netplay.OnDisconnect -= OnDisconnect;
		}

		/// <summary>
		/// The current subworld.
		/// </summary>
		public static Subworld Current => current;
		/// <summary>
		/// Returns true if the current subworld's ID matches the specified ID.
		/// </summary>
		public static bool IsActive(string id) => current?.FullName == id;
		/// <summary>
		/// Returns true if the specified subworld is active.
		/// </summary>
		public static bool IsActive<T>() where T : Subworld => current?.GetType() == typeof(T);
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
		public static string CurrentPath => Path.Combine(Main.WorldPath, Path.GetFileNameWithoutExtension(main.Path), current.Mod.Name + "_" + current.Name + ".wld");

		/// <summary>
		/// Tries to enter a subworld with the specified ID.
		/// </summary>
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

		/// <summary>
		/// Enters the specified subworld.
		/// </summary>
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

		/// <summary>
		/// Exits the current subworld.
		/// </summary>
		public static void Exit()
		{
			if (current != null && current == cache)
			{
				if (Main.netMode == 0)
				{
					Task.Factory.StartNew(ExitWorldCallBack, null);
				}
				else if (Main.netMode == 1)
				{
					ModPacket packet = ModContent.GetInstance<SubworldLibrary>().GetPacket();
					packet.Write((byte)1);
					packet.Send();
				}
			}
		}

		/// <summary>
		/// Can only be called in <see cref="Subworld.CopyMainWorldData"/> or <see cref="Subworld.OnExit"/>!
		/// <br/>Stores data to be transferred between worlds under the specified key, if that key is not already in use.
		/// <br/>Naming the key after the variable pointing to the data is highly recommended to avoid redundant copying. This can be done automatically with nameof().
		/// <code>SubworldSystem.CopyWorldData(nameof(DownedSystem.downedBoss), DownedSystem.downedBoss);</code>
		/// </summary>
		public static void CopyWorldData(string key, object data)
		{
			if (data != null && !copiedData.ContainsKey(key))
			{
				copiedData[key] = data;
			}
		}

		/// <summary>
		/// Can only be called in <see cref="Subworld.ReadCopiedMainWorldData"/> or <see cref="Subworld.ReadCopiedSubworldData"/>!
		/// <br/>Reads data copied from another world stored under the specified key.
		/// <code>DownedSystem.downedBoss = SubworldSystem.ReadCopiedWorldData&lt;bool&gt;(nameof(DownedSystem.downedBoss));</code>
		/// </summary>
		public static T ReadCopiedWorldData<T>(string key) => copiedData.Get<T>(key);

		/// <summary>
		/// Sends a packet from the specified mod directly to the main server.
		/// </summary>
		public static void SendToMainServer(Mod mod, byte[] data)
		{
			int extra = ModNet.NetModCount < 256 ? 6 : 8;
			byte[] packet = new byte[data.Length + extra];

			int i = 0;
			packet[i++] = 0;

			packet[i++] = (byte)packet.Length;
			packet[i++] = (byte)(packet.Length >> 8);

			packet[i++] = 250;

			short netID = ModContent.GetInstance<SubworldLibrary>().NetID;

			packet[i++] = (byte)netID;
			if (ModNet.NetModCount >= 256)
			{
				packet[i++] = (byte)(netID >> 8);
				packet[i + 1] = (byte)(mod.NetID >> 8);
			}

			packet[i++] = (byte)mod.NetID;

			Buffer.BlockCopy(data, 0, packet, extra, data.Length);
			SendToPipe(SubserverSocket.pipe, packet);
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

		private static void BeginEntering(int index)
		{
			if (Main.netMode == 0)
			{
				if (current == null)
				{
					main = Main.ActiveWorldFileData;
				}
				Task.Factory.StartNew(ExitWorldCallBack, subworlds[index]);
			}
			else if (Main.netMode == 1)
			{
				ModPacket packet = ModContent.GetInstance<SubworldLibrary>().GetPacket();
				packet.Write((byte)0);
				packet.Write((ushort)index);
				packet.Send();
			}
		}

		private static void CopyMainWorldData()
		{
			copiedData["seed"] = Main.ActiveWorldFileData.SeedText;
			copiedData["gameMode"] = Main.ActiveWorldFileData.GameMode;
			copiedData["hardMode"] = Main.hardMode;

			using (MemoryStream bestiary = new MemoryStream())
			{
				using BinaryWriter writer = new BinaryWriter(bestiary);
				Main.BestiaryTracker.Save(writer);
				copiedData["bestiary"] = bestiary.GetBuffer();
			}

			copiedData[nameof(Main.drunkWorld)] = Main.drunkWorld;
			copiedData[nameof(Main.getGoodWorld)] = Main.getGoodWorld;
			copiedData[nameof(Main.tenthAnniversaryWorld)] = Main.tenthAnniversaryWorld;
			copiedData[nameof(Main.dontStarveWorld)] = Main.dontStarveWorld;
			copiedData[nameof(Main.notTheBeesWorld)] = Main.notTheBeesWorld;

			CopyDowned();

			foreach (Subworld subworld in subworlds)
			{
				subworld.CopyMainWorldData();
			}
		}

		private static void ReadCopiedMainWorldData()
		{
			Main.ActiveWorldFileData.SetSeed(copiedData.Get<string>("seed"));
			Main.GameMode = copiedData.Get<int>("gameMode");
			Main.hardMode = copiedData.Get<bool>("hardMode");

			using (MemoryStream bestiary = new MemoryStream(copiedData.Get<byte[]>("bestiary")))
			{
				using BinaryReader reader = new BinaryReader(bestiary);
				Main.BestiaryTracker.Load(reader, 248);
			}

			Main.drunkWorld = copiedData.Get<bool>(nameof(Main.drunkWorld));
			Main.getGoodWorld = copiedData.Get<bool>(nameof(Main.getGoodWorld));
			Main.tenthAnniversaryWorld = copiedData.Get<bool>(nameof(Main.tenthAnniversaryWorld));
			Main.dontStarveWorld = copiedData.Get<bool>(nameof(Main.dontStarveWorld));
			Main.notTheBeesWorld = copiedData.Get<bool>(nameof(Main.notTheBeesWorld));

			ReadCopiedDowned();

			foreach (Subworld subworld in subworlds)
			{
				subworld.ReadCopiedMainWorldData();
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

		internal static void SendToPipe(NamedPipeClientStream pipe, byte[] data)
		{
			Task.Run(() =>
			{
				if (!pipe.IsConnected)
				{
					pipe.Connect(5000);
				}
				pipe.Write(data);
			});
		}

		public static void StartSubserver(int id)
		{
			if (pipes.ContainsKey(id))
			{
				return;
			}

			Process p = new Process();
			p.StartInfo.FileName = Process.GetCurrentProcess().MainModule!.FileName;
			p.StartInfo.Arguments = "tModLoader.dll -server -showserverconsole -world \"" + Main.worldPathName + "\" -subworld \"" + subworlds[id].FullName + "\"";
			p.StartInfo.UseShellExecute = true;
			p.Start();

			pipes[id] = new NamedPipeClientStream(".", subworlds[id].FullName + "/IN", PipeDirection.Out);
			new Thread(MainServerCallBack)
			{
				Name = "Subserver Packet Thread",
				IsBackground = true
			}.Start(id);

			copiedData = new TagCompound();
			CopyMainWorldData();

			using MemoryStream stream = new MemoryStream();
			TagIO.ToStream(copiedData, stream);
			pipes[id].Connect(15000);
			pipes[id].Write(stream.ToArray());

			copiedData = null;
		}

		private static void MainServerCallBack(object id)
		{
			NamedPipeServerStream pipe = new NamedPipeServerStream(subworlds[(int)id].FullName + "/OUT", PipeDirection.In, -1);
			try
			{
				pipe.WaitForConnection();

				while (pipe.IsConnected)
				{
					byte[] packetInfo = new byte[3];
					if (pipe.Read(packetInfo) < 3)
					{
						break;
					}

					byte low = packetInfo[1];
					byte high = packetInfo[2];
					int length = (high << 8) | low;

					byte[] data = new byte[length];
					pipe.Read(data, 2, length - 2);
					data[0] = low;
					data[1] = high;

					if (data[2] == 250 && (ModNet.NetModCount < 256 ? data[3] : BitConverter.ToUInt16(data, 3)) == ModContent.GetInstance<SubworldLibrary>().NetID)
					{
						MemoryStream stream = new MemoryStream(data);
						using BinaryReader reader = new BinaryReader(stream);
						if (ModNet.NetModCount < 256)
						{
							stream.Position = 5;
							ModNet.GetMod(data[4]).HandlePacket(reader, 256);
						}
						else
						{
							stream.Position = 6;
							ModNet.GetMod(BitConverter.ToUInt16(data, 5)).HandlePacket(reader, 256);
						}
					}
					else
					{
						Netplay.Clients[packetInfo[0]].Socket.AsyncSend(data, 0, length, (state) => { });
					}
				}
			}
			finally
			{
				pipe.Close();
				pipes[(int)id].Close();
				pipes.Remove((int)id);
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

						NamedPipeServerStream pipe = new NamedPipeServerStream(current.FullName + "/IN", PipeDirection.In, -1);
						pipe.WaitForConnection();

						copiedData = TagIO.FromStream(pipe);
						LoadWorld();
						copiedData = null;

						for (int j = 0; j < Netplay.Clients.Length; j++)
						{
							Netplay.Clients[j].Id = j;
							Netplay.Clients[j].Reset();
							Netplay.Clients[j].ReadBuffer = null; // not used by subservers, saves 262kb total
						}

						SubserverSocket.pipe = new NamedPipeClientStream(".", current.FullName + "/OUT", PipeDirection.Out);
						new Thread(SubserverCallBack)
						{
							IsBackground = true
						}.Start(pipe);

						return true;
					}
				}

				Main.instance.Exit();
				return true;
			}

			playerLocations = new Dictionary<RemoteAddress, int>();
			pipes = new Dictionary<int, NamedPipeClientStream>();

			return false;
		}

		private static void SubserverCallBack(object pipeObject)
		{
			NamedPipeServerStream pipe = (NamedPipeServerStream)pipeObject;
			try
			{
				while (!Netplay.Disconnect)
				{
					byte[] packetInfo = new byte[3];
					if (pipe.Read(packetInfo) < 3)
					{
						break;
					}

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
						pipe.Read(buffer.readBuffer, buffer.totalData + 2, length - 2);

						if (buffer.readBuffer[buffer.totalData + 2] == 1)
						{
							Netplay.Clients[buffer.whoAmI].Socket = new SubserverSocket(buffer.whoAmI);
							Netplay.Clients[buffer.whoAmI].IsActive = true;
							Netplay.HasClients = true;
						}

						buffer.totalData += length;
						buffer.checkBytes = true;
					}
				}
			}
			finally
			{
				pipe.Close();
				SubserverSocket.pipe.Close();
			}
		}

		internal static void ExitWorldCallBack(object subworldObject)
		{
			// presumably avoids a race condition?
			int netMode = Main.netMode;

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
				if (subworldObject != null)
				{
					CopyMainWorldData();
				}
			}
			else
			{
				Netplay.Connection.State = 1;
				cache?.OnExit();
			}

			current = (Subworld)subworldObject;

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

			if (netMode != 1)
			{
				WorldFile.SaveWorld();
			}
			SystemLoader.OnWorldUnload();

			Main.fastForwardTime = false;
			Main.UpdateTimeRate();
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

			cache?.OnUnload();

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
				WorldFile.SetOngoingToTemps();
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
			Main.worldName = Language.GetTextValue("Mods." + current.Mod.Name + ".SubworldName." + current.Name);
			if (Main.netMode == 2)
			{
				Console.Title = Main.worldName;
			}
			WorldFileData data = new WorldFileData(path, cloud)
			{
				Name = Main.worldName,
				GameMode = Main.GameMode,
				CreationTime = DateTime.Now,
				Metadata = FileMetadata.FromCurrentSettings(FileType.World),
				WorldGeneratorVersion = Main.WorldGeneratorVersion,
				UniqueId = Guid.NewGuid()
			};
			data.SetSeed(main.SeedText);
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
			Main.weatherCounter = 18000;
			Cloud.resetClouds();

			ReadCopiedMainWorldData();

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