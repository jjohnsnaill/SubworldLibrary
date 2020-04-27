# SubworldLibrary
Dimensions, made easy.

Subworld Library allows you to easily add dimensions (referred to as subworlds), handling most of the hard work, including loading, saving, and Multiplayer compatibility.

What it does, in more detail:

- Registering

Any class that derives from the Subworld class is automatically registered as a subworld. Subworlds are highly customisable; from their size and how they generate, to whether they or the players inside of them save, to even what ModWorld is allowed to update while inside of them. Registering a subworld without directly referencing Subworld Library is also possible, via Mod.Call.

- Loading

Loading a subworld is as simple as calling "Subworld.Enter". Each subworld can have a custom loading UI if its class overrides "loadingUIState". The UI can even persist after the world is loaded if "loadingUI" is overridden.

- Saving

Subworlds can be set to be either temporary or permanent. Subworlds can also prevent players from saving while inside them. Subworld Library also handles deleting subworlds when their main world is deleted.

- Multiplayer

Subworld Library doesn't just handle loading, saving, etc. in Singleplayer; it also does so in Multiplayer, with little to no extra work required on your end. By default, players vote on whether to enter/leave a subworld, preventing any unwanted entries or exits.

- Misc.

Subworld Library gets rid of Space, the Underworld, and both Oceans inside subworlds, freeing up tons of world space, and allowing for very small subworlds.

If you need help with making a subworld, or encounter a bug, please go to the #sublib channel in the GaMeterraria Discord.

https://discord.gg/zumQztb
