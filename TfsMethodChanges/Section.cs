using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TfsMethodChanges
{
    /// <summary>
    /// Defines a section with a name and all corresponding KeyValuePairs
    /// of this section
    /// </summary>
    class Section
    {
        public string SectionName { get; set; }

        public IEnumerable<KeyValuePair<string, string>> KeyValuePairs { get; set; }
    }
}
