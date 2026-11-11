# Series Subtitles — une langue de sous-titres par série (plugin Jellyfin)

Vous choisissez une série et une langue dans le tableau de bord : tous les épisodes de cette série
démarrent avec les sous-titres dans cette langue, sans réglage épisode par épisode.

> **État :** code source écrit pour Jellyfin 12 et 10.11, **pas encore compilé ni testé sur un serveur**.
> Il faut le compiler une fois (voir ci-dessous) pour obtenir le fichier `.dll` à installer.

## Compiler

Choisissez la commande qui correspond à votre serveur (Tableau de bord → version affichée).

| Serveur Jellyfin | Outil à installer | Commande |
| --- | --- | --- |
| 12.x | [SDK .NET 10](https://dotnet.microsoft.com/download) | `dotnet publish Jellyfin.Plugin.SeriesSubtitles -c Release -o out` |
| 10.11.x | [SDK .NET 9](https://dotnet.microsoft.com/download) | `dotnet publish Jellyfin.Plugin.SeriesSubtitles -c Release -p:JellyfinVersion=10.11.0 -o out` |

Le fichier utile est `out/Jellyfin.Plugin.SeriesSubtitles.dll`.

**Sans rien installer :** déposez ce dossier dans un dépôt GitHub. Le fichier
`.github/workflows/build.yml` compile les deux versions automatiquement ; les DLL se téléchargent
dans l'onglet *Actions* du dépôt.

## Installer

1. Arrêtez Jellyfin.
2. Dans le dossier `plugins` du serveur, créez un dossier `SeriesSubtitles_1.0.0.0` et copiez-y la DLL.
   - Linux : `/var/lib/jellyfin/plugins/`
   - Docker : `/config/plugins/`
   - Windows : `C:\ProgramData\Jellyfin\Server\plugins\` ou `%LOCALAPPDATA%\jellyfin\plugins\`
3. Redémarrez Jellyfin.

## Utiliser

Tableau de bord → Plugins → **Series Subtitles** :

1. Filtrez et choisissez la série.
2. Choisissez la langue.
3. « Ajouter ou remplacer la règle ». L'enregistrement est immédiat, sans redémarrage.

L'option « Imposer la langue même si le lecteur demande une autre piste » rend la règle stricte.
Décochée, la langue de la série est le choix par défaut et reste modifiable pendant une lecture.

## Fonctionnement

- Au lancement d'un épisode, le lecteur demande au serveur les informations de lecture
  (`POST /Items/{id}/PlaybackInfo`). Le plugin y inscrit la piste de sous-titres de la langue choisie ;
  le serveur fait ensuite son travail habituel, incrustation dans l'image comprise si le lecteur en a besoin.
- Dans les réponses qui décrivent un épisode, la piste par défaut est remplacée, pour que la fiche de
  l'épisode présélectionne la bonne langue.
- Piste retenue quand il y en a plusieurs dans la langue : sous-titres complets d'abord
  (ni « forcés » ni malentendants), puis la piste marquée par défaut.

## Limites

- La règle vaut pour tous les utilisateurs du serveur.
- Une piste sans étiquette de langue (« Indéterminé ») ne peut pas être reconnue. Pour un fichier
  externe, nommez-le `Episode.fr.srt`.
- Si un épisode n'a pas de sous-titres dans la langue choisie, rien n'est changé pour cet épisode.
- Un lecteur qui ignore la piste par défaut envoyée par le serveur ne sera pas affecté.

## Fichiers

- `Plugin.cs` : déclaration du plugin et de sa page de réglages.
- `PluginServiceRegistrator.cs` : branche le filtre sur l'API du serveur.
- `SeriesSubtitleFilter.cs` : applique la règle aux demandes de lecture et aux fiches d'épisodes.
- `SubtitleSelector.cs` : choix de la piste dans la langue demandée.
- `Configuration/` : réglages enregistrés et page du tableau de bord.
