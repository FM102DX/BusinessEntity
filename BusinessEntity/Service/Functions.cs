using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessEntity.Service
{
    public static class Functions
    {
        public static string GetNextFreeFileName(string path, string nameBase, string extention)
        {
            String s;
            int i = 0;
            do
            {
                s = Path.Combine(path, $"{nameBase}_{i}.{extention}");
                if (!File.Exists(s)) { return s; }
                i++;
                if (i > 10000) 
                { 
                    throw new Exception($"Too much log files in directory {path}, please consider clearing");
                }
            }
            while (true);
        }

        public static void LeaveLastNFilesOrFoldersInDirectory(string path, int leaveNumber)
        {
            System.IO.DirectoryInfo di = new DirectoryInfo(path);
            var files = di.GetFiles();
            var filesToRemove = files.ToList().OrderBy(x => x.CreationTime).Skip(leaveNumber).ToList();
            filesToRemove.ForEach(file => file.Delete());
        }

        public static string TimeSpanAsString(TimeSpan timeSpan)
        {
            if (timeSpan.TotalDays < 1)
                return timeSpan.ToString(@"hh\:mm\:ss");

            return timeSpan.TotalDays < 2
                ? timeSpan.ToString(@"d\ \d\a\y\ hh\:mm\:ss")
                : timeSpan.ToString(@"d\ \d\a\y\s\ hh\:mm\:ss");
        }
    }
}


