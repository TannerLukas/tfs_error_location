using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Tfs_Error_Location
{
    class FileProvider
    {
        /// <summary>
        /// reads the whole content of a stream into a string
        /// </summary>
        /// <param name="stream">the stream which should be read</param>
        /// <returns>the contents of the stream as a string</returns>
        public static string ReadStreamIntoString(Stream stream)
        {
            stream.Position = 0;
            string text;
            using (StreamReader reader = new StreamReader(stream))
            {
                text = reader.ReadToEnd();
            }

            return text;
        }

        /// <summary>
        /// reads the contents of a file into a stream.
        /// </summary>
        /// <param name="fileName">contains the path to the file</param>
        /// <returns>on success: a stream containing the contents of the file,
        /// null otherwise</returns>
        public static Stream ReadFile(string fileName)
        {
            //check if File exists
            if (File.Exists(fileName))
            {
                using (FileStream fileStream = File.OpenRead(fileName))
                {
                    MemoryStream memStream = new MemoryStream();
                    memStream.SetLength(fileStream.Length);
                    fileStream.Read(memStream.GetBuffer(), 0, (int)fileStream.Length);

                    memStream.Position = 0;

                    StreamReader reader = new StreamReader(memStream);
                    string text = reader.ReadToEnd();

                    Console.WriteLine(text);

                    return memStream;
                }
            }

            return null;
        }

        /// <summary>
        /// creates the example file contents for the old and the new file
        /// </summary>
        /// <param name="oldFile">contains the contents of the oldFile</param>
        /// <param name="newFile">contains the contents of the newFile</param>
        internal static void CreateExampleFiles(
            out Stream oldFile,
            out Stream newFile)
        {
            oldFile = GetOldExampleFileContent();
            newFile = GetNewExampleFileContent();
        }

        /// <summary>
        /// Creates example file contents of the "newFile"
        /// </summary>
        /// <returns>a stream containing the contents of the exampleFile</returns>
        private static Stream GetNewExampleFileContent()
        {
            string newFile = @"using System;
                using System.Collections.Generic;
                using System.Linq;
                using System.Text;

                namespace Tfs_Error_Location
                {
                    class TestFile2
                    {
                        public void Main(string[] args)
                        {
                            Console.WriteLine(""Hello, World"");
                        }

                        public void TestMethod1()
                        {
                            if (5 > 4)
                            {
                                //true
                                int a = 6;
                            }
                        }

                        public Dictionary<string, string> Test2(string[] args)
                        {
                            //testcomment
                            for (int i = 0; i < 10; i++)
                            {
                                //testing
                                int b = 10;
                            }

                            return null;
                        }

                        private static void TestMethod(string test)
                        {
                            test = ""testing"";
                        }
                    }
                }";

            MemoryStream stream = new MemoryStream();
            StreamWriter newWriter = new StreamWriter(stream){AutoFlush = true};
            
            newWriter.Write(newFile);

            return stream;
        }

        /// <summary>
        /// Creates example file contents of the "oldFile"
        /// </summary>
        /// <returns>a stream containing the contents of the exampleFile</returns>
        private static Stream GetOldExampleFileContent()
        {
            string oldFile = @"
                using System;
                using System.Collections.Generic;
                using System.Linq;
                using System.Text;

                namespace Tfs_Error_Location
                {
                    class TestFile1
                    {

                        public void Main(string[] args)
                        {
                            //TestComment
                            Console.WriteLine(""Hello, World"");
                        }

                        public void TestMethod1()
                        {
                            if (5 > 4)
                            {
                                int a = 6;
                            }
                        }

                        public Dictionary<string, string> Test2(string[] args)
                        {
                            //testcomment
                            TestMethod(5);

                            for (int i = 0; i < 10; i++)
                            {
                                //testing
                                int b = 10;
                            }

                            Console.WriteLine(""Hello, World"");
                            return null;
                        }

                        private static void TestMethod(int a)
                        {
                            a = 5;
                        }
                    }
                }";

            MemoryStream stream = new MemoryStream();
            StreamWriter oldWriter = new StreamWriter(stream){AutoFlush = true};
            oldWriter.Write(oldFile);

            return stream;
        }
    }
}
