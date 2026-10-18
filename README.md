# TNovDeploy

Общие настройки сборки плагина TNov и установка локальной сборки в Revit для тестов.

- `TNov.Build.props` — общие настройки всех модулей. Его подключает `Directory.Build.props` каждого репозитория: `..\TNovDeploy\TNov.Build.props`. Конфигурации: `R2022` (net48) и `R2027` (net10.0-windows).
- Сборка идёт локально, в `<папка репозиториев>\TNovBuild\<год>\`. Папку можно переопределить переменной окружения `TNovBuildRoot`.
- `TNovDeploy.exe [год] [папка сборки]` копирует свежие `TNov*.dll` в `%APPDATA%\Autodesk\Revit\Addins\<год>\TNov`. Из сторонних зависимостей копируются только отсутствующие. Запускается автоматически после сборки TNov; отключается свойством `-p:TNovDeploy=false`.
- Выпуск делает TNovReleaser: он берёт dll из той же папки `TNovBuild`.

## Новая машина

Все репозитории клонируются рядом, в одну папку (например `C:\DEV`):

```
git clone https://github.com/novabimperm-creator/TNovDeploy.git C:\DEV\TNovDeploy
powershell -ExecutionPolicy Bypass -File C:\DEV\TNovDeploy\Setup-Workspace.ps1
```

Скрипт клонирует недостающие репозитории, собирает TNovDeploy и TNovReleaser и создаёт `TNovBuild`. С ключом `-Pull` он ещё и подтягивает изменения в уже клонированных репозиториях.

Что нужно установить:

- Visual Studio 2022+ с .NET desktop development;
- .NET SDK 10;
- .NET Framework 4.8 Developer Pack;
- git.

Revit для сборки не нужен: API приходит пакетом `Revit_All_Main_Versions_API_x64`.
