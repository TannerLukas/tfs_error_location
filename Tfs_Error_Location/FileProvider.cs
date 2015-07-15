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
        /// reads the contents of a file into a string.
        /// </summary>
        /// <param name="fileName">contains the path to the file</param>
        /// <returns>on success: a stream containing the contents of the file,
        /// null otherwise</returns>
        public static string ReadFile(string fileName)
        {
            try
            {
                //check if File exists
                using (StreamReader reader = new StreamReader(fileName))
                {
                    string fileContent = reader.ReadToEnd();
                    return fileContent;
                }
            }
            catch (IOException exception)
            {
                Console.WriteLine(exception.Message);
            }

            return null;
        }

        /// <summary>
        /// creates the example file contents for the old and the new file
        /// </summary>
        /// <param name="oldFile">contains the contents of the oldFile</param>
        /// <param name="newFile">contains the contents of the newFile</param>
        public static void CreateExampleFiles(
            out string oldFile,
            out string newFile)
        {
            oldFile = GetOldExampleFileContent();
            newFile = GetNewExampleFileContent();
        }

        /// <summary>
        /// Creates example file contents of the "newFile"
        /// </summary>
        /// <returns>a string containing the contents of the exampleFile</returns>
        private static string GetNewExampleFileContent()
        {
            string newFile = @"using System;
                using System.Collections.Generic;
                using System.Linq;
                using System.Text;

                namespace Tfs_Error_Location
                {
                    class TestFile1
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

            return newFile;
        }

        /// <summary>
        /// Creates example file contents of the "oldFile"
        /// </summary>
        /// <returns>a string containing the contents of the exampleFile</returns>
        private static string GetOldExampleFileContent()
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

            return oldFile;
        }
    }
}
