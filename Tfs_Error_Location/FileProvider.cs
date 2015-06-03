using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Tfs_Error_Location
{
    class FileProvider
    {
        public static void GetFiles(out string oldFile, out string newFile)
        {
            oldFile = ReadWholeFile("..\\..\\TestFile1.cs");
            newFile = ReadWholeFile("..\\..\\TestFile2.cs");
        }

        private static string ReadWholeFile(string fileName)
        {
            string text;
            using (StreamReader reader = new StreamReader(fileName))
            {
                text = reader.ReadToEnd();
            }

            return text;
        }
    }
}
