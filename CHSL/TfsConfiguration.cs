using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CHSL
{
    /// <summary>
    /// contains configuration information which used to establish a connection
    /// to a tfs server and project
    /// </summary>
    class TfsConfiguration
    {
        public TfsConfiguration(
            string server,
            string project)
        {
            Server = server;
            Project = project;
        }

        /// <summary>
        /// contains the serverpath
        /// </summary>
        public string Server
        {
            get;
            private set;
        }

        /// <summary>
        /// contains the name of a project of the tfs server
        /// </summary>
        public string Project
        {
            get;
            private set;
        }
    }

}
