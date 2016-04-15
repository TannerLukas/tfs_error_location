using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace CHSL
{
    public class ConsoleProgressBar
    {
        // Volatile is used as hint to the compiler that this data 
        // member will be accessed by multiple threads. 
        private volatile bool m_ShouldStop;
        private volatile bool m_LoadFinished;
        private volatile bool m_ErrorOccurred;
        private volatile string m_WorkMessage;

        private int m_ProgressBarComplete;
        private int m_ProgressBarMaxVal;
        private readonly int m_ProgressBarSize = Console.WindowWidth - 20;
        private const ConsoleColor s_BackColor = ConsoleColor.DarkGreen;
        private const ConsoleColor s_ProgressColor = ConsoleColor.Green;

        private const char s_ProgressBarChar = '█';
        //private const char s_ProgressBarChar = '#';

        private int m_Counter;

        public ConsoleProgressBar()
        {
            m_Counter = 0;
            m_ShouldStop = false;
            m_LoadFinished = false;
            m_ErrorOccurred = false;
            m_WorkMessage = String.Empty;
        }

        public void RefreshBar(
            int refreshValue = 0,
            int incValue = 1)
        {
            if (refreshValue != 0)
            {
                m_ProgressBarComplete = refreshValue;
            }
            else
            {
                m_ProgressBarComplete += incValue;
            }
        }

        public void SetMaxVal(int maxVal)
        {
            m_ProgressBarMaxVal = maxVal;
        }

        public void SetErrorOccurred()
        {
            m_ErrorOccurred = true;
        }

        public void SetLoadFinished(string message)
        {
            m_LoadFinished = true;
            m_WorkMessage = message;
        }

        public void RequestStop()
        {
            m_ShouldStop = true;
        }

        public void Turn()
        {
            Console.CursorVisible = false;
            Console.Write("Loading....");

            //while the tfs services are loaded print a console spinner
            while (!m_LoadFinished &&
                   !m_ErrorOccurred)
            {
                switch (m_Counter%4)
                {
                    case 0:
                        Console.Write("|");
                        m_Counter = 0;
                        break;
                    case 1:
                        Console.Write("/");
                        break;
                    case 2:
                        Console.Write("-");
                        break;
                    case 3:
                        Console.Write("\\");
                        break;
                }
                m_Counter++;
                Console.SetCursorPosition(Console.CursorLeft - 1, Console.CursorTop);
                Thread.Sleep(120);
            }

            //if an error occurred during the loading process stop working
            if (m_ErrorOccurred)
            {
                return;
            }

            Console.Write(m_WorkMessage + "\r\n");

            int lastVal = -1;

            //while the maxValue is not reached and no error occurred or a stop request is send
            while (m_ProgressBarComplete != m_ProgressBarMaxVal &&
                   !m_ShouldStop && !m_ErrorOccurred)
            {
                if (lastVal != m_ProgressBarComplete)
                {
                    DrawProgressBar(m_ProgressBarComplete, m_ProgressBarMaxVal);
                    lastVal = m_ProgressBarComplete;
                }
                Thread.Sleep(1000);
            }

            //if no error occurred draw the progress bar once more, because it could 
            //be possible that m_ProgressBarComplete == m_ProgressBarMaxVal and is not
            //drawed anymore
            if (!m_ErrorOccurred)
            {
                DrawProgressBar(m_ProgressBarComplete, m_ProgressBarMaxVal);
            }
        }

        /// <summary>
        /// draws a progress bar on the console
        /// </summary>
        /// <param name="complete">the current value of the bar</param>
        /// <param name="maxVal">the highest value which could be reached (=100%)</param>
        private void DrawProgressBar(
            int complete,
            int maxVal)
        {
            Console.CursorVisible = false;
            int cursorPosition = Console.CursorLeft;

            double percentage = complete/(double)maxVal;
            int chars = (int) Math.Floor(percentage/(1 / (double)m_ProgressBarSize));

            string progress = String.Empty;
            for (int i = 0; i < chars; i++)
            {
                progress += s_ProgressBarChar;
            }

            string fillBar = String.Empty;
            for (int i = 0; i < m_ProgressBarSize - chars; i++)
            {
                fillBar += s_ProgressBarChar;
            }

            Console.ForegroundColor = s_ProgressColor;
            Console.Write(progress);
            Console.ForegroundColor = s_BackColor;
            Console.Write(fillBar);
            Console.ResetColor();
            //prints the percentage with 2 digits after the comma
            Console.Write(" {0}%", (percentage * 100).ToString("N2"));
            Console.CursorLeft = cursorPosition;
        }
    }
}
