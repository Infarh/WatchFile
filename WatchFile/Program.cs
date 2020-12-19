using System;
using System.IO;

if (args.Length == 0)
{
    Console.WriteLine("Не указано имя файла");
    return -1;
}

var file_name = args[0];

if (!File.Exists(file_name))
{
    Console.WriteLine("Файл не найден");
    return -2;
}

return 0;