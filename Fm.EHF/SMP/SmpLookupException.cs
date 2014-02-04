using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fm.EHF.SMP
{
    /// <summary>
    /// Exception when LookupSMP fails
    /// </summary>
    public class SmpLookupException : Exception
    {
        public SmpLookupException(string message, string smpurl, Exception innerException)
            : base(message, innerException)
        {
            SmpUrl = smpurl;
        }

        public string SmpUrl { get; set; }
    }
}
