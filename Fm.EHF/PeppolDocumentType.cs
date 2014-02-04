using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fm.EHF
{
    /// <summary>
    /// PEPPOL Document Type
    /// </summary>
    public class PeppolDocumentType
    {
        /// <summary>
        /// PEPPOL BIS invoice outside Norway, profile invoice only (v1-v2).
        /// PEPPOL BIS faktura utenfor Norge, profil kun faktura.
        /// 
        /// Bruk denne om du skal sende faktura til utlandet
        /// </summary>
        public static string InvoicePeppol4a = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2::Invoice##urn:www.cenbii.eu:transaction:biicoretrdm010:ver1.0:#urn:www.peppol.eu:bis:peppol4a:ver1.0::2.0";

        /// <summary>
        /// EHF invoice in Norway, profile invoice only (v1-v2).
        /// EHF faktura i Norge, profil kun faktura.
        /// 
        /// Bruk denne om du bare skal sende faktura i Norge (ikke kreditnota eller purring)
        /// </summary>
        public static string InvoicePeppol4aEHF = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2::Invoice##urn:www.cenbii.eu:transaction:biicoretrdm010:ver1.0:#urn:www.peppol.eu:bis:peppol4a:ver1.0#urn:www.difi.no:ehf:faktura:ver1::2.0";

        /// <summary>
        /// fast verdi
        /// </summary>
        public static string DocumentTypeSchema = "busdox-docid-qns";
    }
}