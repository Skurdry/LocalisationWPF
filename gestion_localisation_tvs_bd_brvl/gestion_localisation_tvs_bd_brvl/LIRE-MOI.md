# Fichiers de gestion de localisation WPF

Cette archive contient uniquement les sources et les exemples, sans solution ni fichier projet.

## Intégration dans ton projet WPF

1. Utilise un projet WPF ciblant .NET 8 avec C# 12.
2. Ajoute le dossier `Core`, `GlobalUsings.cs`, `MainWindow.xaml` et `MainWindow.xaml.cs` à ton projet.
3. Les fichiers `App.xaml` et `App.xaml.cs` fournis sont destinés à remplacer ceux de ton projet, si tu souhaites utiliser cette application telle quelle. Ne conserve pas deux définitions de l’application. Garde App et MainWindow à la racine du projet.
4. Si tu conserves ton App existant, copie les ressources de l’App fourni dans `Application.Resources` et définis `StartupUri="MainWindow.xaml"`. Les ressources, notamment `Ink`, sont nécessaires à la fenêtre.
5. Les namespaces peuvent rester `LocalisationStudio.Wpf` et `LocalisationStudio.Core` même si ton projet a un autre nom. Si tu les modifies, adapte aussi les attributs `x:Class` et les directives `using`.
6. Dans Visual Studio, `App.xaml` doit avoir l’action de génération `ApplicationDefinition` et `MainWindow.xaml` l’action `Page`.

Aucune bibliothèque tierce n’est nécessaire. `Core` peut être ajouté directement au même projet WPF ; aucun second projet n’est requis.

## Fichiers

- `MainWindow.xaml` et son `.cs` : DataGrid, édition, langues, ouverture et export.
- `App.xaml` et son `.cs` : démarrage et styles.
- `GlobalUsings.cs` : imports communs si les imports implicites sont désactivés.
- `Core/TranslationDocument.cs` : modèle et validation.
- `Core/CsvCodec.cs` : lecture et écriture CSV.
- `Core/DocumentFiles.cs` : JSON, XML et sauvegardes.
- `Core/CodeExporter.cs` : génération des classes C# et C++.
- `Exemples/` : fichiers CSV, JSON et XML à importer.

Le JSON est le format de sauvegarde par défaut. Chaque export de classe génère aussi un fichier `.localization.json` permettant de reprendre l’édition. Les classes exportées se nomment `Localization` et exposent `Instance` et `Get(id, langue)`.

Les sources nécessitent encore une compilation et une validation visuelle sous Windows : WPF et le SDK .NET ne sont pas disponibles dans l’environnement de préparation.
