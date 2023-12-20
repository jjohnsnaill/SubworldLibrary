using Microsoft.Xna.Framework;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Terraria;
using Terraria.Chat;
using Terraria.GameContent.NetModules;
using Terraria.Graphics.Light;
using Terraria.IO;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.Net;
using Terraria.Net.Sockets;
using Terraria.Utilities;
using static Mono.Cecil.Cil.OpCodes;

namespace SubworldLibrary
{
	public class SubworldLibrary : Mod
	{
		private static ILHook tcpSocketHook;
		private static ILHook socialSocketHook;

		public override void Load()
		{
			FieldInfo current = typeof(SubworldSystem).GetField("current", BindingFlags.NonPublic | BindingFlags.Static);
			FieldInfo cache = typeof(SubworldSystem).GetField("cache", BindingFlags.NonPublic | BindingFlags.Static);
			FieldInfo hideUnderworld = typeof(SubworldSystem).GetField("hideUnderworld");
			MethodInfo normalUpdates = typeof(Subworld).GetMethod("get_NormalUpdates");
			MethodInfo shouldSave = typeof(Subworld).GetMethod("get_ShouldSave");

			if (Main.dedServ)
			{
				IL_Main.DedServ_PostModLoad += il =>
				{
					ConstructorInfo gameTime = typeof(GameTime).GetConstructor(Type.EmptyTypes);
					MethodInfo update = typeof(Main).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance);
					FieldInfo saveTime = typeof(Main).GetField("saveTime", BindingFlags.NonPublic | BindingFlags.Static);

					var c = new ILCursor(il);
					ILLabel skip = null;
					if (!c.TryGotoNext(i => i.MatchBr(out skip)))
					{
						Logger.Error("FAILED:");
						return;
					}

					c.Emit(OpCodes.Call, typeof(SubworldSystem).GetMethod("LoadIntoSubworld", BindingFlags.NonPublic | BindingFlags.Static));
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

					c.Emit(OpCodes.Call, typeof(Main).Assembly.GetType("Terraria.ModLoader.Engine.ServerHangWatchdog").GetMethod("Checkin", BindingFlags.NonPublic | BindingFlags.Static));

					c.Emit(OpCodes.Call, typeof(SubworldLibrary).GetMethod("CheckClients", BindingFlags.NonPublic | BindingFlags.Static));

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
					c.Emit(OpCodes.Call, typeof(SubworldLibrary).GetMethod("Sleep", BindingFlags.NonPublic | BindingFlags.Static));

					c.MarkLabel(loopStart);

					c.Emit(Ldsfld, typeof(Netplay).GetField("Disconnect"));
					c.Emit(Brfalse, loop);

					c.Emit(Ldsfld, current);
					c.Emit(Callvirt, shouldSave);
					label = c.DefineLabel();
					c.Emit(Brfalse, label);
					c.Emit(OpCodes.Call, typeof(WorldFile).GetMethod("SaveWorld", Type.EmptyTypes));
					c.MarkLabel(label);

					c.Emit(OpCodes.Call, typeof(SystemLoader).GetMethod("OnWorldUnload"));

					c.Emit(Ret);
				};

				if (!Program.LaunchParameters.ContainsKey("-subworld"))
				{
					IL_NetMessage.CheckBytes += il =>
					{
						ILCursor c, cc;
						if (!(c = new ILCursor(il)).TryGotoNext(MoveType.After, i => i.MatchCall(typeof(BitConverter), "ToUInt16"))
						|| !c.Instrs[c.Index].MatchStloc(out int index)
						|| !c.TryGotoNext(MoveType.After, i => i.MatchCallvirt(typeof(Stream), "get_Position"), i => i.MatchStloc(out _))
						|| !(cc = c.Clone()).TryGotoNext(i => i.MatchLdsfld(typeof(NetMessage), "buffer"), i => i.MatchLdarg(0), i => i.MatchLdelemRef(), i => i.MatchLdfld(typeof(MessageBuffer), "reader")))
						{
							Logger.Error("FAILED:");
							return;
						}

						c.Emit(Ldsfld, typeof(NetMessage).GetField("buffer"));
						c.Emit(Ldarg_0);
						c.Emit(Ldelem_Ref);
						c.Emit(Ldloc_2);
						c.Emit(Ldloc, index);
						c.Emit(OpCodes.Call, typeof(SubworldLibrary).GetMethod("DenyRead", BindingFlags.NonPublic | BindingFlags.Static));

						var label = c.DefineLabel();
						c.Emit(Brtrue, label);

						cc.MarkLabel(label);
					};

					socialSocketHook = new ILHook(typeof(SocialSocket).GetMethod("Terraria.Net.Sockets.ISocket.AsyncSend", BindingFlags.NonPublic | BindingFlags.Instance), AsyncSend);
					tcpSocketHook = new ILHook(typeof(TcpSocket).GetMethod("Terraria.Net.Sockets.ISocket.AsyncSend", BindingFlags.NonPublic | BindingFlags.Instance), AsyncSend);

					void AsyncSend(ILContext il)
					{
						var c = new ILCursor(il);
						if (!c.TryGotoNext(MoveType.After, i => i.MatchRet()))
						{
							Logger.Error("FAILED:");
							return;
						}
						c.MoveAfterLabels();

						c.Emit(Ldarg_0);
						c.Emit(Ldarg_1);
						c.Emit(Ldarg_2);
						c.Emit(Ldarg_3);
						c.Emit(Ldarga, 5);
						c.Emit(OpCodes.Call, typeof(SubworldLibrary).GetMethod("DenySend", BindingFlags.NonPublic | BindingFlags.Static));
						var label = c.DefineLabel();
						c.Emit(Brfalse, label);
						c.Emit(Ret);
						c.MarkLabel(label);
					}

					IL_Netplay.UpdateConnectedClients += il =>
					{
						var c = new ILCursor(il);
						if (!c.TryGotoNext(MoveType.After, i => i.MatchCallvirt(typeof(RemoteClient), "Reset"))
						|| !c.Instrs[c.Index].MatchLdloc(out int index))
						{
							Logger.Error("FAILED:");
							return;
						}

						c.Emit(Ldloc, index);
						c.Emit(OpCodes.Call, typeof(SubworldSystem).GetMethod("SyncDisconnect", BindingFlags.NonPublic | BindingFlags.Static));
					};
				}
			}
			else
			{
				IL_Main.DoDraw += il =>
				{
					var c = new ILCursor(il);
					if (!c.TryGotoNext(MoveType.After, i => i.MatchStsfld(typeof(Main), "HoverItem")))
					{
						Logger.Error("FAILED:");
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
					c.Emit(Stsfld, typeof(Main).GetField("_uiScaleUsed", BindingFlags.NonPublic | BindingFlags.Static));
					c.Emit(OpCodes.Call, typeof(Matrix).GetMethod("CreateScale", new Type[] { typeof(float) }));
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
					c.Emit(Stsfld, typeof(Main).GetField("_uiScaleUsed", BindingFlags.NonPublic | BindingFlags.Static));
					c.Emit(OpCodes.Call, typeof(Matrix).GetMethod("CreateScale", new Type[] { typeof(float) }));
					c.Emit(Stsfld, typeof(Main).GetField("_uiScaleMatrix", BindingFlags.NonPublic | BindingFlags.Static));

					c.Emit(Ldsfld, cache);
					c.Emit(Ldarg_0);
					c.Emit(Callvirt, typeof(Subworld).GetMethod("DrawSetup"));
					c.Emit(Ret);

					c.MarkLabel(skip);
				};

				IL_Main.DrawBackground += il =>
				{
					ILCursor c, cc;
					if (!(c = new ILCursor(il)).TryGotoNext(i => i.MatchLdcI4(330))
					|| !(cc = c.Clone()).TryGotoNext(i => i.MatchStloc(out _), i => i.MatchLdcR4(255)))
					{
						Logger.Error("FAILED:");
						return;
					}

					c.Emit(Ldsfld, hideUnderworld);
					var skip = c.DefineLabel();
					c.Emit(Brfalse, skip);

					c.Emit(Conv_R8);
					var label = c.DefineLabel();
					c.Emit(Br, label);

					c.MarkLabel(skip);

					cc.MarkLabel(label);
				};

				IL_Main.OldDrawBackground += il =>
				{
					ILCursor c, cc;
					if (!(c = new ILCursor(il)).TryGotoNext(i => i.MatchLdcI4(230))
					|| !(cc = c.Clone()).TryGotoNext(i => i.MatchStloc(18), i => i.MatchLdcI4(0)))
					{
						Logger.Error("FAILED:");
						return;
					}

					c.Emit(Ldsfld, hideUnderworld);
					var skip = c.DefineLabel();
					c.Emit(Brfalse, skip);

					c.Emit(Conv_R8);
					var label = c.DefineLabel();
					c.Emit(Br, label);

					c.MarkLabel(skip);

					cc.MarkLabel(label);
				};

				IL_Main.UpdateAudio += il =>
				{
					ILCursor c, cc;
					if (!(c = new ILCursor(il)).TryGotoNext(MoveType.AfterLabel, i => i.MatchLdsfld(typeof(Main), "swapMusic"))
					|| !(cc = c.Clone()).TryGotoNext(MoveType.After, i => i.MatchCall(typeof(Main), "UpdateAudio_DecideOnNewMusic"))
					|| !cc.Instrs[cc.Index].MatchBr(out ILLabel label))
					{
						Logger.Error("FAILED:");
						return;
					}

					c.Emit(OpCodes.Call, typeof(SubworldSystem).GetMethod("ChangeAudio", BindingFlags.NonPublic | BindingFlags.Static));
					var skip = c.DefineLabel();
					c.Emit(Brfalse, skip);

					c.Emit(OpCodes.Call, typeof(SubworldSystem).GetMethod("ManualAudioUpdates", BindingFlags.NonPublic | BindingFlags.Static));
					c.Emit(Brfalse, label);

					var ret = c.DefineLabel();
					ret.Target = c.Instrs[c.Instrs.Count - 1];
					c.Emit(Leave, ret);

					c.MarkLabel(skip);
				};

				IL_IngameOptions.Draw += il =>
				{
					ILCursor c, cc, ccc, cccc;
					if (!(c = new ILCursor(il)).TryGotoNext(i => i.MatchLdsfld(typeof(Lang), "inter"), i => i.MatchLdcI4(35))
					|| !(cc = c.Clone()).TryGotoNext(MoveType.After, i => i.MatchCallvirt(typeof(LocalizedText), "get_Value"))
					|| !(ccc = cc.Clone()).TryGotoNext(i => i.MatchLdnull(), i => i.MatchCall(typeof(WorldGen), "SaveAndQuit"))
					|| !(cccc = ccc.Clone()).TryGotoPrev(MoveType.AfterLabel, i => i.MatchLdloc(out _), i => i.MatchLdcI4(1), i => i.MatchAdd(), i => i.MatchStloc(out _)))
					{
						Logger.Error("FAILED:");
						return;
					}

					ccc.Emit(Ldsfld, current);
					var skip = ccc.DefineLabel();
					ccc.Emit(Brfalse, skip);

					ccc.Emit(OpCodes.Call, typeof(SubworldSystem).GetMethod("Exit"));
					var label = ccc.DefineLabel();
					ccc.Emit(Br, label);

					ccc.MarkLabel(skip);

					ccc.Index += 2;
					ccc.MarkLabel(label);

					cccc.Emit(Ldsfld, typeof(SubworldSystem).GetField("noReturn"));
					cccc.Emit(Brtrue, label);

					c.Emit(Ldsfld, current);
					skip = c.DefineLabel();
					c.Emit(Brfalse, skip);

					c.Emit(Ldstr, "Mods.SubworldLibrary.Return");
					c.Emit(OpCodes.Call, typeof(Language).GetMethod("GetTextValue", new Type[] { typeof(string) }));
					label = c.DefineLabel();
					c.Emit(Br, label);

					c.MarkLabel(skip);

					cc.MarkLabel(label);
				};

				IL_TileLightScanner.GetTileLight += il =>
				{
					ILCursor c, cc, ccc;
					if (!(c = new ILCursor(il)).TryGotoNext(MoveType.After, i => i.MatchStloc(1))
					|| !(cc = c.Clone()).TryGotoNext(MoveType.AfterLabel, i => i.MatchLdarg(2), i => i.MatchCall(typeof(Main), "get_UnderworldLayer"))
					|| !(ccc = cc.Clone()).TryGotoNext(MoveType.After, i => i.MatchCall(typeof(TileLightScanner), "ApplyHellLight")))
					{
						Logger.Error("FAILED:");
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

					cc.Emit(Ldsfld, hideUnderworld);
					skip = cc.DefineLabel();
					cc.Emit(Brtrue, skip);

					ccc.MarkLabel(skip);
				};

				IL_Player.UpdateBiomes += il =>
				{
					ILCursor c, cc;
					if (!(c = new ILCursor(il)).TryGotoNext(MoveType.AfterLabel, i => i.MatchLdloc(out _), i => i.MatchLdfld(typeof(Point), "Y"), i => i.MatchLdsfld(typeof(Main), "maxTilesY"))
					|| !(cc = c.Clone()).TryGotoNext(i => i.MatchStloc(out _)))
					{
						Logger.Error("FAILED:");
						return;
					}

					c.Emit(Ldsfld, hideUnderworld);
					var skip = c.DefineLabel();
					c.Emit(Brfalse, skip);

					c.Emit(Ldc_I4_0);
					var label = c.DefineLabel();
					c.Emit(Br, label);

					c.MarkLabel(skip);

					cc.MarkLabel(label);
				};

				IL_Main.DrawUnderworldBackground += il =>
				{
					var c = new ILCursor(il);

					c.Emit(Ldsfld, hideUnderworld);
					var skip = c.DefineLabel();
					c.Emit(Brtrue, skip);
					c.Index = c.Instrs.Count - 1;
					c.MarkLabel(skip);
				};

				IL_Netplay.AddCurrentServerToRecentList += il =>
				{
					var c = new ILCursor(il);

					c.Emit(Ldsfld, current);
					var skip = c.DefineLabel();
					c.Emit(Brtrue, skip);
					c.Index = c.Instrs.Count - 1;
					c.MarkLabel(skip);
				};
			}

			IL_Main.EraseWorld += il =>
			{
				var c = new ILCursor(il);

				c.Emit(Ldarg_0);
				c.Emit(OpCodes.Call, typeof(SubworldSystem).GetMethod("EraseSubworlds", BindingFlags.NonPublic | BindingFlags.Static));
			};

			IL_Main.DoUpdateInWorld += il =>
			{
				ILCursor c, cc;
				if (!(c = new ILCursor(il)).TryGotoNext(MoveType.After, i => i.MatchCall(typeof(SystemLoader), "PreUpdateTime"))
				|| !(cc = c.Clone()).TryGotoNext(i => i.MatchCall(typeof(SystemLoader), "PostUpdateTime")))
				{
					Logger.Error("FAILED:");
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

				cc.MarkLabel(label);
			};

			IL_WorldGen.UpdateWorld += il =>
			{
				var c = new ILCursor(il);
				if (!c.TryGotoNext(i => i.MatchCall(typeof(WorldGen), "UpdateWorld_Inner")))
				{
					Logger.Error("FAILED:");
					return;
				}

				c.Emit(Ldsfld, current);
				var skip = c.DefineLabel();
				c.Emit(Brfalse, skip);

				c.Emit(Ldsfld, current);
				c.Emit(Callvirt, typeof(Subworld).GetMethod("Update"));

				c.Emit(Ldsfld, current);
				c.Emit(Callvirt, normalUpdates);
				var label = c.DefineLabel();
				c.Emit(Brfalse, label);

				c.MarkLabel(skip);

				c.Index++;
				c.MarkLabel(label);
			};

			IL_Player.Update += il =>
			{
				ILCursor c, cc;
				if (!(c = new ILCursor(il)).TryGotoNext(MoveType.AfterLabel, i => i.MatchLdsfld(out _), i => i.MatchConvR4(), i => i.MatchLdcR4(4200))
				|| !(cc = c.Clone()).TryGotoNext(MoveType.After, i => i.MatchLdarg(0), i => i.MatchLdarg(0), i => i.MatchLdfld(typeof(Player), "gravity"))
				|| !cc.Instrs[cc.Index].MatchLdloc(out int index))
				{
					Logger.Error("FAILED:");
					return;
				}

				c.Emit(Ldsfld, current);
				var skip = c.DefineLabel();
				c.Emit(Brfalse, skip);

				c.Emit(Ldsfld, current);
				c.Emit(Callvirt, normalUpdates);
				var label = c.DefineLabel();
				c.Emit(Brtrue, skip);

				c.Emit(Ldsfld, current);
				c.Emit(Ldarg_0);
				c.Emit(Callvirt, typeof(Subworld).GetMethod("GetGravity"));
				c.Emit(Stloc, index);
				c.Emit(Br, label);

				c.MarkLabel(skip);

				cc.Index -= 3;
				cc.MarkLabel(label);
			};

			IL_NPC.UpdateNPC_UpdateGravity += il =>
			{
				ILCursor c, cc;
				if (!(c = new ILCursor(il)).TryGotoNext(i => i.MatchLdsfld(out _), i => i.MatchConvR4(), i => i.MatchLdcR4(4200))
				|| !(cc = c.Clone()).TryGotoNext(MoveType.After, i => i.MatchLdarg(0), i => i.MatchLdarg(0), i => i.MatchCall(typeof(NPC), "get_gravity"))
				|| !cc.Instrs[cc.Index].MatchLdloc(out int index))
				{
					Logger.Error("FAILED:");
					return;
				}

				c.Emit(Ldsfld, current);
				var skip = c.DefineLabel();
				c.Emit(Brfalse, skip);

				c.Emit(Ldsfld, current);
				c.Emit(Callvirt, normalUpdates);
				var label = c.DefineLabel();
				c.Emit(Brtrue, skip);

				c.Emit(Ldsfld, current);
				c.Emit(Ldarg_0);
				c.Emit(Callvirt, typeof(Subworld).GetMethod("GetGravity"));
				c.Emit(Stloc, index);
				c.Emit(Br, label);

				c.MarkLabel(skip);

				cc.Index -= 3;
				cc.MarkLabel(label);
			};

			IL_Liquid.Update += il =>
			{
				var c = new ILCursor(il);
				if (!c.TryGotoNext(i => i.MatchLdarg(0), i => i.MatchLdfld(typeof(Liquid), "y"), i => i.MatchCall(typeof(Main), "get_UnderworldLayer")))
				{
					Logger.Error("FAILED:");
					return;
				}

				c.Emit(Ldsfld, current);
				var skip = c.DefineLabel();
				c.Emit(Brfalse, skip);

				c.Emit(Ldsfld, current);
				c.Emit(Callvirt, normalUpdates);
				c.Emit(Brfalse, (ILLabel)c.Instrs[c.Index + 3].Operand);

				c.MarkLabel(skip);
			};

			IL_Player.SavePlayer += il =>
			{
				ILCursor c, cc, ccc;
				if (!(c = new ILCursor(il)).TryGotoNext(i => i.MatchCall(typeof(Player), "InternalSaveMap"))
				|| !(cc = c.Clone()).TryGotoNext(MoveType.AfterLabel, i => i.MatchLdsfld(typeof(Main), "ServerSideCharacter"))
				|| !(ccc = cc.Clone()).TryGotoNext(MoveType.After, i => i.MatchCall(typeof(FileUtilities), "ProtectedInvoke")))
				{
					Logger.Error("FAILED:");
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

				cc.MarkLabel(label);

				cc.Emit(Ldsfld, cache);
				skip = cc.DefineLabel();
				cc.Emit(Brfalse, skip);

				cc.Emit(Ldsfld, cache);
				cc.Emit(Callvirt, typeof(Subworld).GetMethod("get_NoPlayerSaving"));
				label = cc.DefineLabel();
				cc.Emit(Brtrue, label);

				cc.MarkLabel(skip);

				ccc.MarkLabel(label);
			};

			IL_WorldFile.SaveWorld_bool_bool += il =>
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

		private static bool DenyRead(MessageBuffer buffer, int start, int length)
		{
			if (buffer.readBuffer[start + 2] == 250 && (ModNet.NetModCount < 256 ? buffer.readBuffer[start + 3] : BitConverter.ToUInt16(buffer.readBuffer, start + 3)) == ModContent.GetInstance<SubworldLibrary>().NetID)
			{
				return false;
			}
			if (!SubworldSystem.playerLocations.TryGetValue(Netplay.Clients[buffer.whoAmI].Socket, out int id))
			{
				return false;
			}

			SubserverLink link = SubworldSystem.links[id];
			if (link.Connecting && buffer.readBuffer[start + 2] != 1)
			{
				return false;
			}

			RemoteClient client = Netplay.Clients[buffer.whoAmI];
			if (client.State < 1 && buffer.readBuffer[start + 2] != 1)
			{
				return true; //TODO: attempt to fix packet leaks properly
			}

			client.TimeOutTimer = 0;

			byte[] packet = new byte[length + 1];
			packet[0] = (byte)buffer.whoAmI;
			Buffer.BlockCopy(buffer.readBuffer, start, packet, 1, length);
			link.Send(packet);

			if (packet[3] == 82)
			{
				ushort packetId = BitConverter.ToUInt16(packet, 4);

				if (packetId == NetManager.Instance.GetId<NetBestiaryModule>())
				{
					return false;
				}

				if (packetId == NetManager.Instance.GetId<NetTextModule>())
				{
					return false;
				}
			}
			return true;
		}

		private static bool DenySend(ISocket socket, byte[] data, int start, int length, ref object state)
		{
			return Thread.CurrentThread.Name != "Subserver Packets" && SubworldSystem.playerLocations.ContainsKey(socket);
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

		private static void CheckClients()
		{
			bool connection = false;
			for (int i = 0; i < 255; i++)
			{
				RemoteClient client = Netplay.Clients[i];
				if (client.PendingTermination)
				{
					if (client.PendingTerminationApproved)
					{
						client.Reset();

						NetMessage.SendData(14, -1, i, null, i, 0);
						ChatHelper.BroadcastChatMessage(NetworkText.FromKey(Lang.mp[20].Key, client.Name), new Color(255, 240, 20), i);
						Player.Hooks.PlayerDisconnect(i);
					}
					continue;
				}
				if (client.State > 0)
				{
					connection = true;
					break;
				}
			}

			if (!Netplay.HasClients)
			{
				Netplay.HasClients = connection;
				return;
			}

			if (!connection)
			{
				Netplay.Disconnect = true;
				Netplay.HasClients = false;
			}
		}

		public override object Call(params object[] args)
		{
			try
			{
				string message = args[0] as string;
				switch (message)
				{
					case "Enter":
						return SubworldSystem.Enter(args[1] as string);
					case "Exit":
						SubworldSystem.Exit();
						return true;
					case "Current":
						return SubworldSystem.Current;
					case "IsActive":
						return SubworldSystem.IsActive(args[1] as string);
					case "AnyActive":
						return SubworldSystem.AnyActive(args[1] as Mod);
				}
			}
			catch (Exception e)
			{
				Logger.Error("Call error: " + e.StackTrace + e.Message);
			}
			return false;
		}

		public override void HandlePacket(BinaryReader reader, int whoAmI)
		{
			if (Main.netMode == 2)
			{
				RemoteClient client;

				if (SubworldSystem.current != null)
				{
					int mod = ModNet.NetModCount < 256 ? reader.ReadByte() : reader.ReadUInt16();
					if (mod != NetID)
					{
						ModNet.GetMod(mod).HandlePacket(reader, 256);
						return;
					}

					Netplay.Clients[whoAmI].Reset();

					NetMessage.SendData(14, -1, whoAmI, null, whoAmI, 0);
					ChatHelper.BroadcastChatMessage(NetworkText.FromKey(Lang.mp[20].Key, Netplay.Clients[whoAmI].Name), new Color(255, 240, 20), whoAmI);
					Player.Hooks.PlayerDisconnect(whoAmI);

					CheckClients();
					return;
				}

				ushort id = reader.ReadUInt16();
				if (!SubworldSystem.noReturn)
				{
					SubworldSystem.MovePlayerToSubserver(whoAmI, id);
				}
			}
			else
			{
				ushort id = reader.ReadUInt16();
				Task.Factory.StartNew(SubworldSystem.ExitWorldCallBack, id < ushort.MaxValue ? id : -1);
			}
		}
	}
}