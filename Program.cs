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

Console.WriteLine($"\nкопирование файлов {year} из {folderPath} в {addinsDir}\n");

// Сборки TNov — всегда (кроме совпадающих), чтобы новые dll (например TNovApi.Client.dll)
// не оставались старыми. Сторонние зависимости — только если их ещё нет: их версии
// задаёт выпуск (TNovClient), локальная сборка не должна их подменять.
List<string> filesToCopy = new();
try
{
    filesToCopy = Directory.GetFiles(folderPath, "*.dll")
        .Where(path => Path.GetFileName(path).StartsWith("TNov", StringComparison.OrdinalIgnoreCase)
                       || !File.Exists(Path.Combine(addinsDir, Path.GetFileName(path))))
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

Console.WriteLine("\nДля продолжения нажмите любую клавишу...");
Console.ReadKey();

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
