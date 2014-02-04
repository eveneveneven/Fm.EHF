using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using STARTLibrary.src.eu.peppol.start.xml.structure;

namespace Fm.EHF.SMP
{
    /// <summary>
    /// ServiceMetadataProvider-information
    /// </summary>
    public class SmpInformation
    {
        /// <summary>
        /// Address of accesspoint
        /// </summary>
        public string Address { get; set; }

        /// <summary>
        /// Constructed SMP-URL used to get metadata
        /// </summary>
        public string FromSmpUrl { get; set; }

        /// <summary>
        /// Original metadata returned from smp-url
        /// </summary>
        public ServiceGroup ServiceGroupElements { get; set; }
    }
}
