using System;
using System.Xml;
using System.Xml.Linq;

namespace Fm.EHF
{
    public class ValidationResult
    {
        public ValidationResult(XmlDocument response, TimeSpan timeSpan)
        {
            Response = response;
            TimeItTookToValidate = timeSpan;

            //TODO: populate IsValid, HasValidationErrors, HasWarnings, etc. based on xml
        }

        public XmlDocument Response { get; set; }

        public TimeSpan TimeItTookToValidate { get; set; }
    }
}
