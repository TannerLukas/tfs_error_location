using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace TfsMethodChanges
{
    public class ConsoleSpinner
    {
        // Volatile is used as hint to the compiler that this data 
        // member will be accessed by multiple threads. 
        private volatile bool m_ShouldStop;
        int m_Counter;
        public ConsoleSpinner()
        {
            m_Counter = 0;
            m_ShouldStop = false;
        }

        public void Turn()
        {
            Console.Write("Working....");
            while (!m_ShouldStop)
            {
                switch (m_Counter % 4)
                {
                    case 0: 
                        Console.Write("|");
                        m_Counter = 0;
                        break;
                    case 1: 
                        Console.Write("/"); 
                        break;
                    case 2: Console.Write("-"); 
                        break;
                    case 3: Console.Write("\\");
                        break;   
                }
                m_Counter++;
                Console.SetCursorPosition(Console.CursorLeft - 1, Console.CursorTop);
                Thread.Sleep(120);
            }
        }

        public void RequestStop()
        {
            m_ShouldStop = true;
        }
    }
}
