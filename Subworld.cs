using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.IO;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.Utilities;
using Terraria.WorldBuilding;

namespace SubworldLibrary
{
	public interface ICopyWorldData : ILoadable
	{
		/// <summary>
		/// Called on all content with this interface before <see cref="Subworld.OnEnter"/>, and after <see cref="Subworld.OnExit"/>.
		/// <br/>This is where you copy data from the main world to a subworld, via <see cref="SubworldSystem.CopyWorldData"/>.
		/// <code>SubworldSystem.CopyWorldData(nameof(DownedSystem.downedBoss), DownedSystem.downedBoss);</code>
		/// </summary>
		void CopyMainWorldData() { }

		/// <summary>
		/// Called on all content with this interface before a subworld generates, or after a subworld loads from file.
		/// <br/>This is where you read data copied from the main world to a subworld, via <see cref="SubworldSystem.ReadCopiedWorldData"/>.
		/// <code>DownedSystem.downedBoss = SubworldSystem.ReadCopiedWorldData&lt;bool&gt;(nameof(DownedSystem.downedBoss));</code>
		/// </summary>
		void ReadCopiedMainWorldData() { }
	}

	public abstract class Subworld : ModType, ICopyWorldData, ILocalizedModType
	{
		public string LocalizationCategory => "Subworlds";
		public virtual LocalizedText DisplayName => this.GetLocalization(nameof(DisplayName), PrettyPrintName);

		protected sealed override void Register()
		{
			ModTypeLookup<Subworld>.Register(this);
			SubworldSystem.subworlds.Add(this);
		}
		public sealed override void SetupContent() => SetStaticDefaults();

		public string FileName => Mod.Name + "_" + Name;

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
		/// The index of the subworld the player will be sent to when choosing to return. See <see cref="SubworldSystem.GetIndex{T}"/>.
		/// <br/>Set to -1 to send the player back to the main world.
		/// <br/>Set to <see cref="int.MinValue"/> to send the player to the main menu.
		/// <br/>Default: -1
		/// </summary>
		public virtual int ReturnDestination => -1;
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
		/// If <see cref="ChangeAudio"/> returns true, this completely disables vanilla audio updating.
		/// <br/>Typically not required. Only enable this if you know what you are doing.
		/// <br/>Default: false
		/// </summary>
		public virtual bool ManualAudioUpdates => false;
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
		/// Called after <see cref="ModSystem.PreUpdateWorld"/>, and before <see cref="ModSystem.PostUpdateWorld"/>.
		/// <br/>This can be used to make things happen in the subworld.
		/// </summary>
		public virtual void Update() { }
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
		/// PlayerInput.SetZoom_UI();
		/// Main.instance.GraphicsDevice.Clear(Color.Black);
		/// Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, Main.Rasterizer, null, Main.UIScaleMatrix);
		/// DrawMenu(gameTime);
		/// Main.DrawCursor(Main.DrawThickCursor());
		/// Main.spriteBatch.End();
		/// </code>
		/// </summary>
		public virtual void DrawSetup(GameTime gameTime)
		{
			PlayerInput.SetZoom_UI();

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
		/// Called before music is chosen, including in the loading menu.
		/// <br/>Return true to disable vanilla behaviour, allowing for modification of variables such as <see cref="Main.newMusic"/>.
		/// <br/>Default: false
		/// </summary>
		public virtual bool ChangeAudio() => false;
		/// <summary>
		/// Controls the gravity of an entity in the subworld.
		/// <br/>Default: 1
		/// </summary>
		public virtual float GetGravity(Entity entity) => 1;
		/// <summary>
		/// Controls how a tile in the subworld is lit.
		/// <br/>Return true to disable vanilla behaviour.
		/// <br/>Default: false
		/// </summary>
		public virtual bool GetLight(Tile tile, int x, int y, ref FastRandom rand, ref Vector3 color) => false;
	}
}