
//Read this before setting up the procedural generaton script:
/* 
 * The script is a ready drop in function that will generate pseudo random map layout and generate geometry around it
 * 
 * First section is room layout, you can modify the leangth of the main path, cell size and use either random or your own seed.
 * 
 * After that comes the braches section where you can modify the amount of branches, their leangh and spawning chance. Max total rooms shows how many rooms should there be maximum counting the main path.
 * 
 * For the script to work properly make shure to fill in all the prefabs. Not filling them wont stop the process but some bits will be empty.
 * 
 * Make sure to put in the marker prefabs as without them objects and doorways won't appear.
 * 
 * You can also modify the amout of enemies spawns and looting rooms spawn chance.
 * 
 * Debug section can remove the gizmos and regenerate the layout on reset.
 * 
 * 
 * Game CONTROLS:
 * 
 * WASD to move
 * 
 * E to pick up/interact
 * 
 * R to open recipies
 * 
 * G to drop an item in hand
 * 
 * Mouse 1 to throw item in hand
 * 
 * 1 2 3 4 5 inventory slots
 * 
 */
