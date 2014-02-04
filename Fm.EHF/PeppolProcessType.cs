using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fm.EHF
{
    /// <summary>
    /// PEPPOL Process Type
    /// </summary>
    public class PeppolProcessType
    {
        /// <summary>
        /// BII04 - invoice only
        /// </summary>
        public static string Bii04V1 = "urn:www.cenbii.eu:profile:bii04:ver1.0";


        /// <summary>
        /// fast verdi
        /// POLICY 15: PEPPOL BusDox Process Identifier scheme. The PEPPOL BusDox process identifier scheme to be used is: cenbii-procid-ubl
        /// </summary>
        public static string ProcessIdSchema = "cenbii-procid-ubl";
    }
}
