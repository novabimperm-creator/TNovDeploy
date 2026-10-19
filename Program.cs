using System.Diagnostics;

// Ставит свежую локальную сборку плагина в Revit на этой машине (для тестов).
//   TNovDeploy.exe [год] [папка сборки]
// Год — 2022 (по умолчанию) или 2027. Папка сборки по умолчанию — TNovBuild\<год> рядом
// с репозиториями (см. TNov.Build.props) или %TNovBuildRoot%\<год>.
string year = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]) ? args[0].Trim() : "2022";
string folderPath = args.Length > 1 && !string.IsNullOrWhiteSpace(args[1])
    ? args[1].Trim()
    : Path.Combine(BuildRoot(), year);
string addinsRoot = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
    "Autodesk", "Revit", "Addins", year);
string addinsDir = Path.Combine(addinsRoot, "TNov");

// Открытый Revit держит dll плагина — ждём, пока его закроют.
while (IsRevitRunning(year))
{
    Console.WriteLine($"Revit {year} открыт.");
    Console.WriteLine("Закройте Revit и нажмите любую клавишу, чтобы повторить...");
    Console.ReadKey(true);
    Console.WriteLine();
}

Console.WriteLine($"\nкопирование файлов {year} из {folderPath} в {addinsDir}\n");

// Только сборки TNov (все, чтобы новые dll вроде TNovApi.Client.dll не оставались старыми).
// Сторонние зависимости ставит TNovClient из выпуска: в папке сборки их много лишних
// (транзитивные пакеты NuGet), в Revit они не нужны.
RemoveStrayDependencies(folderPath, addinsDir, year);

List<string> filesToCopy = new();
try
{
    filesToCopy = Directory.GetFiles(folderPath, "TNov*.dll")
        .Where(path => FilesDiffer(path, Path.Combine(addinsDir, Path.GetFileName(path))))
        .Select(Path.GetFileName)
        .OfType<string>()
        .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
        .ToList();
}
catch (Exception e)
{
    Console.WriteLine($"Ошибка чтения {folderPath}: {e.Message}");
}

if (filesToCopy.Count == 0)
{
    Console.WriteLine("Обновление файлов не требуется");
}
else
{
    while (true)
    {
        int copiedCount = 0;
        bool failed = false;
        try
        {
            Directory.CreateDirectory(addinsDir);
            foreach (string file in filesToCopy)
            {
                string source = Path.Combine(folderPath, file);
                if (!File.Exists(source))
                {
                    Console.WriteLine($"Пропуск (нет файла): {file}");
                    continue;
                }

                File.Copy(source, Path.Combine(addinsDir, file), true);
                copiedCount++;

                FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(source);
                string? fileDescription = fvi.ProductName;
                string? fileVersion = fvi.FileVersion;
                Console.WriteLine(fileDescription != null && fileVersion != null
                    ? fileDescription + " " + fileVersion
                    : file);
            }
        }
        catch (Exception e)
        {
            // Чаще всего Revit открыт и держит dll.
            failed = true;
            Console.WriteLine($"Ошибка {e.Message}");
        }

        if (!failed && copiedCount > 0)
            break;

        Console.WriteLine("\nЗакройте Revit и нажмите любую клавишу, чтобы повторить...");
        Console.ReadKey(true);
        Console.WriteLine($"\nкопирование файлов {year} из {folderPath} в {addinsDir}\n");
    }
}

if (!File.Exists(Path.Combine(addinsRoot, "TNov.addin")))
    Console.WriteLine($"\nВнимание: нет {Path.Combine(addinsRoot, "TNov.addin")} — Revit не загрузит плагин. " +
                      "Установите TNovClient (он ставит .addin) или скопируйте файл вручную.");

// Revit 2027 пока не запускаем.
if (year == "2022")
    StartRevit(year);

Console.WriteLine("\nДля продолжения нажмите любую клавишу...");
Console.ReadKey();

