using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TfsMethodChanges
{
    /// <summary>
    /// describes a line in an iniFile
    /// by the LineContent and the LineNumber
    /// </summary>
    class Line
    {
        public string LineContent { get; set; }

        public int LineNumber { get; set; }
    }
}
