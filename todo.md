# To-do List / Appunti Futuri

## Bug da risolvere
- [ ] 

## Nuove feature 
- [X] Aggiungere interazioni con blocchi piazzabili come chest e faló
- [X] Aggiungi interazione con alberi/rocce
- [ ] Implementare un sistema di salvataggio
- [ ] Aggiungere nuovi nemici
- [ ] Aggiungere menu principale
- [ ] Aggiungere sistema di crafting
- [X] Aggiungere sistema di piazzamento blocchi

## Refactoring e Ottimizzazione
- [ ] Worldgen

## Idee sparse / Design
- [X] Luci mini dinamiche per faló torce ecc
- [ ] Aggiungi torce/lamp

Passi consigliati in Unity:

Crea il prefab riga ricetta
In Canvas crea un GameObject UI, ad esempio RecipeRow.
Aggiungi componenti UI:
Image per icona
Text per nome
Text per ingredienti
Text per quantità craftabile
Button per selezione riga
opzionale GameObject highlight selezione
Aggiungi script CraftingRecipeRowUI.
Trascina nei campi dello script i riferimenti corretti (iconImage, titleText, ingredientsText, craftableCountText, selectButton, selectedHighlight).
Salva come prefab.
Crea il prefab menu crafting
In Canvas crea un pannello principale, ad esempio CraftingMenuRoot.
Aggiungi script CraftingStationMenuUI.
Dentro al pannello crea un contenitore lista, ad esempio RecipesRoot.
Sul contenitore lista aggiungi Vertical Layout Group e Content Size Fitter.
Assegna in CraftingStationMenuUI:
recipesRoot = il contenitore lista
recipeRowPrefab = il prefab RecipeRow creato prima
playerInventory e playerTopDown (o lascia auto-find)
Opzionale: aggiungi bottoni Craft x1, x5, x10, Max e collega gli OnClick ai metodi pubblici dello script menu.
Salva CraftingMenuRoot come prefab
Questo prefab andrà poi assegnato nel campo interactionMenuPrefab della definizione placeable del crafting table.