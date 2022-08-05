# SubworldLibrary
Dimensions, made easy.

A tool for modders to easily add dimensions (referred to as subworlds) to their mods, making all the necessary code injections, handling Multiplayer and managing every subworld.

Report issues here, or on the forum page: https://forums.terraria.org/index.php?threads/86283

Wiki: https://github.com/jjohnsnaill/SubworldLibrary/wiki

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