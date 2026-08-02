# Subtitle Overlay

Application Windows WPF en C#/.NET 8 qui lit un fichier audio avec LibVLC et affiche
les sous-titres SRT synchronisés dans une fenêtre toujours visible.

## Prérequis

- Windows 10 ou 11, 64 bits ;
- SDK [.NET 8](https://dotnet.microsoft.com/download/dotnet/8.0) pour le développement ;
- accès à NuGet lors de la première restauration.

Les packages `LibVLCSharp.WPF` et `VideoLAN.LibVLC.Windows` fournissent le lecteur et
les bibliothèques natives LibVLC. VLC n’a pas besoin d’être installé séparément.

## Compiler et lancer

Dans PowerShell, depuis le dossier du projet :

```powershell
dotnet restore
dotnet build
dotnet run
```

## Publier pour Windows 64 bits

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

L’exécutable est produit dans :

```text
bin\Release\net8.0-windows\win-x64\publish\SubtitleOverlay.exe
```

La version Windows déjà compilée est disponible dans la page
[Releases](https://github.com/Hex718/SubtitleOverlay/releases/latest). Après
extraction du ZIP, il faut conserver le sous-dossier `libvlc` à côté de
`SubtitleOverlay.exe`.

`IncludeNativeLibrariesForSelfExtract` est activé : l’exécutable autonome extrait au
démarrage les bibliothèques natives LibVLC dans le répertoire temporaire de .NET.
Le premier lancement peut donc être légèrement plus lent. Si un antivirus bloque
l’extraction, publier avec `-p:PublishSingleFile=false` conserve les DLL natives à
côté de l’exécutable et constitue la solution la plus robuste.

## Utilisation

1. Cliquez sur **Ouvrir un audio** et choisissez un fichier MP3, WAV, FLAC, M4A,
   AAC, OGG ou OPUS.
2. Choisissez un SRT avec **Ouvrir un SRT**. Si un fichier portant le même nom que
   l’audio est présent dans le même dossier, il est chargé automatiquement.
3. Lancez la lecture. La recherche utilise la formule
   `temps SRT = temps audio + décalage`.
4. Déplacez l’overlay en le faisant glisser. Redimensionnez-le depuis son bord.
   Les boutons Haut, Centre et Bas le repositionnent sur l’écran principal.
5. Fermer la fenêtre de contrôles la masque seulement. L’application reste
   disponible dans la zone de notification ; utilisez **Quitter** pour l’arrêter.

Les fichiers SRT UTF-8 avec ou sans BOM, les textes multilignes et les horodatages
`HH:mm:ss,fff` sont acceptés.

## Raccourcis globaux

| Raccourci | Action |
|---|---|
| `Ctrl + Alt + Espace` | Lecture / pause |
| `Ctrl + Alt + Gauche` | Reculer de 10 secondes |
| `Ctrl + Alt + Droite` | Avancer de 10 secondes |
| `Ctrl + Alt + Haut` | Avancer les sous-titres de 500 ms |
| `Ctrl + Alt + Bas` | Retarder les sous-titres de 500 ms |
| `Ctrl + Alt + T` | Activer/désactiver le clic à travers |
| `Ctrl + Alt + O` | Afficher/masquer l’overlay |
| `Ctrl + Alt + R` | Recaler le sous-titre affiché sur la position audio |

Le raccourci `Ctrl + Alt + T` permet toujours de récupérer un overlay en mode clic
à travers. Un message au démarrage indique les raccourcis déjà réservés par une
autre application.

### Synchronisation précise

La barre de progression n’envoie qu’une seule commande de recherche à LibVLC,
au relâchement de la souris. L’index SRT est alors recalculé par recherche binaire.

Pour corriger un décalage constant :

1. mettez en pause exactement au début d’une phrase entendue ;
2. choisissez la phrase SRT correspondante avec **Phrase précédente/suivante** ;
3. cliquez sur **Point A — caler ici** ou utilisez `Ctrl + Alt + R`.

Pour corriger un décalage qui augmente au fil du temps, définissez le Point A près
du début, puis répétez l’opération au moins une minute plus loin avec
**Point B — corriger la dérive**. L’application calcule alors une transformation
linéaire :

```text
temps SRT = temps audio × vitesse de synchronisation + offset
```

Deux phrases explicitement choisies sont nécessaires : l’application ne peut pas
deviner automatiquement quelle phrase est prononcée sans effectuer une nouvelle
reconnaissance vocale.

## Réglages

Le panneau Apparence règle la police, la taille, les opacités, les couleurs (format
hexadécimal ARGB, par exemple `#FFFFFFFF`), l’ombre et la bordure. La position, la
taille, le volume, le décalage et le clic à travers sont sauvegardés dans :

```text
%APPDATA%\SubtitleOverlay\settings.json
```

Un fichier absent ou corrompu est remplacé en mémoire par les valeurs par défaut.

## Limitations connues

- L’overlay est positionné par les boutons sur l’écran principal ; il peut être
  déplacé manuellement vers un autre écran.
- Les raccourcis sont fixes dans cette version.
- Le format SRT est pris en charge ; les formats ASS, VTT et SSA ne le sont pas.
- Le mode transparent WPF impose une fenêtre système sans barre de titre. L’option
  de bordure affiche donc une bordure visuelle interne plutôt qu’une barre Windows.
