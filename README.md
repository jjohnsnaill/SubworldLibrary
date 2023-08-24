# SubworldLibrary
Dimensions, made easy.

A tool for modders to easily add dimensions (referred to as subworlds) to their mods, making all the necessary code injections, handling Multiplayer and managing every subworld.

Report issues here, or on the forum page: https://forums.terraria.org/index.php?threads/86283

Wiki: https://github.com/jjohnsnaill/SubworldLibrary/wiki

Copying any of the code is **not allowed**, with the exception of contributing to Subworld Library, or transforming it for completely different things. I repurpose the code injections for my other projects a lot, and people potentially learning things from the source code would be great! However, with all the maintenance and nasty code injection subworlds require, copying the library would be very detrimental to modders and users; bug fixes and improvements would not be shared between mods, it would needlessly complicate tracking down issues, and it would divide focus that would be much better for everyone going towards a single place.

## HOW IT WORKS
Subworld Library does a LOT of code injection, as Terraria was not made with subworlds in mind.
Subworlds are highly customisable; from how big or small they are, to what ModSystems can update inside of them and even how they are lit.
Subworld Library removes Space, both Oceans and the Underworld from subworlds, allowing them to be extremely small without issues.

## LOADING
Loading a subworld is straightforward. Loading screens can be as simple as text on a plain background, or something complex, like an item selection menu.

## SAVING
Subworlds save to a directory named after the main world. A subworld and/or changes to players inside it can be temporary. Deleting a world deletes all of its subworlds as well.

## MULTIPLAYER
Subworld Library works in Multiplayer with little to no extra work required from modders. A server is opened for every subworld being occupied.
