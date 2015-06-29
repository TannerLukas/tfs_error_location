using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Tfs_Error_Location
{
    class TestFile2
    {
        public void Main(string[] args)
        {
            Console.WriteLine("Hello, World");
        }

        public void Testing1()
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
            test = "testing";
        }
    }
}
