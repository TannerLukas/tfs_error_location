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
            Console.WriteLine("Hello, World");
        }

        public void Testing1()
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

            Console.WriteLine("Hello, World");
            return null;
        }

        private static void TestMethod(int a)
        {
            a = 5;
        }
    }
}
