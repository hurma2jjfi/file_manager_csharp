using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace oop
{
    public abstract class FileSystemItem
    {
        public string Name { get; }
        public string Fullname { get; }
        public DateTime CreationTime { get; }
        public abstract long Size { get; }
        public abstract bool IsDirectory { get; }

        protected FileSystemItem(string fullname)
        {
            Fullname = fullname;
            Name = Path.GetFileName(fullname);
            CreationTime = File.GetCreationTime(fullname);
        }

        public override string ToString() => Name;
    }

    public class FileItem : FileSystemItem
    {
        private readonly FileInfo _fileInfo;

        public FileItem(string path) : base(path)
        {
            _fileInfo = new FileInfo(path);
        }

        public override long Size => _fileInfo.Length;
        public override bool IsDirectory => false;
    }

    public class DirectoryItem : FileSystemItem
    {
        private readonly DirectoryInfo _dirInfo;

        public DirectoryItem(string path) : base(path)
        {
            _dirInfo = new DirectoryInfo(path);
        }

        public override long Size
        {
            get
            {
                try
                {
                    return _dirInfo.EnumerateFiles("*", SearchOption.AllDirectories)
                                   .Sum(f => f.Length);
                }
                catch (UnauthorizedAccessException)
                {
                    return -1;
                }
            }
        }

        public override bool IsDirectory => true;
    }

    public interface IFileSystem
    {
        FileSystemItem[] GetItems(string path);
        void Delete(string path);
        void Copy(string source, string destination, bool overwrite = false);
        void Move(string source, string destination);
        bool Exists(string path);
        string GetCurrentDirectory();
        void SetCurrentDirectory(string path);
        void CreateDirectory(string path);
        void CreateFile(string path);
    }

    public class RealFileSystem : IFileSystem
    {
        public FileSystemItem[] GetItems(string path)
        {
            var dir = new DirectoryInfo(path);
            var files = dir.GetFiles().Select(f => (FileSystemItem)new FileItem(f.FullName));
            var dirs = dir.GetDirectories().Select(d => (FileSystemItem)new DirectoryItem(d.FullName));
            return files.Concat(dirs).ToArray();
        }

        public void Delete(string path)
        {
            if (File.Exists(path))
                File.Delete(path);
            else if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }

        public void Copy(string source, string destination, bool overwrite = false)
        {
            if (Directory.Exists(source))
                CopyDirectory(new DirectoryInfo(source), new DirectoryInfo(destination), overwrite);
            else
                File.Copy(source, destination, overwrite);
        }

        public void Move(string source, string destination)
        {
            if (Directory.Exists(source))
                Directory.Move(source, destination);
            else
                File.Move(source, destination, overwrite: true);
        }

        public bool Exists(string path) => File.Exists(path) || Directory.Exists(path);

        public string GetCurrentDirectory() => Directory.GetCurrentDirectory();

        public void SetCurrentDirectory(string path) => Directory.SetCurrentDirectory(path);

    
        public void CreateDirectory(string path)
        {
            Directory.CreateDirectory(path);
        }

        public void CreateFile(string path)
        {
            File.Create(path).Dispose();
        }

        private void CopyDirectory(DirectoryInfo source, DirectoryInfo target, bool overwrite)
        {
            if (!target.Exists)
                target.Create();

            foreach (FileInfo file in source.GetFiles())
            {
                file.CopyTo(Path.Combine(target.FullName, file.Name), overwrite);
            }

            foreach (DirectoryInfo subDir in source.GetDirectories())
            {
                CopyDirectory(subDir, target.CreateSubdirectory(subDir.Name), overwrite);
            }
        }
    }

    class Program
    {
        private static IFileSystem _fs = new RealFileSystem();

        static void Main(string[] args)
        {
            Console.WriteLine("Консольный файловый менеджер (OOP-версия)");
            Console.WriteLine("Команды: ls, cd <путь>, pwd, mkdir <имя>, touch <имя>, del <путь>, clear, exit");
            Console.WriteLine();

            while (true)
            {
                try
                {
                    string currentDir = _fs.GetCurrentDirectory();
                    Console.Write($"{currentDir}> ");
                    string input = Console.ReadLine();

                    if (string.IsNullOrWhiteSpace(input))
                        continue;

                    string[] parts = input.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    string command = parts[0].ToLower();

                    switch (command)
                    {
                        case "ls":
                        case "dir":
                            ListDirectory();
                            break;

                        case "cd":
                            if (parts.Length < 2)
                                Console.WriteLine("Использование: cd <путь>");
                            else
                                ChangeDirectory(parts[1]);
                            break;

                        case "pwd":
                            Console.WriteLine(_fs.GetCurrentDirectory());
                            break;

                        case "mkdir":
                            if (parts.Length < 2)
                                Console.WriteLine("Использование: mkdir <имя_папки>");
                            else
                                CreateDirectory(parts[1]);
                            break;

                        case "touch":
                            if (parts.Length < 2)
                                Console.WriteLine("Использование: touch <имя_файла>");
                            else
                                CreateFile(parts[1]);
                            break;

                        case "del":
                        case "rm":
                            if (parts.Length < 2)
                                Console.WriteLine("Использование: del <путь>");
                            else
                                DeleteItem(parts[1]);
                            break;

                        case "edit":
                            if(parts.Length > 2)
                            {
                                Console.WriteLine("Использование: edit <имя_файла>");
                            } else
                            {
                                EditFile(parts[1]);
                            }
                            break;

                        case "clear":
                                    Console.Clear();
                                    break;

                                case "help":
                                    ShowHelp();
                                    break;

                                case "exit":
                                    return;

                                default:
                                    Console.WriteLine($"Неизвестная команда: {command}. Введите 'help' для списка.");
                                    break;
                                }
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Ошибка: {ex.Message}");
                    Console.ResetColor();
                }
            }
        }

        static void ListDirectory()
        {
            string current = _fs.GetCurrentDirectory();
            var items = _fs.GetItems(current);

            if (items.Length == 0)
            {
                Console.WriteLine("(Папка пуста)");
                return;
            }

            Console.WriteLine("{0,-8} {1,-30} {2,-12} {3}", "Тип", "Имя", "Размер", "Дата создания");
            Console.WriteLine(new string('-', 65));

            foreach (var item in items)
            {
                string type = item.IsDirectory ? "DIR" : "FILE";
                string size = item.IsDirectory ? "<DIR>" : $"{item.Size} байт";
                string creationDate = item.CreationTime.ToString("yyyy-MM-dd HH:mm");
                Console.WriteLine("{0,-8} {1,-30} {2,-12} {3}", type, item.Name, size, creationDate);
            }
        }

        static void ChangeDirectory(string path)
        {
            string current = _fs.GetCurrentDirectory();
            string target = Path.GetFullPath(Path.Combine(current, path));

            if (!_fs.Exists(target) || !Directory.Exists(target))
            {
                Console.WriteLine($"Папка не найдена: {target}");
                return;
            }

            _fs.SetCurrentDirectory(target);
        }

        static void CreateDirectory(string name)
        {
            string current = _fs.GetCurrentDirectory();
            string fullPath = Path.Combine(current, name);
            _fs.CreateDirectory(fullPath);
            Console.WriteLine($"Папка создана: {fullPath}");
        }

        static void CreateFile(string name)
        {
            string current = _fs.GetCurrentDirectory();
            string fullPath = Path.Combine(current, name);
            _fs.CreateFile(fullPath);
            Console.WriteLine($"Файл создан: {fullPath}");
        }

        static void DeleteItem(string path)
        {
            string current = _fs.GetCurrentDirectory();
            string fullPath = Path.GetFullPath(Path.Combine(current, path));

            if (!_fs.Exists(fullPath))
            {
                Console.WriteLine($"Путь не существует: {fullPath}");
                return;
            }

            _fs.Delete(fullPath);
            Console.WriteLine($"Удалено: {fullPath}");
        }
        static void EditFile(string fileName)
        {
            string current = _fs.GetCurrentDirectory();
            string fullPath = Path.GetFullPath(Path.Combine(current, fileName));

            if (!File.Exists(fullPath))
            {
                Console.WriteLine($"Файл не найден: {fullPath}");
                return;
            }

            try
            {
                // Открываем файл в системном редакторе (Блокнот на Windows)
                Process.Start(new ProcessStartInfo
                {
                    FileName = fullPath,
                    UseShellExecute = true // обязательно для открытия через оболочку
                });
                Console.WriteLine($"Открыт файл: {fullPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Не удалось открыть файл: {ex.Message}");
            }
        }

        static void ShowHelp()
        {
            Console.WriteLine("\nДоступные команды:");
            Console.WriteLine("  ls / dir          — показать содержимое папки");
            Console.WriteLine("  cd <путь>         — перейти в папку (.. — вверх)");
            Console.WriteLine("  pwd               — показать текущий путь");
            Console.WriteLine("  mkdir <имя>       — создать папку");
            Console.WriteLine("  touch <имя>       — создать пустой файл");
            Console.WriteLine("  edit <имя>        — открыть файл для редактирования");
            Console.WriteLine("  del <путь> / rm   — удалить файл или папку");
            Console.WriteLine("  clear             — очистить экран");
            Console.WriteLine("  help              — показать эту справку");
            Console.WriteLine("  exit              — выйти из программы");
            Console.WriteLine();
        }
    }
}