// Revit нужной версии ищем по пути exe (C:\Program Files\Autodesk\Revit 2022\Revit.exe).
// Если путь не прочитать (например, Revit запущен от администратора), считаем, что это он.
static bool IsRevitRunning(string year)
{
    foreach (Process process in Process.GetProcessesByName("Revit"))
    {
        using (process)
        {
            string? path;
            try
            {
                path = process.MainModule?.FileName;
            }
            catch
            {
                return true;
            }

            if (path == null || path.Contains("Revit " + year, StringComparison.OrdinalIgnoreCase))
                return true;
        }
    }
    return false;
}

static void StartRevit(string year)
{
    string revitExe = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        "Autodesk", "Revit " + year, "Revit.exe");
    if (!File.Exists(revitExe))
    {
        Console.WriteLine($"\nRevit {year} не запущен: нет {revitExe}");
        return;
    }

    try
    {
        Process.Start(new ProcessStartInfo(revitExe) { UseShellExecute = true });
        Console.WriteLine($"\nзапуск Revit {year}");
    }
    catch (Exception e)
    {
        Console.WriteLine($"\nRevit {year} не запущен: {e.Message}");
    }
}

// Папка сборки: %TNovBuildRoot% или TNovBuild рядом с репозиториями. Корень репозиториев
// ищем вверх от exe по TNov.Build.props (exe лежит в TNovDeploy\bin\deploy).
static string BuildRoot()
{
    string? fromEnvironment = Environment.GetEnvironmentVariable("TNovBuildRoot");
    if (!string.IsNullOrWhiteSpace(fromEnvironment))
        return fromEnvironment;

    for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
    {
        if (File.Exists(Path.Combine(dir.FullName, "TNov.Build.props")))
            return Path.Combine(dir.Parent?.FullName ?? dir.FullName, "TNovBuild");
    }

    throw new DirectoryNotFoundException("Не найден TNov.Build.props над " + AppContext.BaseDirectory);
}

// Прежняя версия TNovDeploy копировала в папку Revit все сторонние dll из сборки, которых там
// не было. Убираем из папки плагина dll, которые есть в сборке, но не входят в выпуск
// (корпоративный манифест): так удаляются только скопированные деплоем, а не поставленные клиентом.
static void RemoveStrayDependencies(string buildDir, string addinsDir, string year)
{
    if (!Directory.Exists(addinsDir) || !Directory.Exists(buildDir))
        return;

    HashSet<string> released;
    try
    {
        using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("TNovDeploy");
        using var manifest = System.Text.Json.JsonDocument.Parse(
            http.GetStringAsync("https://novabimperm-creator.github.io/TNovDist/corp/manifest.json").GetAwaiter().GetResult());
        string prefix = year + "/TNov/";
        released = manifest.RootElement.GetProperty("files").EnumerateArray()
            .Select(file => file.GetProperty("path").GetString() ?? "")
            .Where(path => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(path => path.Substring(prefix.Length))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
    catch (Exception e)
    {
        // Без манифеста не знаем, что входит в выпуск, — ничего не удаляем.
        Console.WriteLine($"Лишние зависимости не проверены (манифест недоступен: {e.Message})\n");
        return;
    }

    if (released.Count == 0)
        return;

    var stray = Directory.GetFiles(addinsDir, "*.dll")
        .Select(Path.GetFileName)
        .OfType<string>()
        .Where(name => !name.StartsWith("TNov", StringComparison.OrdinalIgnoreCase)
                       && !released.Contains(name)
                       && File.Exists(Path.Combine(buildDir, name)))
        .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
        .ToList();
    if (stray.Count == 0)
        return;

    Console.WriteLine("удаление зависимостей, которых нет в выпуске:");
    foreach (string name in stray)
    {
        try
        {
            File.Delete(Path.Combine(addinsDir, name));
            Console.WriteLine("  " + name);
        }
        catch (Exception e)
        {
            Console.WriteLine($"  {name}: не удалён ({e.Message}) — закройте Revit и запустите деплой ещё раз");
        }
    }
    Console.WriteLine();
}

static bool FilesDiffer(string source, string target)
{
    if (!File.Exists(target))
        return true;
    var sourceInfo = new FileInfo(source);
    var targetInfo = new FileInfo(target);
    if (sourceInfo.Length != targetInfo.Length)
        return true;
    return !File.ReadAllBytes(source).AsSpan().SequenceEqual(File.ReadAllBytes(target));
}
