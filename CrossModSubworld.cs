using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.WorldBuilding;

namespace SubworldLibrary
{
	internal class CrossModSubworld : Subworld
	{
		public override string Name { get; }
		public override int Width { get; }
		public override int Height { get; }
		public override List<GenPass> Tasks { get; }
		public override WorldGenConfiguration Config { get; }
		public override int ReturnDestination { get; }
		public override bool ShouldSave { get; }
		public override bool NoPlayerSaving { get; }
		public override bool NormalUpdates { get; }
		public override bool ManualAudioUpdates { get; }

		private Action _OnEnter;
		public override void OnEnter() => _OnEnter();
		private Action _OnExit;
		public override void OnExit() => _OnExit();
		private Action _Update;
		public override void Update() => _Update();
		private Action _CopyMainWorldData;
		public override void CopyMainWorldData() => _CopyMainWorldData();
		private Action _CopySubworldData;
		public override void CopySubworldData() => _CopySubworldData();
		private Action _ReadCopiedMainWorldData;
		public override void ReadCopiedMainWorldData() => _ReadCopiedMainWorldData();
		private Action _ReadCopiedSubworldData;
		public override void ReadCopiedSubworldData() => _ReadCopiedSubworldData();
		private Action _OnLoad;
		public override void OnLoad() => _OnLoad();
		private Action _OnUnload;
		public override void OnUnload() => _OnUnload();
		private Action<GameTime> _DrawMenu;
		public override void DrawMenu(GameTime gameTime) => _DrawMenu(gameTime);
		private Func<bool> _ChangeAudio;
		public override bool ChangeAudio() => _ChangeAudio();
		private Func<Entity, float> _GetGravity;
		public override float GetGravity(Entity entity) => _GetGravity(entity);

		public CrossModSubworld(string name, int width, int height, List<GenPass> tasks, WorldGenConfiguration config, int returnDestination, bool shouldSave, bool noPlayerSaving, bool normalUpdates, bool manualAudioUpdates, Action onEnter, Action onExit, Action update, Action copyMainWorldData, Action copySubworldData, Action readCopiedMainWorldData, Action readCopiedSubworldData, Action onLoad, Action onUnload, Action<GameTime> drawMenu, Func<bool> changeAudio, Func<Entity, float> getGravity)
		{
			Name = name;
			Width = width;
			Height = height;
			Tasks = tasks;
			Config = config;
			ReturnDestination = returnDestination;
			ShouldSave = shouldSave;
			NoPlayerSaving = noPlayerSaving;
			NormalUpdates = normalUpdates;
			ManualAudioUpdates = manualAudioUpdates;
			_OnEnter = onEnter ?? base.OnEnter;
			_OnExit = onExit ?? base.OnExit;
			_Update = update ?? base.Update;
			_CopyMainWorldData = copyMainWorldData ?? base.CopyMainWorldData;
			_CopySubworldData = copySubworldData ?? base.CopySubworldData;
			_ReadCopiedMainWorldData = readCopiedMainWorldData ?? base.ReadCopiedMainWorldData;
			_ReadCopiedSubworldData = readCopiedSubworldData ?? base.ReadCopiedSubworldData;
			_OnLoad = onLoad ?? base.OnLoad;
			_OnUnload = onUnload ?? base.OnUnload;
			_DrawMenu = drawMenu ?? base.DrawMenu;
			_ChangeAudio = changeAudio ?? base.ChangeAudio;
			_GetGravity = getGravity ?? base.GetGravity;
		}
	}
}