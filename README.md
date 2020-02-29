# SubworldLibrary
Dimensions, made easy.

Subworld Library allows you to easily add dimensions (referred to as subworlds), handling most of the hard work, including loading, saving, and Multiplayer compatibility.

What it does, in more detail:

- Registering

Any class that derives from the Subworld class is automatically registered as a subworld. Subworlds are highly customisable; from their size and how they generate, to whether they or the players inside of them save, to even what ModWorld is allowed to update while inside of them.

- Loading

Loading a subworld is as simple as calling "SLWorld.EnterSubworld". Each subworld can have a custom loading UI if its class overrides "loadingUIState". The UI can even persist after the world is loaded if "loadingUI" is overridden.

- Saving

Subworlds can be set to be either temporary or permanent. Subworlds can also prevent players from saving while inside them. Subworld Library also handles deleting subworlds when their main world is deleted.

- Multiplayer

Subworld Library doesn't just handle loading, saving, etc. in Singleplayer; it also does so in Multiplayer, with little to no extra work required on your end.

- Misc.

Subworld Library gets rid of Space, the Underworld, and both Oceans inside subworlds, freeing up tons of world space, and allowing for very small subworlds.

If you need help with making a subworld, or encounter a bug, please go to the #sublib_help and #sublib_bugs channels in the GaMeterraria Discord respectively.

https://discord.gg/zumQztb
