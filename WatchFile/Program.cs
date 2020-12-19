using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

if (args.Length == 0 || args.Any(arg => arg == "-?" || arg == "/?"))
{
    Console.WriteLine("Утилита отслеживания файла");
    Console.WriteLine("\twatchfile file.txt");
    Console.WriteLine("Отслеживает и выводит на консоль все изменения в файле");
    Console.WriteLine();
    Console.WriteLine(@"Параметры:
    -p              Напечатать исходное содержимое файла
    -e code_page    Указание номера кодировки (число)
    -e encoding     Указание имени кодировки
    -c              Не очищать при уменьшении размера файла
    -r              Не перечитывать уменьшившийся файл
    -i              Не отслеживать возможность уменьшения размера файла");
    return -1;
}

var no_clear = args.Any(arg => arg == "-c");
var no_reprint = args.Any(arg => arg == "-r");
var incremental_only = args.Any(arg => arg == "-i");

var file_name = args.FirstOrDefault(File.Exists);
if (file_name is null)
{
    Console.WriteLine("Не указан файл для отслеживания");
    return -2;
}

var file = new FileInfo(file_name);

if (!file.Exists)
{
    Console.WriteLine("Файл {0} не найден", file.FullName);
    return -3;
}

var result = new TaskCompletionSource<int>();

var watcher = new FileSystemWatcher(file.DirectoryName!, file.Name)
{
    NotifyFilter = NotifyFilters.LastWrite
};
watcher.Deleted += (_, _) => result.TrySetResult(0);

var file_length = file.Length;
Encoding encoding = Encoding.UTF8;
const int buffer_length = 1024;
var buffer = new char[buffer_length];

watcher.Changed += OnFileChanged; // Событие возникает дважды для каждого изменения файла
[MethodImpl(MethodImplOptions.Synchronized)]
void OnFileChanged(object sender, FileSystemEventArgs e)
{
    try
    {
        var new_length = PrintFile(file_length);
        if (new_length > file_length)
            file_length = new_length;
        else if (!incremental_only)
        {
            if (!no_clear)
                Console.Clear();
            file_length = new_length;
            if (file_length > 0 && !no_reprint)
                PrintFile();
        }
    }
    catch (Exception error)
    {
        Console.WriteLine(error.Message);
        result.TrySetResult(-4);
    }
}

long PrintFile(long offset = 0)
{
    using var file_stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
    var length = file_stream.Length;
    if (length == 0) // Возможно возвращение ложного значения нулевой длины
    {
        // Делаем 100 попыток определить длину
        for (var i = 0; i < 100 && file_stream.Length == 0; i++)
            Thread.SpinWait(1000); // Останавливаем поток на 1000 итераций между попытками

        if (file_stream.Length == 0)
            return 0;
    }

    if (offset > 0)
        file_stream.Seek(offset, SeekOrigin.Begin);
    using var reader = new StreamReader(file_stream, encoding, true);

    while (!reader.EndOfStream)
    {
        var readed = reader.Read(buffer, 0, buffer_length);
        var readed_str = new string(buffer, 0, readed);
        Console.Out.Write(buffer, 0, readed);
    }

    return file_stream.Length;
}

switch (Array.IndexOf(args, "-e"))
{
    case { } i when i >= 0 && i < args.Length - 2 && int.TryParse(args[i + 1], out var codepage):
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            encoding = Encoding.GetEncoding(codepage);
        }
        catch (NotSupportedException)
        {
            Console.WriteLine("Кодировка с указанным кодом {0} не найдена", codepage);
            return -5;
        }
        break;
    case { } i when i >= 0 && i < args.Length - 2:
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var new_encoding = Encoding.GetEncoding(args[i + 1]);
            encoding = new_encoding;
        }
        catch (ArgumentException)
        {
            Console.WriteLine("Кодировка с указанным именем {0} не найдена", args[i + 1]);
        }
        break;
}

if (args.Contains("-p"))
    PrintFile();

watcher.EnableRaisingEvents = true;

return await result.Task;