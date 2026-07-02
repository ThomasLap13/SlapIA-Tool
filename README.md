<div align="center">

# SlapIA Tool

**Informations systeme et monitoring temps reel pour Windows.**

Une application de bureau native, au design Fluent (Windows 11), qui affiche le materiel,
le systeme, les performances en direct et les logiciels installes sur un PC.

[![Plateforme](https://img.shields.io/badge/plateforme-Windows%2010%2F11-0078D4)](https://github.com/ThomasLap13/SlapIA-Tool)
[![.NET](https://img.shields.io/badge/.NET-8-512BD4)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Licence](https://img.shields.io/badge/licence-MIT-informational)](LICENSE)
[![Derniere release](https://img.shields.io/github/v/release/ThomasLap13/SlapIA-Tool?label=derniere%20version)](https://github.com/ThomasLap13/SlapIA-Tool/releases/latest)

[**Telecharger la derniere version**](https://github.com/ThomasLap13/SlapIA-Tool/releases/latest) ·
[Signaler un bug](https://github.com/ThomasLap13/SlapIA-Tool/issues)

![Vue d'ensemble de SlapIA Tool](docs/screenshot-overview.png)

</div>

## Sommaire

- [Fonctionnalites](#fonctionnalites)
- [Installation](#installation)
- [Stack technique](#stack-technique)
- [Demarrer en developpement](#demarrer-en-developpement)
- [Compiler l'installeur](#compiler-linstalleur-exe)
- [Publier une mise a jour](#publier-une-nouvelle-version-mises-a-jour-automatiques)
- [Packaging alternatif (MSIX)](#packaging-alternatif--msix-microsoft-store--winget)
- [Structure du projet](#structure-du-projet)
- [Confidentialite](#confidentialite)

## Fonctionnalites

| | |
|---|---|
| 🖥️ **Vue d'ensemble** | Carte de synthese : ordinateur, OS, uptime, CPU, RAM, GPU, disque, carte mere. |
| 🔧 **Materiel** | Detail complet : processeur, memoire, cartes graphiques, disques physiques, volumes, cartes reseau, BIOS. |
| 📈 **Monitoring temps reel** | Utilisation CPU / RAM / disque / GPU avec graphique en direct (rafraichi chaque seconde). |
| 📦 **Logiciels installes** | Inventaire des applications installees (nom, version, editeur, date) avec recherche instantanee. |
| 🔄 **Mises a jour automatiques** | Bouton "Verifier les mises a jour" qui interroge les [releases GitHub](https://github.com/ThomasLap13/SlapIA-Tool/releases) et installe la nouvelle version en un clic. |

## Installation

1. Telecharger `SlapIA.Tool-win-Setup.exe` depuis la [derniere release](https://github.com/ThomasLap13/SlapIA-Tool/releases/latest).
2. Executer le fichier. L'application s'installe comme n'importe quel programme Windows
   (raccourcis Bureau / Menu Demarrer, entree dans "Applications installees" avec
   desinstalleur) — aucune dependance a installer au prealable.
3. Les mises a jour suivantes se font depuis l'application elle-meme (bouton "Verifier les
   mises a jour" dans la barre laterale).

## Stack technique

| | |
|---|---|
| Framework | .NET 8 / WPF |
| Architecture | MVVM ([CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet)) |
| Design | Fluent 2 (palette Windows 11), [WPF-UI](https://github.com/lepoco/wpfui) |
| Graphiques | [LiveCharts2](https://livecharts.dev/) |
| Donnees systeme | WMI (`System.Management`), compteurs de performance Windows, registre |
| Installeur & auto-update | [Velopack](https://velopack.io/) |
| Packaging alternatif | MSIX (Microsoft Store / winget) |

## Demarrer en developpement

Prerequis : [SDK .NET 8](https://dotnet.microsoft.com/download/dotnet/8.0), Windows 10/11.

```powershell
git clone https://github.com/ThomasLap13/SlapIA-Tool.git
cd SlapIA-Tool
dotnet run --project src/SlapIA.App
```

## Compiler l'installeur (.exe)

Le script `packaging/build-installer.ps1` publie l'application et l'empaquette avec
[Velopack](https://velopack.io/) en un installeur classique :

```powershell
dotnet tool install -g vpk   # une seule fois
./packaging/build-installer.ps1
```

Cela genere dans `packaging/Releases/` :

- **`SlapIA.Tool-win-Setup.exe`** — l'installeur a distribuer. Double-clic = installation
  dans `%LocalAppData%\SlapIA.Tool`, raccourcis Bureau/Menu Demarrer, entree dans
  "Applications installees" avec desinstalleur, exactement comme un programme classique.
- `SlapIA.Tool-win-Portable.zip`, `*.nupkg`, `RELEASES` — le flux de mise a jour a publier
  sur GitHub (voir ci-dessous).

Installation silencieuse (utile pour les scripts de deploiement) :

```powershell
SlapIA.Tool-win-Setup.exe --silent
```

### Publier une nouvelle version (mises a jour automatiques)

1. Monter le numero de version dans `src/SlapIA.App/SlapIA.App.csproj` (`<Version>`).
2. Regenerer l'installeur : `./packaging/build-installer.ps1`
3. Creer une [release GitHub](https://github.com/ThomasLap13/SlapIA-Tool/releases/new) taguee
   `v<version>` et y uploader **tout le contenu** de `packaging/Releases/`.
4. C'est tout : le bouton "Verifier les mises a jour" de l'application (via `GithubSource`)
   detecte la nouvelle release, la telecharge et redemarre l'app dessus.

> Les mises a jour ne fonctionnent que pour une version installee via `Setup.exe` — lancer
> l'app avec `dotnet run` desactive la verification (`IsInstalled = false`).

## Packaging alternatif : MSIX (Microsoft Store / winget)

Un second script produit un paquet `.msix` pour une distribution via le Microsoft Store :

```powershell
./packaging/build-msix.ps1          # empaqueter
./packaging/build-msix.ps1 -Sign    # + signer avec un certificat de test (installation locale)
```

1. Reserver le nom de l'application sur le [Centre partenaires Microsoft](https://partner.microsoft.com/dashboard).
2. Mettre a jour `Identity` / `Publisher` dans `packaging/Package.appxmanifest` avec les valeurs fournies.
3. Soumettre le `.msix` via le Centre partenaires (sans `-Sign` — le Store signe lui-meme le paquet).
4. Une fois publiee, l'application est automatiquement disponible via `winget install` (source `msstore`).

## Structure du projet

```
SlapIA tool/
├─ src/SlapIA.App/
│  ├─ Models/          # Enregistrements de donnees (SystemSnapshot, InstalledApplication, ...)
│  ├─ Services/         # Acces WMI, compteurs de perf, registre, auto-update (Velopack)
│  ├─ ViewModels/        # Logique MVVM (CommunityToolkit.Mvvm)
│  ├─ Views/            # Pages XAML (Overview, Hardware, Monitoring, Software)
│  └─ Converters/         # Convertisseurs de binding XAML
├─ packaging/            # Installeur Velopack, manifeste MSIX, assets
└─ docs/                # Captures d'ecran
```

## Confidentialite

SlapIA Tool fonctionne entierement en local : aucune information systeme n'est envoyee
vers un serveur externe (seule la verification de mise a jour contacte l'API GitHub).

## Licence

Distribue sous licence [MIT](LICENSE).
